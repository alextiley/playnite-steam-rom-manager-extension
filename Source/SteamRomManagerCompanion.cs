using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using SteamRomManagerCompanion.Controllers;
using SteamRomManagerCompanion.Handlers;
using SteamRomManagerCompanion.Interfaces;
using SteamRomManagerCompanion.Models;
using SteamRomManagerCompanion.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Controls;

namespace SteamRomManagerCompanion
{
    public class SteamRomManagerCompanion : GenericPlugin
    {
        private const string URI_HANDLER_PATH = "install-or-start";

        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly string librariesDataDir;
        private readonly string gameStateTrackingDir;

        private readonly IProcessController playniteProcess;
        private readonly IUriHandler uriHandler;
        private readonly FileSystemController fileSystem;
        private readonly GameStateTracker gameStateTracker;

        private SteamRomManagerCompanionSettingsViewModel settings { get; set; }


        public override Guid Id { get; } = Guid.Parse("5fe1d136-a9dc-44d7-80d2-43c02df6e546");

        public SteamRomManagerCompanion(IPlayniteAPI api) : base(api)
        {
            librariesDataDir = Path.Combine(GetPluginUserDataPath(), "libraries");
            gameStateTrackingDir = Path.Combine(GetPluginUserDataPath(), "tracking");

            playniteProcess = new PlayniteProcessController();
            fileSystem = new FileSystemController();
            settings = new SteamRomManagerCompanionSettingsViewModel(this);

            gameStateTracker = new GameStateTracker(
                new GameStateTrackerArgs
                {
                    dataDir = gameStateTrackingDir,
                    fileSystem = fileSystem
                }
            );

            uriHandler = new LaunchGameUriHandler(
                new LaunchGameUriHandlerArgs
                {
                    gameStateTracker = gameStateTracker,
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
            // Reset libraries data directory
            fileSystem.CreateDirectory(librariesDataDir);
            fileSystem.EmptyDirectory(librariesDataDir);

            // Reset game state data
            fileSystem.CreateDirectory(gameStateTrackingDir);
            fileSystem.EmptyDirectory(gameStateTrackingDir);

            // Enable requests for starting or installing a game.
            uriHandler.Register(URI_HANDLER_PATH);

            // TODO: Copy the launch.cmd script into the data directory.
        }

        /**
         * When a game is installed, mark it as "stopped" in our game tracker.
         * The run script executed by Steam will poll against this state.
         */
        public override void OnGameInstalled(OnGameInstalledEventArgs args)
        {
            logger.Info($"game installed, marking as exited: {args.Game.Id}");
            gameStateTracker.Stop(args.Game.Id);
        }

        /**
         * When a game ends, mark it as stopped in our game tracker.
         * This will tell Steam to "end" the session when Playnite closes.
         */
        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            logger.Info($"game stopped, marking as exited: {args.Game.Id}");
            gameStateTracker.Stop(args.Game.Id);
        }

        /**
         * When the application is exiting, ensure we clean the game state directory.
         * This will tell Steam to "end" the session when Playnite closes.
         */
        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            logger.Info("playnite exiting, marking all games as exited");
            fileSystem.EmptyDirectory(gameStateTrackingDir);
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            logger.Info("library updated, fetching list of games");

            string playniteExePath = playniteProcess.GetExePath();
            string playniteInstallDir = playniteProcess.GetInstallPath();

            // Get all visible games.
            IEnumerable<Game> games = PlayniteApi.Database.Games
                .Where((game) => !game.Hidden);

            logger.Info($"{games.Count()} games found");

            // Group the games into their respective libraries.
            Dictionary<LibraryPlugin, List<SteamRomManagerManifestEntry>> mappings = games
                .GroupBy(game => GetLibraryPlugin(game))
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(
                        game => new SteamRomManagerManifestEntry(
                            new SteamRomManagerManifestEntryArgs
                            {
                                // TODO: Pass a script as the target, stored in the data directory.
                                // This script spawns the Playnite process, watches the underlying process that Playnite creates and waits for it to end.
                                // Once it has ended, the script process terminates, informing Steam that the game or install process has now closed.
                                LaunchOptions = $"playnite://{URI_HANDLER_PATH}/{game.Id}",
                                StartIn = playniteInstallDir,
                                Target = playniteExePath,
                                Title = game.Name
                            }
                         )
                    ).ToList()
                );

            logger.Info($"cleaning data directory: {librariesDataDir}");

            fileSystem.EmptyDirectory(librariesDataDir);

            logger.Info($"writing manifest files to {librariesDataDir} for {mappings.Count()} libraries");

            mappings.ForEach(
                (mapping) =>
                {
                    logger.Info($"writing manifest for library {mapping.Key.Name}");

                    LibraryPlugin library = mapping.Key;
                    List<SteamRomManagerManifestEntry> manifest = mapping.Value;

                    string path = Path.Combine(librariesDataDir, library.Name, "manifest.json");

                    fileSystem.WriteJson(path, manifest);

                    logger.Info($"manifest.json file written to: {path}");
                }
            );

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

        private LibraryPlugin GetLibraryPlugin(Game Game)
        {
            return PlayniteApi.Addons.Plugins.Find(
                (plugin) => plugin.Id == Game.PluginId
            ) as LibraryPlugin;
        }
    }
}
