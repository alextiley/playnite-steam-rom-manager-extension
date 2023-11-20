using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Path = System.IO.Path;

namespace SteamRomManagerCompanion
{
    public delegate (Guid, string) GroupBySelectorFunc(Game game);

    internal class SteamRomManagerHelperArgs
    {
        public string SteamInstallDir { get; set; }
        public string SteamActiveUsername { get; set; }
        public string BinarySourceUri { get; set; }
        public string BinaryDestinationFilename { get; set; }
        public FilesystemHelper FilesystemHelper { get; set; }
    }

    internal class SteamRomManagerHelper
    {
        private const string configDirectory = "userData";

        private readonly string binarySourceUri;
        private readonly string binaryFilename;
        private readonly string binaryPath;
        private readonly string steamActiveUsername;
        private readonly string steamInstallDir;

        private readonly FilesystemHelper filesystemHelper;

        private static readonly ILogger logger = LogManager.GetLogger();

        public SteamRomManagerHelper(SteamRomManagerHelperArgs args)
        {
            filesystemHelper = args.FilesystemHelper;
            binarySourceUri = args.BinarySourceUri;
            steamInstallDir = args.SteamInstallDir;
            binaryPath = Path.Combine(args.FilesystemHelper.binariesDataDir, args.BinaryDestinationFilename);
            binaryFilename = args.BinaryDestinationFilename;
            steamActiveUsername = args.SteamActiveUsername;
        }

        public bool IsBinaryDownloaded()
        {
            return File.Exists(binaryPath);
        }

        public async Task<bool> ConfigureSync()
        {
            logger.Info("enabling all libraries in steam rom manager");

            var enableProcess = new Process();
            var enableProcessHandled = new TaskCompletionSource<bool>();

            enableProcess.StartInfo.FileName = binaryFilename;
            enableProcess.StartInfo.Arguments = "enable --all";
            enableProcess.StartInfo.WorkingDirectory = filesystemHelper.binariesDataDir;
            enableProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            enableProcess.StartInfo.CreateNoWindow = true;
            enableProcess.EnableRaisingEvents = true;
            enableProcess.Exited += new EventHandler(
                (object sender, EventArgs e) => enableProcessHandled.TrySetResult(true)
            );
            _ = enableProcess.Start();

            // Wait no longer than 60 seconds
            _ = await Task.WhenAny(enableProcessHandled.Task, Task.Delay(60 * 1000));

            logger.Info("libraries enabled in steam rom manager");

            return true;
        }

        public async Task<bool> StartSync()
        {
            logger.Info("libraries enabled, adding games to steam");

            var addProcess = new Process();
            var addProcessHandled = new TaskCompletionSource<bool>();

            addProcess.StartInfo.FileName = binaryFilename;
            addProcess.StartInfo.Arguments = "add";
            addProcess.StartInfo.WorkingDirectory = filesystemHelper.binariesDataDir;
            addProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            addProcess.StartInfo.CreateNoWindow = true;
            addProcess.EnableRaisingEvents = true;
            addProcess.Exited += new EventHandler(
                (object sender, EventArgs e) => addProcessHandled.TrySetResult(true)
            );
            _ = addProcess.Start();

            // Wait no longer than 15 minutes
            _ = await Task.WhenAny(addProcessHandled.Task, Task.Delay(60 * 1000 * 15));

            logger.Info("games added successfully");

            return true;
        }

        public async Task<bool> Initialise()
        {
            if (!await DownloadBinary())
            {
                return false;
            }
            WriteUserSettings(CreateUserSettings());
            return true;
        }

        public SteamRomManagerUserSettings CreateUserSettings()
        {
            return new SteamRomManagerUserSettings
            {
                FuzzyMatcher = new FuzzyMatcher
                {
                    Timestamps = new Timestamps { Check = 0, Download = 0 },
                    Verbose = false,
                    FilterProviders = true,
                },
                EnvironmentVariables = new EnvironmentVariables
                {
                    SteamDirectory = steamInstallDir,
                    UserAccounts = $"${{{steamActiveUsername}}}",
                    LocalImagesDirectory = "",
                    RomsDirectory = "",
                    RetroarchPath = "",
                    RaCoresDirectory = "",
                },
                PreviewSettings = new PreviewSettings
                {
                    RetrieveCurrentSteamImages = true,
                    DeleteDisabledShortcuts = false,
                    ImageZoomPercentage = 30,
                    Preload = false,
                },
                EnabledProviders = new[] { "SteamGridDB" },
                BatchDownloadSize = 50,
                Language = "en-US",
                Theme = "Deck",
                OfflineMode = false,
                NavigationWidth = 311,
                ClearLogOnTest = true,
                Version = 6
            };
        }

