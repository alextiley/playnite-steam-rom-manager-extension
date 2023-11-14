using Newtonsoft.Json;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Controls;

namespace SteamRomManagerCompanion
{
    public class SteamRomManagerCompanion : GenericPlugin
    {
        private const string URI_HANDLER_PATH = "go";

        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly string playniteExe;
        private readonly string playniteDir;
        private readonly string dataDir;

        private readonly IUriHandler uriHandler;

        private SteamRomManagerCompanionSettingsViewModel settings { get; set; }

        public override Guid Id { get; } = Guid.Parse("5fe1d136-a9dc-44d7-80d2-43c02df6e546");

        public SteamRomManagerCompanion(IPlayniteAPI api) : base(api)
        {
            uriHandler = new SteamRomManagerUriHandler(PlayniteApi);
            settings = new SteamRomManagerCompanionSettingsViewModel(this);
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };

            dataDir = GetPluginUserDataPath();
            playniteExe = Process.GetCurrentProcess().MainModule.FileName;
            playniteDir = Path.GetDirectoryName(playniteExe);
        }

        public override void OnGameInstalled(OnGameInstalledEventArgs args)
        {
            // Get the current process and attempt to restart it.
            // This is a hack to trick steam into thinking that the process finished!
            RestartPlaynite();
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            // Get the current process and attempt to restart it.
            // This is a hack to trick steam into thinking that the Playnite process has finished!
            RestartPlaynite();
        }


        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            logger.Info("registering playnite://go/{id} uri handler");
            uriHandler.Register(URI_HANDLER_PATH);
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            logger.Info("library updated, fetching list of games");

            logger.Info($"process path is {playniteExe}");
            logger.Info($"install path is {playniteDir}");
            logger.Info($"extension data path is {dataDir}");

            // Get all visible games.
            IEnumerable<Game> games = PlayniteApi.Database.Games
                .Where((game) => !game.Hidden);

            logger.Info($"{games.Count()} games found");

            // Group the games into their respective libraries.
            Dictionary<LibraryPlugin, List<SteamRomManagerManifest>> mappings = games
                .GroupBy(game => GetLibraryPlugin(game))
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(
                        game => new SteamRomManagerManifest(
                            game.Name,
                            playniteExe,
                            playniteDir,
                            $"playnite://go/{game.Id}"
                         )
                    ).ToList()
                );

            CleanDirectory(dataDir);

            logger.Info($"writing manifest files to {dataDir} for {mappings.Count()} libraries");

            mappings.ForEach(
                (mapping) =>
                {
                    logger.Info($"writing manifest for library {mapping.Key.Name}");
                    WriteManifestJson(mapping);
                    logger.Info($"manifest successfully written for library {mapping.Key.Name}");
                }
            );

            // TODO: Download SRM binary.

            // TODO: Generate SRM parser configuration JSON for each manifest.

            // TODO: Find Steam executable and kill it if running.

            // TODO: Run SRM for each manifest and parser configuration combination.

            // TODO: Restart Steam if it was previously running.
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new SteamRomManagerCompanionSettingsView();
        }

        private LibraryPlugin GetLibraryPlugin(Game Game)
        {
            return PlayniteApi.Addons.Plugins.Find(
                (plugin) => plugin.Id == Game.PluginId
            ) as LibraryPlugin;
        }

        private void WriteManifestJson(KeyValuePair<LibraryPlugin, List<SteamRomManagerManifest>> mapping)
        {
            LibraryPlugin library = mapping.Key;
            List<SteamRomManagerManifest> manifest = mapping.Value;
            string libraryDir = Path.Combine(dataDir, library.Name);

            _ = Directory.CreateDirectory(libraryDir);

            string manifestPath = Path.Combine(libraryDir, "manifest.json");
            string manifestJson = JsonConvert.SerializeObject(manifest, Formatting.None);

            File.WriteAllText(manifestPath, manifestJson, Encoding.UTF8);

            logger.Info($"manifest.json file written to: {manifestPath}");
        }

        private void CleanDirectory(string dir)
        {
            logger.Info($"cleaning data directory: {dir}");

            foreach (string file in Directory.GetFiles(dir))
            {
                File.Delete(file);
            }
            foreach (string subdir in Directory.GetDirectories(dir))
            {
                Directory.Delete(subdir, true);
            }
            logger.Info("data directory successfully cleaned");
        }

        private void RestartPlaynite()
        {
            Process CurrentProcess = Process.GetCurrentProcess();

            // Hack, timeout doesn't work so this is the next best thing.
            ProcessStartInfo Info = new ProcessStartInfo
            {
                Arguments = "/C ping 127.0.0.1 -n 2 && \"" + CurrentProcess.MainModule.FileName + "\"",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                FileName = "cmd.exe"
            };
            _ = Process.Start(Info);

            CurrentProcess.Kill();
        }
    }
}
