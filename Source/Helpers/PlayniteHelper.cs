using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SteamRomManagerCompanion
{
    internal enum Library
    {
        AmazonGamesLibrary,
        BattleNetLibrary,
        BattlestateGamesLibrary,
        BethesdaLibrary,
        ElectronicArtsLibrary,
        EpicLibrary,
        GogLibrary,
        IndieGalaLibrary,
        ItchioLibrary,
        LegacyGamesLibrary,
        RiotGamesLibrary,
        RockstarGamesLibrary,
        SteamLibrary,
        UbisoftConnectLibrary,
        UnknownLibrary,
        XboxLibrary,
    }

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

        public IEnumerable<Game> GetVisibleNonSteamGames()
        {
            return api.Database.Games
                .Where((g) => !g.Hidden)
                .Where(g => !GetLibraryPlugin(g).Name.ToLower().Contains("steam"));
        }

        public LibraryPlugin GetLibraryPlugin(Game g)
        {
            return (LibraryPlugin)api.Addons.Plugins.Where(x => x.Id == g.PluginId).Single();
        }

        public Library GetLibraryFromPluginId(Guid pluginId)
        {
            // TODO: Create mapping of plugins -> Steam category names
            switch (pluginId.ToString())
            {
                case "402674cd-4af6-4886-b6ec-0e695bfa0688":
                    return Library.AmazonGamesLibrary;
                case "e3c26a3d-d695-4cb7-a769-5ff7612c7edd":
                    return Library.BattleNetLibrary;
                case "d0217e44-0df5-45f7-8515-478bdf21a883":
                    return Library.BattlestateGamesLibrary;
                case "85dd7072-2f20-4e76-a007-41035e390724":
                    return Library.ElectronicArtsLibrary;
                case "00000002-dbd1-46c6-b5d0-b1ba559d10e4":
                    return Library.EpicLibrary;
                case "aebe8b7c-6dc3-4a66-af31-e7375c6b5e9e":
                    return Library.GogLibrary;
                case "f7da6eb0-17d7-497c-92fd-347050914954":
                    return Library.IndieGalaLibrary;
                case "00000001-ebb2-4eec-abcb-7c89937a42bb":
                    return Library.ItchioLibrary;
                case "34c3178f-6e1d-4e27-8885-99d4f031b168":
                    return Library.LegacyGamesLibrary;
                case "317a5e2e-eac1-48bc-adb3-fb9e321afd3f":
                    return Library.RiotGamesLibrary;
                case "88409022-088a-4de8-805a-fdbac291f00a":
                    return Library.RockstarGamesLibrary;
                case "cb91dfc9-b977-43bf-8e70-55f46e410fab":
                    return Library.SteamLibrary;
                case "c2f038e5-8b92-4877-91f1-da9094155fc5":
                    return Library.UbisoftConnectLibrary;
                case "7e4fbb5e-2ae3-48d4-8ba0-6b30e7a4e287":
                    return Library.XboxLibrary;
                default:
                    return Library.UnknownLibrary;
            }
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
            return api.Paths.ApplicationPath;
        }
    }
}
