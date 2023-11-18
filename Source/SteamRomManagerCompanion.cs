using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

// TODO: Create mapping of Playnite plugins -> Steam category names
// TODO: Handle Steam not being installed.
// TODO: Handle install aborts by checking processes - probably via known process names by library.
//       May also be possible by finding spawned child processes of the current process?
// TODO: Improve UI behaviour when import is happening.
// TODO: Look into dependency injection.
// TODO: Tidy up everything in general, it's a mess!
// TODO: Delete active_game contents where game is terminated directly from steam and event handlers below are not called.
//       This might be possible for checking if our script process gets terminated.
//       May also be possible by finding spawned child processes of the current process and detected when that is terminated?
// 
// TODO: No need to copy script resources on every run. But maybe we should in case of invalidation. Need to think about this.
namespace SteamRomManagerCompanion
{
    public class SteamRomManagerCompanion : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly SteamHelper steamHelper;
        private readonly SteamRomManagerHelper steamRomManagerHelper;
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

            steamRomManagerHelper = new SteamRomManagerHelper(
                new SteamRomManagerHelperArgs
                {
                    BinaryDestinationFilename = "steam-rom-manager.exe",
                    BinarySourceUri = "https://github.com/SteamGridDB/steam-rom-manager/releases/download/v2.4.17/Steam-ROM-Manager-portable-2.4.17.exe",
                    FilesystemHelper = filesystemHelper,
                    SteamInstallDir = steamHelper.GetInstallPath(),
                    SteamActiveUsername = steamHelper.GetActiveSteamUsername()
                }
            );

