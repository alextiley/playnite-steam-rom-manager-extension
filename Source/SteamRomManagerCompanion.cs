using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Controls;

namespace SteamRomManagerCompanion
{
    public class SteamRomManagerCompanion : GenericPlugin
    {
        private const string uriHandlerToRegister = "install-or-start";
        private const string processRestartFlags = "--hidesplashscreen --startclosedtotray --nolibupdate";

        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly string librariesDataDir;

        private readonly PlayniteProcessHelper playniteProcess;
        private readonly LaunchGameUriHandler uriHandler;
        private readonly FileSystemHelper fileSystem;

        private SteamRomManagerCompanionSettingsViewModel settings { get; set; }


        public override Guid Id { get; } = Guid.Parse("5fe1d136-a9dc-44d7-80d2-43c02df6e546");

        public SteamRomManagerCompanion(IPlayniteAPI api) : base(api)
        {
            librariesDataDir = Path.Combine(GetPluginUserDataPath(), "libraries");

            playniteProcess = new PlayniteProcessHelper();
            fileSystem = new FileSystemHelper();
            settings = new SteamRomManagerCompanionSettingsViewModel(this);

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
        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
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

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            logger.Info("library updated, fetching list of games");

            string playniteInstallDir = playniteProcess.GetInstallPath();

            // Get all visible games.
            IEnumerable<Game> games = PlayniteApi.Database.Games
                .Where((game) => !game.Hidden);

            logger.Info($"{games.Count()} games found. grouping games by library into steam rom manager manifest format");

            // Group the games into their respective libraries.
            Dictionary<LibraryPlugin, List<SteamRomManagerManifestEntry>> mappings = games
                .GroupBy(
                    game => PlayniteApi.Addons.Plugins.Find(
                        (plugin) => plugin.Id == game.PluginId
                    ) as LibraryPlugin
                )
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(
                        game => new SteamRomManagerManifestEntry(
                            new SteamRomManagerManifestEntryArgs
                            {
                                LaunchOptions = "",
                                StartIn = playniteInstallDir,
                                Target = $"playnite://{uriHandlerToRegister}/{game.Id}",
                                Title = game.Name
                            }
                         )
                    ).ToList()
                );

            logger.Info($"cleaning data directory: {librariesDataDir}");

            // Clear manifests from previous library updates.
            fileSystem.DeleteDirectoryContents(librariesDataDir);

            logger.Info($"writing manifest files to {librariesDataDir} for {mappings.Count()} libraries");

            mappings.ForEach((mapping) =>
            {
                logger.Info($"writing manifest for library {mapping.Key.Name}");

                LibraryPlugin library = mapping.Key;
                List<SteamRomManagerManifestEntry> manifest = mapping.Value;

                string path = Path.Combine(librariesDataDir, library.Name, "manifest.json");

                fileSystem.WriteJson(path, manifest);

                logger.Info($"manifest.json file written to: {path}");
            });

            // TODO: Download SRM binary.

            // TODO: Generate SRM parser configuration JSON for each manifest.

            // TODO: Find Steam executable and kill it if running.

            // TODO: Run SRM for each manifest and parser configuration combination.

            // TODO: Restart Steam if it was previously running.
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new SteamRomManagerCompanionSettingsView();
        }
    }
}
