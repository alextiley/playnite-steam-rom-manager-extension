using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Path = System.IO.Path;

namespace SteamRomManagerCompanion
{
    public delegate LibraryPlugin LibraryPluginFilterFn(Game game);

    public class BuildManifestGroupsArgs
    {
        public IEnumerable<Game> Games { get; set; }
        public string LaunchOptions { get; set; }
        public string StartIn { get; set; }
        public string Target { get; set; }
        public LibraryPluginFilterFn LibraryPluginFilterFn { get; set; }
    }

    internal class SteamRomManagerArgs
    {
        public string Source { get; set; }
        public string Filename { get; set; }
    }

    internal class SteamRomManager
    {
        private readonly string uri;
        private readonly string filename;

        private static readonly ILogger logger = LogManager.GetLogger();

        public SteamRomManager(SteamRomManagerArgs args)
        {
            uri = args.Source;
            filename = args.Filename;
        }

        public bool IsBinaryAvailable()
        {
            return File.Exists(filename);
        }

        public async Task<bool> DownloadBinary(bool force = false)
        {
            if (IsBinaryAvailable() && !force)
            {
                logger.Info($"steam rom manager already installed to {filename}");
                return true;
            }
            var client = new HttpClient();
            try
            {
                var bytes = await client.GetByteArrayAsync(uri);
                _ = Directory.CreateDirectory(Path.GetDirectoryName(filename));
                File.WriteAllBytes(filename, bytes);

                logger.Info($"steam rom manager installed to {filename}");

                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "error when downloading steam rom manager");
                return false;
            }
        }

        public Dictionary<LibraryPlugin, List<SteamRomManagerManifestEntry>> BuildManifestGroups(BuildManifestGroupsArgs args)
        {
            var grouped = args.Games.GroupBy(
                game => args.LibraryPluginFilterFn(game)
            );

            return grouped.ToDictionary(
                group => group.Key,
                group => group.Select(
                    game => new SteamRomManagerManifestEntry(
                        new SteamRomManagerManifestEntryArgs
                        {
                            LaunchOptions = args.LaunchOptions ?? "",
                            StartIn = args.StartIn,
                            Target = args.Target.Replace("{id}", game.Id.ToString()),
                            Title = game.Name
                        }
                    )
                ).ToList()
            );
        }
    }
}