            Settings = new SteamRomManagerCompanionSettingsViewModel(this);
            Properties = new GenericPluginProperties { HasSettings = true };
        }

        public override async void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            if (!CopyGameScriptsToExtensionsDirectory())
            {
                return;
            }

            _ = await EnsureSteamRomManagerIsDownloaded();

            uriHandler.Register(new LaunchGameUriHandlerRegisterArgs
            {
                OnPostLaunchGame = (Game game) => playniteHelper.SaveGameActiveState(game),
                OnInstallAbort = (Game game) => playniteHelper.DeleteGameActiveState(),
            });
        }

        public override void OnGameInstalled(OnGameInstalledEventArgs args)
        {
            playniteHelper.DeleteGameActiveState();
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            playniteHelper.DeleteGameActiveState();
        }

        public override void OnGameStarted(OnGameStartedEventArgs args)
        {
            var pid = args.StartedProcessId;
            _ = PlayniteApi.Dialogs.ShowMessage($"Process started: {pid}", "Process started");
        }


        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            logger.Info("library updated, attempting to sync Playnite library to Steam");

            var nonSteamGames = playniteHelper.GetVisibleNonSteamGames();
            var (isSyncRequired, cacheValue) = steamRomManagerHelper.CheckLibrarySyncRequired(nonSteamGames);

            if (!isSyncRequired)
            {
                logger.Info("no changes since the previous library import. skipping.");
                return;
            }

            var wasSteamRunning = steamHelper.IsRunning();
            if (!CheckUserWishesToSync(wasSteamRunning))
            {
                logger.Info("user has requested to abort steam rom manager sync, exiting.");
                return;
            }

            // Only update the cache if we're actually going to perform a sync.
            steamRomManagerHelper.UpdateLibraryCache(cacheValue);

            var options = new GlobalProgressOptions("Checking for dependencies...", true)
            {
                Cancelable = false,
                IsIndeterminate = false,
            };

            _ = PlayniteApi.Dialogs.ActivateGlobalProgress(async (progress) =>
            {
                progress.CurrentProgressValue = 0;
                progress.ProgressMaxValue = 3;

                if (!await EnsureSteamRomManagerIsDownloaded())
                {
                    return;
                }

                var manifestsByLibrary = MapManifestsByLibrary(nonSteamGames);

                PrepareManifestsDirectory();
                WriteManifests(manifestsByLibrary);

                steamRomManagerHelper.WriteUserConfigurations(CreateSrmUserConfigs(manifestsByLibrary));

                if (wasSteamRunning)
                {
                    if (!CheckUserWishesToCloseSteam())
                    {
                        return;
                    }
                    steamHelper.Stop();
                }
                progress.CurrentProgressValue += 1;
                progress.Text = "Working...";

                if (!await steamRomManagerHelper.ConfigureSync())
                {
                    return;
                }
                progress.CurrentProgressValue += 1;
                progress.Text = "Adding non-Steam games. This may take a few minutes...";

                if (!await steamRomManagerHelper.StartSync())
                {
                    ShowFailedNotification("Non-Steam games could not be added.");
                    return;
                }
                progress.CurrentProgressValue += 1;
                progress.Text = "Non-Steam games successfully added! 🎉";

                if (CheckUserWishesToStartSteam(wasSteamRunning))
                {
                    steamHelper.Start();
                }
                ShowSuccessNotification("Your non-Steam games were successfully added! 🎉");

            }, options);
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return Settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new SteamRomManagerCompanionSettingsView();
        }

        private IEnumerable<SteamRomManagerParserConfig> CreateSrmUserConfigs(Dictionary<string, List<SteamRomManagerManifestEntry>> manifestsByLibrary)
        {
            var libraryPluginNames = manifestsByLibrary.Select((pair) => pair.Key).ToArray();
            return steamRomManagerHelper.CreateUserConfigurations(libraryPluginNames);
        }

        private bool CheckUserWishesToSync(bool isSteamRunning)
        {
            // TODO Move into i18n resources
            var caption = "Sync into Steam?";
            var pre = "Your library has been updated with new non-Steam games! Would you like to add them to Steam?";
            var open = isSteamRunning ? "re-open" : "open";
            var post = $"Please do not {open} Steam until import is completed.";
            var prompt = isSteamRunning
                ? $"{pre} It looks like Steam is running right now, so we'll close that for you automatically. {post}"
                : $"{pre} {post}";
            var buttons = MessageBoxButton.YesNo;
            var icon = MessageBoxImage.Question;

            return PlayniteApi.Dialogs.ShowMessage(prompt, caption, buttons, icon) == MessageBoxResult.Yes;
        }

        private bool CheckUserWishesToCloseSteam()
        {
            // TODO Move into i18n resources
            var caption = "Steam needs to be closed";
            var prompt = "A running instance of Steam was detected. Steam must be closed to perform the import. Would you like to close Steam now?";
            var buttons = MessageBoxButton.YesNo;
            var icon = MessageBoxImage.Question;

            return PlayniteApi.Dialogs.ShowMessage(prompt, caption, buttons, icon) == MessageBoxResult.Yes;
        }

        private bool CheckUserWishesToStartSteam(bool wasExitedDuringSync)
        {
            // TODO Move into i18n resources
            var caption = "Non-Steam games added!";
            var prompt = wasExitedDuringSync
                ? "Your non-Steam games were successfully added! Would you like to relaunch Steam? 👀"
                : "Your non-Steam games were successfully added! Would you like to start Steam now?";
            var buttons = MessageBoxButton.YesNo;
            var icon = MessageBoxImage.Question;

            return PlayniteApi.Dialogs.ShowMessage(prompt, caption, buttons, icon) == MessageBoxResult.Yes;
        }

        private void ShowSuccessNotification(string msg)
        {
            PlayniteApi.Notifications.Add(
                new NotificationMessage("srm_import_success", msg, NotificationType.Info)
            );
        }

        private void ShowFailedNotification(string msg)
        {
            PlayniteApi.Notifications.Add(
                new NotificationMessage("srm_import_failed", msg, NotificationType.Error)
            );
        }

        private void WriteManifests(Dictionary<string, List<SteamRomManagerManifestEntry>> manifestsByLibrary)
        {
            manifestsByLibrary.ForEach((pair) =>
            {
                var libraryName = pair.Key;
                var manifestJson = pair.Value;
                var path = Path.Combine(filesystemHelper.manifestsDataDir, libraryName, "manifest.json");

                filesystemHelper.WriteJson(path, manifestJson);

                logger.Info($"manifest.json file written to: {path}");
            });
        }

        private void PrepareManifestsDirectory()
        {
            // Ensure the manifests directory exists and clear any existing files from previous library updates.
            filesystemHelper.CreateDirectory(filesystemHelper.manifestsDataDir);
            filesystemHelper.DeleteDirectoryContents(filesystemHelper.manifestsDataDir);
        }

        private Dictionary<string, List<SteamRomManagerManifestEntry>> MapManifestsByLibrary(IEnumerable<Game> games)
        {
            // The target command for each game uses wscript.exe in order to run a batch script in a hidden window.
            return steamRomManagerHelper.CreateLibraryManifestDict(
                new CreateLibraryManifestDictionaryArgs
                {
                    Games = games,
                    GroupBySelectorFunc = (g) => playniteHelper.GetLibraryPlugin(g),
                    StartIn = playniteHelper.GetInstallPath(),
                    Target = Path.Combine(filesystemHelper.GetSystemDirectory(), "System32", "wscript.exe"),
                    LaunchOptions = $"\"{Path.Combine(filesystemHelper.scriptsDir, "launch.vbs")}\" \"{{id}}\""
                }
            );
        }

        private async Task<bool> EnsureSteamRomManagerIsDownloaded()
        {
            if (!steamRomManagerHelper.IsBinaryDownloaded())
            {
                logger.Info("steam rom manager not installed, downloading");
                if (!await steamRomManagerHelper.Initialise())
                {
                    PlayniteApi.Notifications.Add(
                        new NotificationMessage("srm_error", "Unable to download Steam Rom Manager.", NotificationType.Error)
                    );
                    logger.Error("unable to download steam rom manager.");
                    return false;
                }
            }

            return true;
        }

        private bool CopyGameScriptsToExtensionsDirectory()
        {
            try
            {
                Array.ForEach(new[] { "launch.cmd", "launch.vbs" }, filename =>
                {
                    logger.Info($"writing resource {filename} to scripts directory");
                    var path = Path.Combine(filesystemHelper.scriptsDir, filename);
                    var resourceName = $"SteamRomManagerCompanion.Source.Scripts.{filename}";
                    filesystemHelper.WriteResourceToFile(resourceName, path);
                });

                return true;
            }
            catch (Exception e)
            {
                logger.Error(e, $"unable to write script resources to {filesystemHelper.scriptsDir}");
            }

            return false;
        }
    }
}
