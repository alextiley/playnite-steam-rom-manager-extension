using Newtonsoft.Json;

namespace SteamRomManagerCompanion
{
    internal class SteamRomManagerManifest
    {
        public SteamRomManagerManifest(string title, string target, string startIn, string launchOptions)
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
}
