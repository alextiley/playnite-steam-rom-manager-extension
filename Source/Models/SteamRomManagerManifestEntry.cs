using Newtonsoft.Json;

namespace SteamRomManagerCompanion.Models
{
    internal class SteamRomManagerManifestEntryArgs
    {
        public string StartIn { get; set; }
        public string Target { get; set; }
        public string LaunchOptions { get; set; }
        public string Title { get; set; }
    }

    internal class SteamRomManagerManifestEntry
    {
        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("target")]
        public string Target { get; set; }

        [JsonProperty("startIn")]
        public string StartIn { get; set; }

        [JsonProperty("launchOptions")]
        public string LaunchOptions { get; set; }

        public SteamRomManagerManifestEntry(SteamRomManagerManifestEntryArgs args)
        {
            StartIn = args.StartIn;
            Title = args.Title;
            Target = args.Target;
            LaunchOptions = args.LaunchOptions;
        }
    }
}