        public SteamRomManagerParserConfig CreateUserConfiguration(Guid guid, string name)
        {
            return new SteamRomManagerParserConfig
            {
                ParserType = "Manual",
                ConfigTitle = $"Playnite - {name}",
                SteamDirectory = "${steamDirGlobal}",
                SteamCategory = $"${{{name}}}",
                RomDirectory = "",
                ExecutableArgs = "",
                ExecutableModifier = "\"${exePath}\"",
                StartInDirectory = "",
                TitleModifier = "${fuzzyTitle}",
                FetchControllerTemplatesButton = null,
                RemoveControllersButton = null,
                ImageProviders = new[] { "SteamGridDB" },
                OnlineImageQueries = "${${fuzzyTitle}}",
                ImagePool = "${fuzzyTitle}",
                UserAccounts = new UserAccounts
                {
                    SpecifiedAccounts = $"${{${{accountsglobal}}}}",
                },
                Executable = new Executable
                {
                    Path = "",
                    ShortcutPassthrough = false,
                    AppendArgsToExecutable = true,
                },
                ParserInputs = new ParserInputs
                {
                    ManualManifests = Path.Combine(filesystemHelper.manifestsDataDir, $"{guid}")
                },
                TitleFromVariable = new TitleFromVariable
                {
                    LimitToGroups = "",
                    CaseInsensitiveVariables = false,
                    SkipFileIfVariableWasNotFound = false,
                    TryToMatchTitle = false
                },
                FuzzyMatch = new FuzzyMatch
                {
                    ReplaceDiacritics = true,
                    RemoveCharacters = true,
                    RemoveBrackets = true,
                },
                Controllers = new Controllers
                {
                    PS4 = new Controller { Title = "Gamepad", ProfileType = "template", MappingId = "controller_ps4_gamepad_joystick.vdf" },
                    PS5 = new Controller { Title = "Gamepad", ProfileType = "template", MappingId = "controller_ps5_gamepad_joystick.vdf" },
                    Xbox360 = new Controller { Title = "Gamepad", ProfileType = "template", MappingId = "controller_xbox360_gamepad_joystick.vdf" },
                    XboxOne = new Controller { Title = "Gamepad", ProfileType = "template", MappingId = "controller_xboxone_gamepad_joystick.vdf" },
                    SwitchJoyconLeft = new Controller { Title = "Gamepad", ProfileType = "template", MappingId = "controller_switch_joycon_left_gamepad_joystick.vdf" },
                    SwitchJoyconRight = new Controller { Title = "Gamepad", ProfileType = "template", MappingId = "controller_switch_joycon_right_gamepad_joystick.vdf" },
                    SwitchPro = new Controller { Title = "Gamepad", ProfileType = "template", MappingId = "controller_switch_pro_gamepad_joystick.vdf" },
                    Neptune = new Controller { Title = "Gamepad", ProfileType = "template", MappingId = "controller_neptune_gamepad_joystick.vdf" },
                },
                ImageProviderAPIs = new ImageProviderAPIs
                {
                    SteamGridDB = new SteamGridDB
                    {
                        Nsfw = false,
                        Humor = false,
                        Styles = new[] { "alternate", "blurred", "white_logo", "material" },
                        StylesHero = new[] { "blurred", "alternate", "material" },
                        StylesLogo = new[] { "official", "white", "black" },
                        StylesIcon = new[] { "official", "custom" },
                        ImageMotionTypes = new[] { "static" }
                    }
                },
                DefaultImage = new Image
                {
                    Tall = null,
                    Long = null,
                    Hero = null,
                    Logo = null,
                    Icon = null,
                },
                LocalImages = new Image
                {
                    Tall = null,
                    Long = null,
                    Hero = null,
                    Logo = null,
                    Icon = null,
                },
                ParserId = $"GeneratedByPlayniteSteamRomManagerCompanion_{guid}",
                Disabled = false,
                Version = 15
            };
        }

        public void DeleteManifests()
        {
            filesystemHelper.DeleteDirectoryContents(filesystemHelper.manifestsDataDir);
        }

        public void WriteUserConfigurations(IEnumerable<SteamRomManagerParserConfig> configs)
        {
            WriteJsonToConfigDir("userConfigurations.json", configs);
        }

        public void WriteUserSettings(SteamRomManagerUserSettings config)
        {
            WriteJsonToConfigDir("userSettings.json", config);
        }

        private void WriteJsonToConfigDir(string filename, object contents)
        {
            var path = Path.Combine(filesystemHelper.binariesDataDir, configDirectory, filename);
            filesystemHelper.WriteJson(path, contents);
        }

        private async Task<bool> DownloadBinary(bool force = false)
        {
            logger.Info($"attempting to download steam rom manager binary from {binarySourceUri}");

            if (IsBinaryDownloaded() && !force)
            {
                logger.Info($"install skipped, steam rom manager already exists at {binaryPath}");
                return true;
            }
            var client = new HttpClient();
            try
            {
                filesystemHelper.WriteBinary(binaryPath, await client.GetByteArrayAsync(binarySourceUri));
                logger.Info($"install succeeded, steam rom manager ready at {binaryPath}");
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "error, unable to download steam rom manager");
                return false;
            }
        }
    }
}
