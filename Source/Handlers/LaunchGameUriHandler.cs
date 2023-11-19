using Playnite.SDK;
using Playnite.SDK.Models;
using System;

namespace SteamRomManagerCompanion
{
    public delegate void GameDelegate(Game game);

    internal class LaunchGameUriHandlerArgs
    {
        public IPlayniteAPI PlayniteApi { get; set; }
    }

    internal class LaunchGameUriHandlerRegisterArgs
    {
        public GameDelegate OnStartGame { get; set; }
    }

    internal class LaunchGameUriHandler
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly IPlayniteAPI PlayniteApi;

        public const string path = "steam-launcher";

        public LaunchGameUriHandler(LaunchGameUriHandlerArgs args)
        {
            PlayniteApi = args.PlayniteApi;
        }

        public void Register(LaunchGameUriHandlerRegisterArgs args)
        {
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
                    args.OnStartGame?.Invoke(game);
                }
                else
                {
                    logger.Info($"installing game: {game.Name}");
                    PlayniteApi.InstallGame(guid);
                }
            });
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
