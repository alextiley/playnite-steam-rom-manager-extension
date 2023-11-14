using SteamRomManagerCompanion.Interfaces;
using System.Diagnostics;
using System.IO;

namespace SteamRomManagerCompanion.Controllers
{
    internal class PlayniteProcessController : IProcessController
    {
        public string GetExePath()
        {
            return Process.GetCurrentProcess().MainModule.FileName;
        }

        public string GetInstallPath()
        {
            return Path.GetDirectoryName(GetExePath());
        }

        public void Restart()
        {
            Process CurrentProcess = Process.GetCurrentProcess();

            // Hack, timeout doesn't work so this is the next best thing.
            ProcessStartInfo Info = new ProcessStartInfo
            {
                Arguments = "/C ping 127.0.0.1 -n 2 && \"" + CurrentProcess.MainModule.FileName + "\"",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                FileName = "cmd.exe"
            };
            _ = Process.Start(Info);

            CurrentProcess.Kill();
        }
    }
}
