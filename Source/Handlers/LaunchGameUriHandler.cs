using Playnite.SDK;
using Playnite.SDK.Models;
using SteamRomManagerCompanion.Interfaces;
using System;

namespace SteamRomManagerCompanion.Handlers
{
    internal class LaunchGameUriHandlerArgs
    {
        public IPlayniteAPI PlayniteAPI { get; set; }
        public IGameStateTracker gameStateTracker { get; set; }
    }

    internal class LaunchGameUriHandler : IUriHandler
    {
        private readonly IPlayniteAPI PlayniteApi;
        private readonly IGameStateTracker gameStateTracker;

        private static readonly ILogger logger = LogManager.GetLogger();

        public LaunchGameUriHandler(LaunchGameUriHandlerArgs args)
        {
            PlayniteApi = args.PlayniteAPI;
            gameStateTracker = args.gameStateTracker;
        }

        public void Register(string path)
        {
            logger.Info($"registering handler 'playnite://{path}/.*'");

            PlayniteApi.UriHandler.RegisterSource(path, (args) =>
            {
                logger.Info($"handler 'playnite://{path}/.*' invoked");

                string id = args.Arguments[0];
                Guid? parsedGuid = ParseGameIdFromEventArgs(id);
                if (parsedGuid == null)
                {
                    logger.Error($"uri handler argument validation failed for id: {id}");
                    return;
                }

                Guid guid = (Guid)parsedGuid;
                Game game = PlayniteApi.Database.Games.Get(guid);
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
                }

                logger.Info($"game initialised, marking as started: {guid}");
                gameStateTracker.Start(guid);
            });
        }

        public void Unregister(string path)
        {
            PlayniteApi.UriHandler.RemoveSource(path);
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
