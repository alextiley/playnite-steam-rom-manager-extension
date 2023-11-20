using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
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

        public async Task<bool> ConfigureSync(IEnumerable<string> configIds)
        {
            var timeout = 120 * 1000; // 2 minutes max execution
            var args = $"enable {string.Join(" ", configIds)}";
            var workingDir = filesystemHelper.binariesDataDir;
            var result = await ProcessHelper.RunCommand(timeout, binaryFilename, args, workingDir);

            return result;
        }

        public async Task<bool> StartSync()
        {
            var timeout = 60 * 1000 * 15; // 15 minutes max execution
            var args = "add";
            var workingDir = filesystemHelper.binariesDataDir;
            var result = await ProcessHelper.RunCommand(timeout, binaryFilename, args, workingDir);

            return result;
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

        public SteamRomManagerParserConfig CreateUserConfiguration(Guid guid, string title)
        {
            return new SteamRomManagerParserConfig
            {
                ParserType = "Manual",
                ConfigTitle = $"Playnite - {title}",
                SteamDirectory = "${steamDirGlobal}",
                SteamCategory = $"${{{title}}}",
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
                ParserId = guid.ToString(),
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
