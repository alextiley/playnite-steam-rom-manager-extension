namespace SteamRomManagerCompanion.Interfaces
{
    internal interface IUriHandler
    {
        void Register(string path);
        void Unregister(string path);
    }
}
