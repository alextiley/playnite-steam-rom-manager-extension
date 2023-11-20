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
        EaAppLibrary,
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

        public static Dictionary<Guid, (Library, string)> LibraryDictionary { get; } = new Dictionary<Guid, (Library, string)>
        {
            { Guid.Parse("402674cd-4af6-4886-b6ec-0e695bfa0688"), (Library.AmazonGamesLibrary, "Amazon Games") },
            { Guid.Parse("E3C26A3D-D695-4CB7-A769-5FF7612C7EDD"), (Library.BattleNetLibrary, "Battle.net") },
            { Guid.Parse("d0217e44-0df5-45f7-8515-478bdf21a883"), (Library.BattlestateGamesLibrary, "Battlestate Games") },
            { Guid.Parse("85DD7072-2F20-4E76-A007-41035E390724"), (Library.EaAppLibrary, "EA Games") },
            { Guid.Parse("00000002-DBD1-46C6-B5D0-B1BA559D10E4"), (Library.EpicLibrary, "Epic Games") },
            { Guid.Parse("AEBE8B7C-6DC3-4A66-AF31-E7375C6B5E9E"), (Library.GogLibrary, "GOG") },
            { Guid.Parse("f7da6eb0-17d7-497c-92fd-347050914954"), (Library.IndieGalaLibrary, "IndieGala") },
            { Guid.Parse("00000001-EBB2-4EEC-ABCB-7C89937A42BB"), (Library.ItchioLibrary, "Itch.io") },
            { Guid.Parse("34c3178f-6e1d-4e27-8885-99d4f031b168"), (Library.LegacyGamesLibrary, "Legacy Games") },
            { Guid.Parse("317a5e2e-eac1-48bc-adb3-fb9e321afd3f"), (Library.RiotGamesLibrary, "Riot Games") },
            { Guid.Parse("88409022-088a-4de8-805a-fdbac291f00a"), (Library.RockstarGamesLibrary, "Rockstar Games") },
            { Guid.Parse("CB91DFC9-B977-43BF-8E70-55F46E410FAB"), (Library.SteamLibrary, "Steam") },
            { Guid.Parse("C2F038E5-8B92-4877-91F1-DA9094155FC5"), (Library.UbisoftConnectLibrary, "Ubisoft") },
            { Guid.Parse("7e4fbb5e-2ae3-48d4-8ba0-6b30e7a4e287"), (Library.XboxLibrary, "Xbox") },
        };

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

        public IEnumerable<(Guid, IEnumerable<Game>, string)> GenerateLibraryCacheStatuses(IEnumerable<Game> nonSteamGames)
        {
            return nonSteamGames
                .Select(x => x.PluginId)
                .Distinct()
                .Select(pluginId =>
                {
                    var gamesInLibrary = nonSteamGames.Where(g => g.PluginId == pluginId);
                    var prev = ReadLibrarySyncCache(pluginId.ToString());
                    var next = string.Join(",", gamesInLibrary.Select(x => x.Id).OrderBy(x => x));

                    return (pluginId, gamesInLibrary, isSyncRequired: prev != next, next);
                })
                .Where(x => x.isSyncRequired)
                .Select(x =>
                {
                    return (guid: x.pluginId, games: x.gamesInLibrary, cacheValue: x.next);
                });
        }

        public void SaveGameActiveState(Game game)
        {
            var stateDataDir = filesystemHelper.stateDataDir;
            var destination = Path.Combine(stateDataDir, "track", game.Id.ToString());
            filesystemHelper.WriteFile(destination, "");
        }

        public void DeleteGameActiveState()
        {
            var stateDataDir = filesystemHelper.stateDataDir;
            var destination = Path.Combine(stateDataDir, "track");
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

        public string ReadLibrarySyncCache(string key)
        {
            return filesystemHelper.ReadFile(Path.Combine(GetSyncCachePath(), key));
        }

        public void WriteLibrarySyncCache(string key, string value)
        {
            filesystemHelper.WriteFile(Path.Combine(GetSyncCachePath(), key), value);
        }

        private string GetSyncCachePath()
        {
            return Path.Combine(filesystemHelper.stateDataDir, "cache", "sync");
        }
    }
}
