using Playnite.SDK;
using Playnite.SDK.Models;
using System;

namespace SteamRomManagerCompanion
{
    internal interface IUriHandler
    {
        void Register(string path);
        void Unregister(string path);
    }

    internal class SteamRomManagerUriHandler : IUriHandler
    {
        private readonly IPlayniteAPI PlayniteApi;

        private static readonly ILogger logger = LogManager.GetLogger();

        public SteamRomManagerUriHandler(IPlayniteAPI PlayniteApi)
        {
            this.PlayniteApi = PlayniteApi;
        }

        public void Register(string path)
        {
            PlayniteApi.UriHandler.RegisterSource(path, (args) =>
            {
                string id = args.Arguments[0];
                Guid? guid = ParseGameIdFromEventArgs(id);
                if (guid == null)
                {
                    logger.Error($"uri handler argument validation failed for id: {id}");
                    return;
                }

                Game game = PlayniteApi.Database.Games.Get((Guid)guid);
                if (game == null)
                {
                    logger.Error($"unable to find game with id: {guid}");
                    return;
                }

                if (game.IsInstalled)
                {
                    logger.Info($"launching game: {game.Name}");
                    PlayniteApi.StartGame((Guid)guid);
                }
                else
                {
                    logger.Info($"installing game: {game.Name}");
                    PlayniteApi.InstallGame((Guid)guid);
                }
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
