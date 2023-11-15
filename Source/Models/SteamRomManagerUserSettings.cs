using Newtonsoft.Json;

namespace SteamRomManagerCompanion
{
    internal class EnvironmentVariables
    {
        [JsonProperty("steamDirectory")] public string SteamDirectory { get; set; }
        [JsonProperty("userAccounts")] public string UserAccounts { get; set; }
        [JsonProperty("romsDirectory")] public string RomsDirectory { get; set; }
        [JsonProperty("retroarchPath")] public string RetroarchPath { get; set; }
        [JsonProperty("raCoresDirectory")] public string RaCoresDirectory { get; set; }
        [JsonProperty("localImagesDirectory")] public string LocalImagesDirectory { get; set; }
    }

    internal class PreviewSettings
    {
        [JsonProperty("retrieveCurrentSteamImages")] public bool RetrieveCurrentSteamImages { get; set; }
        [JsonProperty("deleteDisabledShortcuts")] public bool DeleteDisabledShortcuts { get; set; }
        [JsonProperty("imageZoomPercentage")] public int ImageZoomPercentage { get; set; }
        [JsonProperty("preload")] public bool Preload { get; set; }
    }

    internal class Timestamps
    {
        [JsonProperty("check")] public long Check { get; set; }
        [JsonProperty("download")] public long Download { get; set; }
    }

    internal class FuzzyMatcher
    {
        [JsonProperty("timestamps")] public Timestamps Timestamps { get; set; }
        [JsonProperty("verbose")] public bool Verbose { get; set; }
        [JsonProperty("filterProviders")] public bool FilterProviders { get; set; }
    }

    internal class SteamRomManagerUserSettings
    {
        [JsonProperty("fuzzyMatcher")] public FuzzyMatcher FuzzyMatcher { get; set; }
        [JsonProperty("environmentVariables")] public EnvironmentVariables EnvironmentVariables { get; set; }
        [JsonProperty("previewSettings")] public PreviewSettings PreviewSettings { get; set; }
        [JsonProperty("enabledProviders")] public string[] EnabledProviders { get; set; }
        [JsonProperty("batchDownloadSize")] public int BatchDownloadSize { get; set; }
        [JsonProperty("language")] public string Language { get; set; }
        [JsonProperty("theme")] public string Theme { get; set; }
        [JsonProperty("offlineMode")] public bool OfflineMode { get; set; }
        [JsonProperty("navigationWidth")] public int NavigationWidth { get; set; }
        [JsonProperty("clearLogOnTest")] public bool ClearLogOnTest { get; set; }
        [JsonProperty("version")] public int Version { get; set; }
    }
}
