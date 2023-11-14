using System;

namespace SteamRomManagerCompanion.Interfaces
{
    internal interface IGameStateTracker
    {
        void Start(Guid game);
        void Stop(Guid game);
    }
}
