using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Path = System.IO.Path;

// TODO: Handle Steam not being installed.
// TODO: Handle install aborts by checking processes - probably via known process names by library.
// TODO: Might be able to get Playnite exe path with `Assembly.GetExecutingAssembly().GetName().Name`
// TODO: Optimise library import handler by moving things to start up.
// TODO: Improve UI behaviour when import is happening.
// TODO: Look into dependency injection.
// TODO: Tidy up everything in general, it's a mess!
namespace SteamRomManagerCompanion
{
    public class SteamRomManagerCompanion : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly SteamHelper steamHelper;
        private readonly SteamRomManagerHelper steamRomManager;
        private readonly PlayniteHelper playniteHelper;
        private readonly LaunchGameUriHandler uriHandler;
        private readonly FilesystemHelper filesystemHelper;

        private SteamRomManagerCompanionSettingsViewModel Settings { get; set; }

        public override Guid Id { get; } = Guid.Parse("5fe1d136-a9dc-44d7-80d2-43c02df6e546");

        public SteamRomManagerCompanion(IPlayniteAPI api) : base(api)
        {
            steamHelper = new SteamHelper();
            uriHandler = new LaunchGameUriHandler(api);

            filesystemHelper = new FilesystemHelper(new FilesystemHelperArgs
            {
                binariesDataDir = Path.Combine(GetPluginUserDataPath(), "binaries"),
                manifestsDataDir = Path.Combine(GetPluginUserDataPath(), "manifests"),
                scriptsDir = Path.Combine(GetPluginUserDataPath(), "scripts"),
                stateDataDir = Path.Combine(GetPluginUserDataPath(), "state"),
            });

            playniteHelper = new PlayniteHelper(
                new PlayniteHelperArgs
                {
                    api = api,
                    filesystemHelper = filesystemHelper
                }
            );

            steamRomManager = new SteamRomManagerHelper(
                new SteamRomManagerHelperArgs
                {
                    BinaryDestinationFilename = "steam-rom-manager.exe",
                    BinarySourceUri = "https://github.com/SteamGridDB/steam-rom-manager/releases/download/v2.4.17/Steam-ROM-Manager-portable-2.4.17.exe",
                    FileSystemHelper = filesystemHelper,
                    SteamInstallDir = steamHelper.GetInstallPath(),
                    SteamActiveUsername = steamHelper.GetActiveSteamUsername()
                }
            );

            Settings = new SteamRomManagerCompanionSettingsViewModel(this);
            Properties = new GenericPluginProperties { HasSettings = true };
        }

        /**
         * Registers a URI handler that starts a game, or falls back to install, when necessary.
         */
        public override async void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            // Copy our start script to the extension scripts directory.
            var resourceName = "SteamRomManagerCompanion.Source.Scripts.launch.cmd";
            var script = Path.Combine(filesystemHelper.scriptsDir, "launch.cmd");
            filesystemHelper.WriteResourceToFile(resourceName, script);

            // Enable requests for starting or installing a game.
            uriHandler.Register(new RegisterArgs
            {
                PlayniteApi = PlayniteApi,
                OnPostLaunchGame = (Game game) =>
                {
                    playniteHelper.SaveGameActiveState(game);
                },
                OnInstallAbort = (Game game) =>
                {
                    playniteHelper.DeleteGameActiveState();
                }
            });

            // Fetch Steam Rom Manager and store in the binaries directory.
            // We do this on start-up to optimise run time during library refresh.
            _ = await steamRomManager.Initialise();

