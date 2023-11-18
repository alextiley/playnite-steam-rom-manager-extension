using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SteamRomManagerCompanion
{
    internal class PlayniteHelperArgs
    {
        public IPlayniteAPI api { get; set; }
        public FilesystemHelper filesystemHelper { get; set; }
    }

    internal class PlayniteHelper
    {
        private readonly IPlayniteAPI api;
        private readonly FilesystemHelper filesystemHelper;

        public PlayniteHelper(PlayniteHelperArgs args)
        {
            api = args.api;
            filesystemHelper = args.filesystemHelper;
        }

        public LibraryPlugin GetLibraryPlugin(Game g)
        {
            return (LibraryPlugin)api.Addons.Plugins.Where(x => x.Id == g.PluginId).First();
        }

        public void SaveGameActiveState(Game game)
        {
            var stateDataDir = filesystemHelper.stateDataDir;
            var destination = Path.Combine(stateDataDir, "active_game", game.Id.ToString());
            filesystemHelper.WriteFile(destination, "");
        }

        public void DeleteGameActiveState()
        {
            var stateDataDir = filesystemHelper.stateDataDir;
            var destination = Path.Combine(stateDataDir, "active_game");
            filesystemHelper.DeleteDirectoryContents(destination);
        }

        public string GetExecutablePath()
        {
            return Process.GetCurrentProcess().MainModule.FileName;
        }

        public string GetInstallPath()
        {
            return Path.GetDirectoryName(GetExecutablePath());
        }
    }
}
