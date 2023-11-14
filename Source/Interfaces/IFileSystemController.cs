namespace SteamRomManagerCompanion.Interfaces
{
    internal interface IFileSystemController
    {
        void CreateDirectory(string path);
        void EmptyDirectory(string path);
        void Delete(string path);
        void WriteFile(string path, string contents);
        void WriteJson(string path, object json);
    }
}
