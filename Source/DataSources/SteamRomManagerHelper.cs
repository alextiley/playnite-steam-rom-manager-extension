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
    public delegate LibraryPlugin GroupBySelectorFunc(Game game);

    public class CreateLibraryManifestDictionaryArgs
    {
        public IEnumerable<Game> Games { get; set; }
        public string LaunchOptions { get; set; }
        public string StartIn { get; set; }
        public string Target { get; set; }
        public GroupBySelectorFunc GroupBySelectorFunc { get; set; }
    }

    internal class SteamRomManagerHelperArgs
    {
        public string BinariesDataDir { get; set; }
        public string ManifestsDataDir { get; set; }
        public string SteamInstallDir { get; set; }
        public string SteamActiveUsername { get; set; }
        public string BinarySourceUri { get; set; }
        public string BinaryDestinationFilename { get; set; }
        public FilesystemHelper FileSystemHelper { get; set; }
    }

    internal class SteamRomManagerHelper
    {
        private const string configDirectory = "userData";

        private readonly string binarySourceUri;
        private readonly string binariesDir;
        private readonly string manifestsDir;
        private readonly string binaryPath;
        private readonly string steamActiveUsername;
        private readonly string steamInstallDir;

        private readonly FilesystemHelper fileSystemHelper;

        private static readonly ILogger logger = LogManager.GetLogger();

        public SteamRomManagerHelper(SteamRomManagerHelperArgs args)
        {
            binarySourceUri = args.BinarySourceUri;
            binariesDir = args.BinariesDataDir;
            manifestsDir = args.ManifestsDataDir;
            steamInstallDir = args.SteamInstallDir;
            binaryPath = Path.Combine(args.BinariesDataDir, args.BinaryDestinationFilename);
            steamActiveUsername = args.SteamActiveUsername;
            fileSystemHelper = args.FileSystemHelper;
        }

        public bool IsBinaryDownloaded()
        {
            return File.Exists(binaryPath);
        }

        public void ImportLibrary()
        {
            //
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

        public Dictionary<string, List<SteamRomManagerManifestEntry>> CreateLibraryManifestDict(CreateLibraryManifestDictionaryArgs args)
        {
            return args.Games
                .GroupBy(
                    game => args.GroupBySelectorFunc(game)
                )
                .ToDictionary(
                    group => group.Key.Name,
                    group => group.Select(
                        game => new SteamRomManagerManifestEntry(
                            new SteamRomManagerManifestEntryArgs
                            {
                                LaunchOptions = args.LaunchOptions ?? "",
                                StartIn = args.StartIn,
                                Target = args.Target.Replace("{id}", $"{game.Id}"),
                                Title = game.Name
                            }
                        )
                    ).ToList()
                );
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
                    DeleteDisabledShortcuts = true,
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

        public IEnumerable<SteamRomManagerParserConfig> CreateUserConfigurations(string[] libraryNames)
        {
            return libraryNames.Select((libraryName, i) =>
            {
                return new SteamRomManagerParserConfig
                {
                    ParserType = "Manual",
                    ConfigTitle = $"Playnite - {libraryName}",
                    SteamDirectory = "${steamDirGlobal}",
                    SteamCategory = $"${{{libraryName}}}",
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
                        ManualManifests = Path.Combine(manifestsDir, libraryName)
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
                    ParserId = $"GeneratedByPlayniteSteamRomManagerCompanion_{i}",
                    Disabled = false,
                    Version = 15
                };
            });
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
            var path = Path.Combine(binariesDir, configDirectory, filename);
            fileSystemHelper.WriteJson(path, contents);
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
                fileSystemHelper.WriteBinary(binaryPath, await client.GetByteArrayAsync(binarySourceUri));
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
