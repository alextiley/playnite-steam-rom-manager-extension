using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Timers;

namespace SteamRomManagerCompanion
{
    public delegate void GameDelegate(Game game);

    internal class RegisterArgs
    {
        public GameDelegate OnPostLaunchGame { get; set; }
        public GameDelegate OnInstallAbort { get; set; }
        public IPlayniteAPI PlayniteApi { get; set; }
    }

    internal class LaunchGameUriHandler
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly IPlayniteAPI PlayniteApi;

        public const string path = "steam-launcher";

        public LaunchGameUriHandler(IPlayniteAPI api)
        {
            PlayniteApi = api;
        }

        public void Register(RegisterArgs args)
        {
            var onPostLaunchGame = args.OnPostLaunchGame;
            var onInstallAbort = args.OnInstallAbort;

            logger.Info($"registering handler 'playnite://{path}/.*'");

            PlayniteApi.UriHandler.RegisterSource(path, (handlerArgs) =>
            {
                logger.Info($"handler 'playnite://{path}/.*' invoked");

                var id = handlerArgs.Arguments[0];
                if (id == null)
                {
                    logger.Error("no argument provided to handler, exiting");
                    return;
                }

                var parsedGuid = ParseGameIdFromEventArgs(id);
                if (parsedGuid == null)
                {
                    logger.Error($"uri handler argument validation failed for id: {id}");
                    return;
                }

                var guid = (Guid)parsedGuid;
                var game = PlayniteApi.Database.Games.Get(guid);
                if (game == null)
                {
                    logger.Error($"unable to find game with id: {guid}");
                    return;
                }

                if (game.IsInstalled)
                {
                    logger.Info($"launching game: {game.Name}");
                    PlayniteApi.StartGame(guid);
                }
                else
                {
                    logger.Info($"installing game: {game.Name}");

                    PlayniteApi.InstallGame(guid);

                    var installTimer = new Timer { Interval = 20000 };
                    installTimer.Elapsed += (sender, e) => CheckInstallationState(installTimer, guid, onInstallAbort);
                    installTimer.Start();
                }

                onPostLaunchGame?.Invoke(game);
            });
        }

        private void CheckInstallationState(Timer timer, Guid guid, GameDelegate onInstallAbort)
        {
            var game = PlayniteApi.Database.Games.Get(guid);

            if (game == null)
            {
                logger.Error($"Unable to find game with ID: {guid}");
                timer.Stop();
                timer.Dispose();
                return;
            }

            if (!IsGameInstallerRunning(game))
            {
                onInstallAbort?.Invoke(game);
                timer.Stop();
                timer.Dispose();
            }
        }

        private bool IsGameInstallerRunning(Game game)
        {
            // Check processes associated to library
            return true;
        }

        private Guid? ParseGameIdFromEventArgs(string id)
        {
            try
            {
                return Guid.Parse(id);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
