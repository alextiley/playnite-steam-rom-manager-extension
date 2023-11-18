using Newtonsoft.Json;

namespace SteamRomManagerCompanion
{
    internal class UserAccounts
    {
        [JsonProperty("specifiedAccounts")] public string SpecifiedAccounts { get; set; }
    }

    internal class Executable
    {
        [JsonProperty("path")] public string Path { get; set; }
        [JsonProperty("shortcutPassthrough")] public bool ShortcutPassthrough { get; set; }
        [JsonProperty("appendArgsToExecutable")] public bool AppendArgsToExecutable { get; set; }
    }

    internal class ParserInputs
    {
        [JsonProperty("manualManifests")] public string ManualManifests { get; set; }
    }

    internal class TitleFromVariable
    {
        [JsonProperty("limitToGroups")] public string LimitToGroups { get; set; }
        [JsonProperty("caseInsensitiveVariables")] public bool CaseInsensitiveVariables { get; set; }
        [JsonProperty("skipFileIfVariableWasNotFound")] public bool SkipFileIfVariableWasNotFound { get; set; }
        [JsonProperty("tryToMatchTitle")] public bool TryToMatchTitle { get; set; }
    }

    internal class FuzzyMatch
    {
        [JsonProperty("replaceDiacritics")] public bool ReplaceDiacritics { get; set; }
        [JsonProperty("removeCharacters")] public bool RemoveCharacters { get; set; }
        [JsonProperty("removeBrackets")] public bool RemoveBrackets { get; set; }
    }

    internal class Controller
    {
        [JsonProperty("title")] public string Title { get; set; }
        [JsonProperty("mappingId")] public string MappingId { get; set; }
        [JsonProperty("profileType")] public string ProfileType { get; set; }
    }

    internal class Controllers
    {
        [JsonProperty("ps4")] public Controller PS4 { get; set; }
        [JsonProperty("ps5")] public Controller PS5 { get; set; }
        [JsonProperty("xbox360")] public Controller Xbox360 { get; set; }
        [JsonProperty("xboxone")] public Controller XboxOne { get; set; }
        [JsonProperty("switch_joycon_left")] public Controller SwitchJoyconLeft { get; set; }
        [JsonProperty("switch_joycon_right")] public Controller SwitchJoyconRight { get; set; }
        [JsonProperty("switch_pro")] public Controller SwitchPro { get; set; }
        [JsonProperty("neptune")] public Controller Neptune { get; set; }
    }

    internal class SteamGridDB
    {
        [JsonProperty("nsfw")] public bool Nsfw { get; set; }
        [JsonProperty("humor")] public bool Humor { get; set; }
        [JsonProperty("styles")] public string[] Styles { get; set; }
        [JsonProperty("stylesHero")] public string[] StylesHero { get; set; }
        [JsonProperty("stylesLogo")] public string[] StylesLogo { get; set; }
        [JsonProperty("stylesIcon")] public string[] StylesIcon { get; set; }
        [JsonProperty("imageMotionTypes")] public string[] ImageMotionTypes { get; set; }
    }

    internal class ImageProviderAPIs
    {
        [JsonProperty("SteamGridDB")] public SteamGridDB SteamGridDB { get; set; }
    }

    internal class Image
    {
        [JsonProperty("tall")] public string Tall { get; set; }
        [JsonProperty("long")] public string Long { get; set; }
        [JsonProperty("hero")] public string Hero { get; set; }
        [JsonProperty("logo")] public string Logo { get; set; }
        [JsonProperty("icon")] public string Icon { get; set; }
    }

    internal class SteamRomManagerParserConfig
    {
        [JsonProperty("parserType")] public string ParserType { get; set; }
        [JsonProperty("configTitle")] public string ConfigTitle { get; set; }
        [JsonProperty("steamDirectory")] public string SteamDirectory { get; set; }
        [JsonProperty("steamCategory")] public string SteamCategory { get; set; }
        [JsonProperty("romDirectory")] public string RomDirectory { get; set; }
        [JsonProperty("executableArgs")] public string ExecutableArgs { get; set; }
        [JsonProperty("executableModifier")] public string ExecutableModifier { get; set; }
        [JsonProperty("startInDirectory")] public string StartInDirectory { get; set; }
        [JsonProperty("titleModifier")] public string TitleModifier { get; set; }
        [JsonProperty("fetchControllerTemplatesButton")] public string FetchControllerTemplatesButton { get; set; }
        [JsonProperty("removeControllersButton")] public string RemoveControllersButton { get; set; }
        [JsonProperty("imageProviders")] public string[] ImageProviders { get; set; }
        [JsonProperty("onlineImageQueries")] public string OnlineImageQueries { get; set; }
        [JsonProperty("imagePool")] public string ImagePool { get; set; }
        [JsonProperty("userAccounts")] public UserAccounts UserAccounts { get; set; }
        [JsonProperty("executable")] public Executable Executable { get; set; }
        [JsonProperty("parserInputs")] public ParserInputs ParserInputs { get; set; }
        [JsonProperty("titleFromVariable")] public TitleFromVariable TitleFromVariable { get; set; }
        [JsonProperty("fuzzyMatch")] public FuzzyMatch FuzzyMatch { get; set; }
        [JsonProperty("controllers")] public Controllers Controllers { get; set; }
        [JsonProperty("imageProviderAPIs")] public ImageProviderAPIs ImageProviderAPIs { get; set; }
        [JsonProperty("defaultImage")] public Image DefaultImage { get; set; }
        [JsonProperty("localImages")] public Image LocalImages { get; set; }
        [JsonProperty("version")] public long Version { get; set; }
        [JsonProperty("disabled")] public bool Disabled { get; set; }
        [JsonProperty("parserId")] public string ParserId { get; set; }
    }
}
