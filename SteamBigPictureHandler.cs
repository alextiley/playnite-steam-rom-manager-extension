using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Newtonsoft.Json;
using System.Runtime.InteropServices.ComTypes;
using System.IO;

namespace SteamBigPictureHandler
{
    public class SteamRomManagerManifestJson
    {
        public SteamRomManagerManifestJson(
            String title,
            String target,
            String startIn,
            String launchOptions
        ) {
            this.StartIn = startIn;
            this.Title = title;
            this.Target = target;
            this.LaunchOptions = launchOptions;
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

    public class SteamBigPictureHandler : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private string playniteExe;
        private string playniteDir;
        private string dataDir;

        private SteamBigPictureHandlerSettingsViewModel settings { get; set; }

        public override Guid Id { get; } = Guid.Parse("5fe1d136-a9dc-44d7-80d2-43c02df6e546");

        public SteamBigPictureHandler(IPlayniteAPI api) : base(api)
        {
            settings = new SteamBigPictureHandlerSettingsViewModel(this);
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };

            dataDir = this.GetPluginUserDataPath();
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
            logger.Info($"process path is {this.playniteExe}");
            logger.Info($"install path is {this.playniteDir}");
            logger.Info($"extension data path is {this.dataDir}");

            // Get all visible games.
            var games = this.PlayniteApi.Database.Games
                .Where((game) => !game.Hidden);

            logger.Info($"{games.Count()} games found");

            // Group the games into their respective libraries.
            var mappings = games
                .GroupBy(game => GetLibraryPlugin(game))
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(
                        game => new SteamRomManagerManifestJson(
                            game.Name,
                            this.playniteExe,
                            this.playniteDir,
                            $"playnite://install-or-start/{game.Id}"
                         )
                    ).ToList()
                );

            logger.Info($"{mappings.Count()} libraries found");

            logger.Info($"writing manifest files to {this.dataDir}");

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
            return new SteamBigPictureHandlerSettingsView();
        }

        private LibraryPlugin GetLibraryPlugin(Game Game)
        {
            return this.PlayniteApi.Addons.Plugins.Find(
                (plugin) => plugin.Id == Game.PluginId
            ) as LibraryPlugin;
        }

        private void WriteManifestJson(KeyValuePair<LibraryPlugin, List<SteamRomManagerManifestJson>> mapping)
        {
            var library = mapping.Key;
            var manifest = mapping.Value;
            var libraryDir = Path.Combine(this.dataDir, library.Name);

            // Create subdirectory or clear existing one
            if (!Directory.Exists(libraryDir))
            {
                Directory.CreateDirectory(libraryDir);
            }
            else
            {
                CleanDirectory(libraryDir);
            }

            // Write manifest.json file
            var manifestPath = Path.Combine(libraryDir, "manifest.json");
            var manifestJson = JsonConvert.SerializeObject(manifest, Formatting.None);
            
            File.WriteAllText(manifestPath, manifestJson, Encoding.UTF8);

            logger.Info($"manifest.json file written to: {manifestPath}");
        }

        private void CleanDirectory(String dir)
        {
            foreach (var file in Directory.GetFiles(dir))
            {
                File.Delete(file);
            }
            foreach (var subdir in Directory.GetDirectories(dir))
            {
                Directory.Delete(subdir, true);
            }
        }
    }
}
