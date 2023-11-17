using System.Diagnostics;
using System.IO;

namespace SteamRomManagerCompanion
{
    internal class PlayniteHelper
    {
        public string GetExecutablePath()
        {
            return Process.GetCurrentProcess().MainModule.FileName;
        }

        public string GetInstallPath()
        {
            return Path.GetDirectoryName(GetExecutablePath());
        }

        public void Restart(string flags)
        {
            var currentProcess = Process.GetCurrentProcess();

            // Start the current process again in ~5s.
            // This is a hack, timeout doesn't work so this is the next best thing.
            var Info = new ProcessStartInfo
            {
                Arguments = $"/C ping 127.0.0.1 -n 5 && \"{currentProcess.MainModule.FileName}\" {flags}",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                FileName = "cmd.exe"
            };
            _ = Process.Start(Info);

            // Kills the current process
            currentProcess.Kill();
        }
    }
}
