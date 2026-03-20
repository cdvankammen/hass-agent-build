using System;
using Serilog;

using HASS.Agent.Platform;

namespace HASS.Agent.Platform.Linux.Notifications
{
    public class Notifier : INotifier
    {
        public void Notify(string title, string message)
        {
            try
            {
                Serilog.Log.Information("[PLATFORM.LINUX][NOTIFY] {title}: {msg}", title, message);
                // Best-effort: try notify-send if available
                try
                {
                    var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "which", Arguments = "notify-send", UseShellExecute = false, RedirectStandardOutput = true });
                    if (p != null)
                    {
                        p.WaitForExit(200);
                        if (p.ExitCode == 0)
                        {
                            var n = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "notify-send", Arguments = $"\"{title}\" \"{message}\"", UseShellExecute = false });
                            if (n != null) n.Dispose();
                        }
                    }
                }
                catch { }
            }
            catch (System.Exception ex)
            {
                Serilog.Log.Error(ex, "[PLATFORM.LINUX][NOTIFY] Error sending notification");
            }
        }
    }
}
