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

        private SteamRomManagerCompanionSettingsViewModel SettingsViewModel { get; set; }

        public override Guid Id { get; } = Guid.Parse("5fe1d136-a9dc-44d7-80d2-43c02df6e546");

        public SteamRomManagerCompanion(IPlayniteAPI api) : base(api)
        {
            SettingsViewModel = new SteamRomManagerCompanionSettingsViewModel(this);
            Properties = new GenericPluginProperties { HasSettings = true };
            steamHelper = new SteamHelper();

            uriHandler = new LaunchGameUriHandler(new LaunchGameUriHandlerArgs
            {
                PlayniteApi = api,
            });

            filesystemHelper = new FilesystemHelper(new FilesystemHelperArgs
            {
                binariesDataDir = Path.Combine(GetPluginUserDataPath(), "binaries"),
                manifestsDataDir = Path.Combine(GetPluginUserDataPath(), "manifests"),
                scriptsDir = Path.Combine(GetPluginUserDataPath(), "scripts"),
                stateDataDir = Path.Combine(GetPluginUserDataPath(), "state"),
            });

            playniteHelper = new PlayniteHelper(new PlayniteHelperArgs
            {
                api = api,
                filesystemHelper = filesystemHelper
            });

            steamRomManagerHelper = new SteamRomManagerHelper(new SteamRomManagerHelperArgs
            {
                BinaryDestinationFilename = "steam-rom-manager.exe",
                BinarySourceUri = "https://github.com/SteamGridDB/steam-rom-manager/releases/download/v2.4.17/Steam-ROM-Manager-portable-2.4.17.exe",
                FilesystemHelper = filesystemHelper,
                SteamInstallDir = steamHelper.GetInstallPath(),
                SteamActiveUsername = steamHelper.GetActiveSteamUsername()
            });
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            // When Playnite closes, clean up game state.
            playniteHelper.DeleteGameActiveState();
        }

        public override async void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            // Clean up anything that might be left if games didn't exit cleanly.
            playniteHelper.DeleteGameActiveState();

            // Copy the entrypoint script into the extension data directory.
            if (!CopyGameScriptsToExtensionsDirectory())
            {
                return;
            }

            // Fetch required binaries
            _ = await EnsureSteamRomManagerIsDownloaded();

            // Register a custom handler for running or installing games.
            uriHandler.Register(new LaunchGameUriHandlerRegisterArgs
            {
                // When a game is started by the handler (triggered by the entrypoint script), persist the game state file.
                // The entrypoint script will scan for removal of this file.
                OnStartGame = (Game game) => playniteHelper.SaveGameActiveState(game)
            });
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            // When a game ends, clean up the created state file.
            playniteHelper.DeleteGameActiveState();
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            var nonSteamGames = playniteHelper.GetVisibleNonSteamGames();
            var libraryUpdateStatuses = playniteHelper.GetLibraryUpdateStatuses(nonSteamGames);
            var updatedLibrariesData = libraryUpdateStatuses.Where(x => x.hasChanged);
            var nonUpdatedLibrariesData = libraryUpdateStatuses.Where(x => !x.hasChanged);
            var updatedLibraryCount = updatedLibrariesData.Count();
            var updatedLibraryNames = updatedLibrariesData.Select(x => x.library.Name);

            if (updatedLibraryCount == 0)
            {
                logger.Info("no changes since the previous library import. skipping.");
                return;
            }

            var wasSteamRunning = steamHelper.IsRunning();
            if (!CheckUserWishesToSync(wasSteamRunning))
            {
                return;
            }

            var options = new GlobalProgressOptions("Working...", true) { Cancelable = false };

            _ = PlayniteApi.Dialogs.ActivateGlobalProgress(async (progress) =>
            {
                progress.CurrentProgressValue = 0;
                progress.ProgressMaxValue = 3;

                if (!await EnsureSteamRomManagerIsDownloaded())
                {
                    return;
                }

                updatedLibrariesData.ForEach(updates =>
                {
                    var (library, games, _, _) = updates;
                    var manifests = games.Select(game => new SteamRomManagerManifestEntry(
                        new SteamRomManagerManifestEntryArgs
                        {
                            LaunchOptions = $"\"{Path.Combine(filesystemHelper.scriptsDir, "launch.vbs")}\" \"{game.Id}\"",
                            StartIn = playniteHelper.GetInstallPath(),
                            Target = Path.Combine(filesystemHelper.GetSystemDirectory(), "System32", "wscript.exe"),
                            Title = game.Name
                        }
                    ));
                    WriteManifest(library, manifests);
                });

                var updatedLibraryConfigs = updatedLibrariesData.Select(x =>
                {
                    var (library, _, _, _) = x;
                    var (_, title) = PlayniteHelper.LibraryDictionary[library.Id];

                    return steamRomManagerHelper.CreateUserConfiguration(library.Id, title);
                });

                // Also generate and save non updated libraries.
                // This will allow manual runs of the SRM GUI.
                var nonUpdatedLibraryConfigs = nonUpdatedLibrariesData.Select(x =>
                {
                    var (library, _, _, _) = x;
                    var (_, title) = PlayniteHelper.LibraryDictionary[library.Id];

                    return steamRomManagerHelper.CreateUserConfiguration(library.Id, title);
                });

                var allLibraryConfigs = updatedLibraryConfigs.Concat(nonUpdatedLibraryConfigs);

                steamRomManagerHelper.WriteUserConfigurations(allLibraryConfigs);

                if (wasSteamRunning)
                {
                    if (!CheckUserWishesToCloseSteam())
                    {
                        return;
                    }
                    steamHelper.Stop();
                }
                progress.CurrentProgressValue += 1;

                var configIds = updatedLibraryConfigs.Select(x => x.ParserId);

                if (!await steamRomManagerHelper.ConfigureSync(configIds))
                {
                    return;
                }
                progress.CurrentProgressValue += 1;
                progress.Text = "Adding new non-Steam games. This may take a few minutes...";

                if (!await steamRomManagerHelper.StartSync())
                {
                    ShowFailedNotification("Non-Steam games could not be added.");
                    return;
                }
                progress.CurrentProgressValue += 1;
                progress.Text = "Non-Steam games successfully added!";

                if (CheckUserWishesToStartSteam(wasSteamRunning))
                {
                    steamHelper.Start();
                }

                updatedLibrariesData.ForEach(
                    x => playniteHelper.WriteLibrarySyncCache(x.library.Id.ToString(), x.cacheValue)
                );

                ShowSuccessNotification("Your non-Steam games were successfully added!");
            }, options);
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return SettingsViewModel;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new SteamRomManagerCompanionSettingsView();
        }

        private bool CheckUserWishesToSync(bool isSteamRunning)
        {
            // TODO Move into i18n resources
            var caption = "Add non-Steam games?";
            var message = "Your library has been updated with new non-Steam games! Would you like to add them to Steam?";
            var prompt = isSteamRunning ? $"{message} Please do not open Steam until import is completed." : message;
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
                ? "Your non-Steam games were successfully added! Would you like to relaunch Steam?"
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

        private void WriteManifest(LibraryPlugin library, IEnumerable<SteamRomManagerManifestEntry> entries)
        {
            var path = Path.Combine(filesystemHelper.manifestsDataDir, library.Id.ToString(), "manifest.json");

            filesystemHelper.CreateDirectory(filesystemHelper.manifestsDataDir);
            filesystemHelper.WriteJson(path, entries);

            logger.Info($"manifest.json file written to: {path}");
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
