using SteamRomManagerCompanion.Interfaces;
using System;
using System.IO;

namespace SteamRomManagerCompanion.Storage
{
    internal class GameStateTrackerArgs
    {
        public string dataDir { get; set; }
        public IFileSystemController fileSystem { get; set; }
    }

    internal class GameStateTracker : IGameStateTracker
    {
        private readonly string dataDir;
        private readonly IFileSystemController fileSystem;

        public GameStateTracker(GameStateTrackerArgs args)
        {
            dataDir = args.dataDir;
            fileSystem = args.fileSystem;
        }

        public void Start(Guid gameId)
        {
            string id = gameId.ToString();
            fileSystem.WriteFile(Path.Combine(dataDir, id), "");
        }

        public void Stop(Guid gameId)
        {
            string id = gameId.ToString();
            fileSystem.Delete(Path.Combine(dataDir, id));
        }
    }
}
