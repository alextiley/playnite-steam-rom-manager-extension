using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Plugins;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using Path = System.IO.Path;

// TODO Handle Steam not being installed.
namespace SteamRomManagerCompanion
{
    public class SteamRomManagerCompanion : GenericPlugin
    {
        private const string uriHandlerToRegister = "steam-launcher";
        private const string processRestartFlags = "--hidesplashscreen --startclosedtotray --nolibupdate";

        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly string binariesDataDir;
        private readonly string librariesDataDir;

        private readonly SteamHelper steamHelper;
        private readonly SteamRomManagerHelper steamRomManager;
        private readonly PlayniteHelper playniteHelper;
        private readonly LaunchGameUriHandler uriHandler;
        private readonly FilesystemHelper filesystemHelper;

        private SteamRomManagerCompanionSettingsViewModel settings { get; set; }

        public override Guid Id { get; } = Guid.Parse("5fe1d136-a9dc-44d7-80d2-43c02df6e546");

        public SteamRomManagerCompanion(IPlayniteAPI api) : base(api)
        {
            binariesDataDir = Path.Combine(GetPluginUserDataPath(), "binaries");
            librariesDataDir = Path.Combine(GetPluginUserDataPath(), "libraries");

            steamHelper = new SteamHelper();
            playniteHelper = new PlayniteHelper();
            filesystemHelper = new FilesystemHelper();
            settings = new SteamRomManagerCompanionSettingsViewModel(this);

            steamRomManager = new SteamRomManagerHelper(
                new SteamRomManagerHelperArgs
                {
                    BinariesDataDir = binariesDataDir,
                    LibrariesDataDir = librariesDataDir,
                    BinaryDestinationFilename = "steam-rom-manager.exe",
                    BinarySourceUri = "https://github.com/SteamGridDB/steam-rom-manager/releases/download/v2.4.17/Steam-ROM-Manager-portable-2.4.17.exe",
                    FileSystemHelper = filesystemHelper,
                    SteamInstallDir = steamHelper.GetInstallPath(),
                    PlayniteInstallDir = playniteHelper.GetInstallPath(),
                    SteamActiveUsername = steamHelper.GetActiveSteamUsername()
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
            filesystemHelper.Delete(
                Path.Combine(playniteHelper.GetInstallPath(), "safestart.flag")
            );

            // Enable requests for starting or installing a game.
            uriHandler.Register(uriHandlerToRegister);

            // Fetch Steam Rom Manager and store in the binaries directory.
            _ = await steamRomManager.DownloadBinary();
        }

        public override void OnGameInstalled(OnGameInstalledEventArgs args)
        {
            // Restart Playnite when a game finishes installing.
            // Steam will not think install has ended if the Playnite process
            // that it spawned is still running.
            if (uriHandler.WasTriggered)
            {
                logger.Info("game installed, restarting Playnite to trick steam into ending the running session");
                playniteHelper.Restart(processRestartFlags);
            }
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            // Restart Playnite when a game session ends.
            // Steam will not think play has ended if the Playnite process
            // that it spawned is still running.
            if (uriHandler.WasTriggered)
            {
                logger.Info("game ended, restarting Playnite to trick steam into ending the running session");
                playniteHelper.Restart(processRestartFlags);
            }
        }

        public override async void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            logger.Info("library updated, attempting to sync Playnite library to Steam");

            // Fetch Steam Rom Manager if it failed to download on start-up.
            if (!steamRomManager.IsBinaryDownloaded())
            {
                logger.Warn("steam rom manager not available, attempting to re-download");
                if (!await steamRomManager.DownloadBinary())
                {
                    PlayniteApi.Notifications.Add(
                        new NotificationMessage("srm_error", "Your library could not be synced with Steam: unable to download Steam Rom Manager.", NotificationType.Error)
                    );
                    logger.Error("unable to download steam rom manager after library import. skipping auto-import into steam.");
                    return;
                }
            }

            // Get all visible games.
            var allGames = PlayniteApi.Database.Games;
            var games = allGames.Where((g) => !g.Hidden);
            var plugins = PlayniteApi.Addons.Plugins;
            var playniteInstallDir = playniteHelper.GetInstallPath();

            logger.Info($"{games.Count()} games found. grouping by library into steam rom manager manifest format");

            // Group the games into their respective libraries.
            var libraryManifestPairs = steamRomManager.CreateLibraryManifestDict(
                new CreateLibraryManifestDictionaryArgs
                {
                    Games = games,
                    LibraryPluginFilterFn = (g) => (LibraryPlugin)plugins.Find((p) => p.Id == g.PluginId),
                    StartIn = playniteInstallDir,
                    Target = $"playnite://{uriHandlerToRegister}/{{id}}",
                });

            logger.Info($"cleaning data directory: {librariesDataDir}");

            // Clear manifests from previous library updates / ensure directory exists.
            filesystemHelper.CreateDirectory(librariesDataDir);
            filesystemHelper.DeleteDirectoryContents(librariesDataDir);

            logger.Info($"writing manifests for {libraryManifestPairs.Count()} libraries");

            // Write manifests for each library.
            _ = Parallel.ForEach(libraryManifestPairs, (pair) =>
            {
                var library = pair.Key;
                var manifest = pair.Value;
                var path = Path.Combine(librariesDataDir, library.Name, "manifest.json");

                filesystemHelper.WriteJson(path, manifest);

                logger.Info($"manifest.json file written to: {path}");
            });

            logger.Info("generating steam rom manager parser configurations and settings");

            // Generate Steam Rom Manager parser configurations for each library.
            // Skip Steam, we don't need non-steam shortcuts for those!
            var userConfigurations = steamRomManager.CreateUserConfigurations(
                libraryManifestPairs
                    .Select((pair) => pair.Key)
                    .SkipWhile(x => x.Name.ToLower().Contains("steam"))
            );

            // Now generate the global settings config.
            var userSettings = steamRomManager.CreateUserSettings();

            logger.Info("writing steam rom manager parser configurations and settings");

            // Write them to Steam Rom Manager's config directory.
            steamRomManager.WriteUserConfigurations(userConfigurations);
            steamRomManager.WriteUserSettings(userSettings);

            // If Steam is running, we need to shut it down as we're about to write to it's .vdf files.
            var steamWasRunning = steamHelper.IsRunning();
            if (steamWasRunning)
            {
                logger.Info("steam process already running, ending the process in preparation for library import");
                steamHelper.Stop();
            }

            logger.Info("initialising library import");

            // Import the library of games.
            steamRomManager.ImportLibrary();

            logger.Info("import completed");

            // Let the user know the good news!
            PlayniteApi.Notifications.Add(
                new NotificationMessage("srm_success", "Your library was successfully synced into Steam.", NotificationType.Info)
            );
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
