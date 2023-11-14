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
    public class SteamRomManagerManifestJson
    {
        public SteamRomManagerManifestJson(
            string title,
            string target,
            string startIn,
            string launchOptions
        )
        {
            StartIn = startIn;
            Title = title;
            Target = target;
            LaunchOptions = launchOptions;
        }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("target")]
        public string Target { get; set; }

        [JsonProperty("startIn")]
        public string StartIn { get; set; }

        [JsonProperty("launchOptions")]
        public string LaunchOptions { get; set; }
    }

    public class SteamRomManagerCompanion : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly string playniteExe;
        private readonly string playniteDir;
        private readonly string dataDir;

        private SteamRomManagerCompanionSettingsViewModel settings { get; set; }

        public override Guid Id { get; } = Guid.Parse("5fe1d136-a9dc-44d7-80d2-43c02df6e546");

        public SteamRomManagerCompanion(IPlayniteAPI api) : base(api)
        {
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
            // Add code to be executed when game is finished installing.
        }

        public override void OnGameStarted(OnGameStartedEventArgs args)
        {
            // Add code to be executed when game is started running.
        }

        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
            // Add code to be executed when game is preparing to be started.
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            // Add code to be executed when game is preparing to be started.
        }

        public override void OnGameUninstalled(OnGameUninstalledEventArgs args)
        {
            // Add code to be executed when game is uninstalled.
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            // Add code to be executed when Playnite is initialized.
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            // Add code to be executed when Playnite is shutting down.
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
            Dictionary<LibraryPlugin, List<SteamRomManagerManifestJson>> mappings = games
                .GroupBy(game => GetLibraryPlugin(game))
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(
                        game => new SteamRomManagerManifestJson(
                            game.Name,
                            playniteExe,
                            playniteDir,
                            $"playnite://install-or-start/{game.Id}"
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

        private void WriteManifestJson(KeyValuePair<LibraryPlugin, List<SteamRomManagerManifestJson>> mapping)
        {
            LibraryPlugin library = mapping.Key;
            List<SteamRomManagerManifestJson> manifest = mapping.Value;
            string libraryDir = Path.Combine(dataDir, library.Name);

            _ = Directory.CreateDirectory(libraryDir);

            // Write manifest.json file
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
    }
}
