using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SteamRomManagerCompanion
{
    internal class FilesystemHelperArgs
    {
        public string binariesDataDir { get; set; }
        public string manifestsDataDir { get; set; }
        public string scriptsDir { get; set; }
        public string stateDataDir { get; set; }
    }

    internal class FilesystemHelper
    {
        public string binariesDataDir { get; }
        public string manifestsDataDir { get; }
        public string scriptsDir { get; }
        public string stateDataDir { get; }

        public FilesystemHelper(FilesystemHelperArgs args)
        {
            binariesDataDir = args.binariesDataDir;
            manifestsDataDir = args.manifestsDataDir;
            scriptsDir = args.scriptsDir;
            stateDataDir = args.stateDataDir;
        }

        public string ReadFile(string path)
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

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
            File.WriteAllText(path, contents);
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

        public void WriteResourceToFile(string resourceName, string fileName)
        {
            using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                using (var file = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                {
                    resource.CopyTo(file);
                }
            }
        }
    }
}