            // TODO: Any processes that don't rely on library update can be
            // performed here to optimise run speed.
        }

        public override void OnGameInstalled(OnGameInstalledEventArgs args)
        {
            playniteHelper.DeleteGameActiveState();
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            playniteHelper.DeleteGameActiveState();
        }

        public override async void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            logger.Info("library updated, attempting to sync Playnite library to Steam");

            // Fetch Steam Rom Manager if it failed to download on start-up.
            if (!steamRomManager.IsBinaryDownloaded())
            {
                logger.Warn("steam rom manager not installed, downloading");
                if (!await steamRomManager.Initialise())
                {
                    PlayniteApi.Notifications.Add(
                        new NotificationMessage("srm_error", "Your library could not be synced with Steam: unable to download Steam Rom Manager.", NotificationType.Error)
                    );
                    logger.Error("unable to download steam rom manager after library import. skipping auto-import into steam.");
                    return;
                }
            }

            // Get all visible "non Steam" games.
            var nonSteamGames = PlayniteApi.Database.Games
                .Where((g) => !g.Hidden)
                .Where(g => !playniteHelper.GetLibraryPlugin(g).Name.ToLower().Contains("steam"));

            // Do a very basic check on whether we need to re-import or not.
            var cacheFile = Path.Combine(filesystemHelper.stateDataDir, "cache");
            var prevGameIds = filesystemHelper.ReadFile(cacheFile);
            var nextGameIds = string.Join(",", nonSteamGames.Select(x => x.Id).OrderBy(x => x));

            if (prevGameIds == nextGameIds)
            {
                logger.Info("no changes since the previous library import. skipping.");
                return;
            }

            logger.Info("changes to library detected. continuing.");

            // Write the list of games we're importing to the cache for comparison next time.
            filesystemHelper.WriteFile(cacheFile, nextGameIds);

            logger.Info($"{nonSteamGames.Count()} games found. grouping by library into steam rom manager manifest format");

            // Grab the Playnite "Start in" path once before mapping.
            var playniteInstallDir = playniteHelper.GetInstallPath();

            // Group the games into their respective libraries.
            var manifestsByLibrary = steamRomManager.CreateLibraryManifestDict(
                new CreateLibraryManifestDictionaryArgs
                {
                    Games = nonSteamGames,
                    GroupBySelectorFunc = (g) => playniteHelper.GetLibraryPlugin(g),
                    StartIn = playniteInstallDir,
                    Target = Path.Combine(filesystemHelper.scriptsDir, "launch.cmd"),
                    LaunchOptions = "{id}",
                });

            var manifestsDataDir = filesystemHelper.manifestsDataDir;

            logger.Info($"cleaning data directory: {manifestsDataDir}");

            // Ensure the manifests directory exists and clear any existing files from previous library updates.
            filesystemHelper.CreateDirectory(manifestsDataDir);
            filesystemHelper.DeleteDirectoryContents(manifestsDataDir);

            logger.Info($"writing manifests for {manifestsByLibrary.Count()} libraries");

            // Write manifests for each library.
            manifestsByLibrary.ForEach((manifestByLibraryPair) =>
            {
                var libraryName = manifestByLibraryPair.Key;
                var manifestJson = manifestByLibraryPair.Value;
                var path = Path.Combine(manifestsDataDir, libraryName, "manifest.json");

                filesystemHelper.WriteJson(path, manifestJson);

                logger.Info($"manifest.json file written to: {path}");
            });

            logger.Info("generating steam rom manager parser configurations and settings");

            // Generate Steam Rom Manager parser configurations for each library.
            var libraryPluginNames = manifestsByLibrary.Select((pair) => pair.Key).ToArray();
            var userConfigurations = steamRomManager.CreateUserConfigurations(libraryPluginNames);

            logger.Info("writing steam rom manager parser configurations");

            // Write them to Steam Rom Manager's config directory.
            steamRomManager.WriteUserConfigurations(userConfigurations);

            // If Steam is running, we need to shut it down as we're about to write to it's .vdf files.
            if (steamHelper.IsRunning())
            {
                logger.Info("steam process already running, ending the process in preparation for library import");
                steamHelper.Stop();
            }

            PlayniteApi.MainView.SwitchToLibraryView();

            //_ = PlayniteApi.Dialogs.GetCurrentAppWindow().Activate();
            //PlayniteApi.Dialogs.ActivateGlobalProgress(new GlobalProgressActionArgs {Text }, new GlobalProgressOptions { Cancelable = false });
            _ = PlayniteApi.Dialogs.ShowMessage("Syncing library to Steam. This may take a few minutes. Please do not open Steam.");

            logger.Info("initialising library import");

            // Import the library of games.
            _ = await steamRomManager.ImportLibrary();

            logger.Info("import completed");

            // Let the user know the good news!
            PlayniteApi.Notifications.Add(
                new NotificationMessage("srm_success", "Your library was successfully synced into Steam.", NotificationType.Info)
            );
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
