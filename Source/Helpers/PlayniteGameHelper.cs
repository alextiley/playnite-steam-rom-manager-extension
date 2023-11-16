using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System.Linq;

namespace SteamRomManagerCompanion
{
    public class PlayniteGameHelperArgs
    {
        public IPlayniteAPI api { get; set; }
    }

    internal class PlayniteGameHelper
    {
        private readonly IPlayniteAPI api;

        public PlayniteGameHelper(PlayniteGameHelperArgs args)
        {
            api = args.api;
        }

        public LibraryPlugin GetLibraryPlugin(Game g)
        {
            return (LibraryPlugin)api.Addons.Plugins.Where(x => x.Id == g.PluginId).First();
        }
    }
}
