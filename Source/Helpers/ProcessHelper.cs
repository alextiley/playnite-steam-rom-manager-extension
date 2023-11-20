using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SteamRomManagerCompanion
{
    internal class ProcessHelper
    {
        public static async Task<bool> RunCommand(int timeout, string filename, string args, string startIn)
        {
            var enableProcess = new Process();
            var enableProcessHandled = new TaskCompletionSource<bool>();

            enableProcess.StartInfo.FileName = filename;
            enableProcess.StartInfo.Arguments = args;
            enableProcess.StartInfo.WorkingDirectory = startIn;
            enableProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            enableProcess.StartInfo.CreateNoWindow = true;
            enableProcess.EnableRaisingEvents = true;
            enableProcess.Exited += new EventHandler(
                (object sender, EventArgs e) => enableProcessHandled.TrySetResult(true)
            );
            _ = enableProcess.Start();

            // Wait no longer than 60 seconds
            var result = await Task.WhenAny(enableProcessHandled.Task, Task.Delay(timeout));

            return result.IsCompleted;
        }
    }
}
