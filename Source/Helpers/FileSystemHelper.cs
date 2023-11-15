using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SteamRomManagerCompanion
{
    internal class FilesystemHelper
    {
        public void DeleteDirectoryContents(string path)
        {
            try
            {
                Directory.GetFileSystemEntries(path).ToArray().ForEach((node) => Delete(node));
            }
            catch
            {
                return;
            }
        }

        public void Delete(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            else if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        public void CreateDirectory(string path)
        {
            _ = Directory.CreateDirectory(path);
        }

        public void WriteFile(string path, string contents)
        {
            CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, contents, Encoding.UTF8);
        }

        public void WriteJson(string path, object json, Formatting format = Formatting.None)
        {
            CreateDirectory(Path.GetDirectoryName(path));
            WriteFile(path, JsonConvert.SerializeObject(json, format));
        }

        public void WriteBinary(string path, byte[] bytes)
        {
            CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, bytes);
        }
    }
}
