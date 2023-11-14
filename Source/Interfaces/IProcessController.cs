namespace SteamRomManagerCompanion.Interfaces
{
    internal interface IProcessController
    {
        void Restart();
        string GetInstallPath();
        string GetExePath();
    }
}
