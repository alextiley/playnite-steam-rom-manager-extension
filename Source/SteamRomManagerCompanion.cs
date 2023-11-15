using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Path = System.IO.Path;

// TODO Handle Steam not being installed.
namespace SteamRomManagerCompanion
{
    public class SteamRomManagerCompanion : GenericPlugin
    {
        private const string uriHandlerToRegister = "install-or-start";
        private const string processRestartFlags = "--hidesplashscreen --startclosedtotray --nolibupdate";

        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly string binariesDataDir;
        private readonly string librariesDataDir;

        private readonly SteamProcessHelper steamProcess;
        private readonly SteamRomManager steamRomManager;
        private readonly PlayniteProcessHelper playniteProcess;
        private readonly LaunchGameUriHandler uriHandler;
        private readonly FileSystemHelper fileSystem;

        private SteamRomManagerCompanionSettingsViewModel Settings { get; set; }


        public override Guid Id { get; } = Guid.Parse("5fe1d136-a9dc-44d7-80d2-43c02df6e546");

        public SteamRomManagerCompanion(IPlayniteAPI api) : base(api)
        {
            binariesDataDir = Path.Combine(GetPluginUserDataPath(), "binaries");
            librariesDataDir = Path.Combine(GetPluginUserDataPath(), "libraries");

            steamProcess = new SteamProcessHelper();
            playniteProcess = new PlayniteProcessHelper();
            fileSystem = new FileSystemHelper();
            Settings = new SteamRomManagerCompanionSettingsViewModel(this);

            steamRomManager = new SteamRomManager(
                new SteamRomManagerArgs
                {
                    Filename = Path.Combine(binariesDataDir, "steam-rom-manager.exe"),
                    Source = "https://github.com/SteamGridDB/steam-rom-manager/releases/download/v2.4.17/Steam-ROM-Manager-portable-2.4.17.exe"
                }
            );

            uriHandler = new LaunchGameUriHandler(
                new LaunchGameUriHandlerArgs
                {
                    PlayniteAPI = api,
                }
            );

            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };
        }

        /**
         * Registers a URI handler that starts a game, or falls back to install, when necessary.
         */
        public override async void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            // Remove safestart.flag file. This is a hack!
            // The file is created by Playnite due to how we end the process.
            // Removing this file prevents the prompt from showing when we restart.
            fileSystem.Delete(
                Path.Combine(playniteProcess.GetInstallPath(), "safestart.flag")
            );

            // Create libraries data directory if it doesn't exist.
            fileSystem.CreateDirectory(librariesDataDir);

            // Enable requests for starting or installing a game.
            uriHandler.Register(uriHandlerToRegister);


            // Fetch Steam Rom Manager and store in the binaries data 
            if (!await steamRomManager.DownloadBinary())
            {
                logger.Error("unable to download steam rom manager binary");
            }
        }

        public override void OnGameInstalled(OnGameInstalledEventArgs args)
        {
            // Restart Playnite when a game finishes installing.
            // Steam will not think install has ended if the Playnite process
            // that it spawned is still running.
            playniteProcess.Restart(processRestartFlags);
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            // Restart Playnite when a game session ends.
            // Steam will not think play has ended if the Playnite process
            // that it spawned is still running.
            playniteProcess.Restart(processRestartFlags);
        }

        public override async void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            logger.Info("library updated, fetching list of games");

            var playniteInstallDir = playniteProcess.GetInstallPath();

            // Get all visible games.
            var games = PlayniteApi.Database.Games
                .Where((game) => !game.Hidden);

            logger.Info($"{games.Count()} games found. grouping games by library into steam rom manager manifest format");

            // Group the games into their respective libraries.
            var mappings = steamRomManager.BuildManifestGroups(
                new BuildManifestGroupsArgs
                {
                    Games = games,
                    LibraryPluginFilterFn = (Game game) =>
                        PlayniteApi.Addons.Plugins.Find(
                            (plugin) => plugin.Id == game.PluginId
                        ) as LibraryPlugin,
                    StartIn = playniteInstallDir,
                    Target = $"playnite://{uriHandlerToRegister}/{{id}}",
                }
            );

            logger.Info($"cleaning data directory: {librariesDataDir}");

            // Clear manifests from previous library updates.
            fileSystem.DeleteDirectoryContents(librariesDataDir);


            // Write manifests for each library.
            mappings.ForEach((mapping) =>
            {
                logger.Info($"writing manifest for library {mapping.Key.Name}");

                var library = mapping.Key;
                var manifest = mapping.Value;

                var path = Path.Combine(librariesDataDir, library.Name, "manifest.json");

                fileSystem.WriteJson(path, manifest);

                logger.Info($"manifest.json file written to: {path}");
            });

            // Fetch the Steam Rom Manager if it failed to download on start-up.
            if (!steamRomManager.IsBinaryAvailable())
            {
                logger.Warn("steam rom manager not available, attempting to re-download");
                var downloaded = await steamRomManager.DownloadBinary();
                if (!downloaded)
                {
                    logger.Error("unable to download steam rom manager after library import. skipping auto-import into Steam.");
                    return;
                }
            }

            // TODO: Generate SRM parser configuration JSON for each manifest.

            if (steamProcess.IsRunning())
            {
                PlayniteApi.Notifications.Add(
                    new NotificationMessage("steam_closed", "The Steam client was temporarily closed in order to import your library.", NotificationType.Info)
                );
                steamProcess.Stop();
            }

            // TODO: Run SRM for each manifest and parser configuration combination.

            // TODO: Restart Steam if it was previously running.
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return Settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new SteamRomManagerCompanionSettingsView();
        }
    }
}
