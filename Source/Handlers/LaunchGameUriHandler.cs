using Playnite.SDK;
using System;

namespace SteamRomManagerCompanion
{
    internal class LaunchGameUriHandlerArgs
    {
        public IPlayniteAPI PlayniteAPI { get; set; }
    }

    internal class LaunchGameUriHandler
    {
        public bool WasTriggered { get; set; } = false;

        private readonly IPlayniteAPI PlayniteApi;

        private static readonly ILogger logger = LogManager.GetLogger();

        public LaunchGameUriHandler(LaunchGameUriHandlerArgs args)
        {
            PlayniteApi = args.PlayniteAPI;
        }

        public void Register(string path)
        {
            logger.Info($"registering handler 'playnite://{path}/.*'");

            PlayniteApi.UriHandler.RegisterSource(path, (args) =>
            {
                logger.Info($"handler 'playnite://{path}/.*' invoked");

                var id = args.Arguments[0];
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
                }
                WasTriggered = true;
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
