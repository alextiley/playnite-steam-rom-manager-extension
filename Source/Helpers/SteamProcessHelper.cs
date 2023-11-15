using Microsoft.Win32;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace SteamRomManagerCompanion
{
    internal class SteamProcessHelper
    {
        private static readonly string steamExecutableFilename = "steam.exe";
        private static readonly string registryKey64 = "Software\\Wow6432Node\\Valve\\Steam";
        private static readonly string registryKey32 = "Software\\Valve\\Steam";

        public string GetExePath()
        {
            return
                GetSteamInstallPathFromRegistry(registryKey64) ??
                GetSteamInstallPathFromRegistry(registryKey32) ??
                throw new FileNotFoundException("steam not installed");
        }

        public string GetInstallPath()
        {
            return Path.GetDirectoryName(GetExePath());
        }

        public void Start()
        {
            var info = new ProcessStartInfo
            {
                WorkingDirectory = GetInstallPath(),
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                FileName = GetExePath()
            };
            _ = Process.Start(info);
        }

        public bool IsRunning()
        {
            return Process.GetProcessesByName(
                Path.GetFileNameWithoutExtension(GetExePath())
            ).Length > 0;
        }

        public void Stop()
        {
            Process.GetProcessesByName(
                Path.GetFileNameWithoutExtension(GetExePath())
            ).ForEach(
                process => process.Kill()
            );
        }

        private string GetSteamInstallPathFromRegistry(string key)
        {
            var path = Registry.LocalMachine.OpenSubKey(key).GetValue("InstallPath");
            return path != null ? Path.Combine(path.ToString(), steamExecutableFilename) : null;
        }
    }
}
