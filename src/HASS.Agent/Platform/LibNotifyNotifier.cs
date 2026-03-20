using System;
using System.Diagnostics;
using Serilog;

namespace HASS.Agent.Platform
{
    public class LibNotifyNotifier : INotifier
    {
        public void Notify(string title, string message, bool isError = false)
        {
            try
            {
                // Try notify-send if available
                var psi = new ProcessStartInfo("notify-send")
                {
                    Arguments = $"\"{title}\" \"{message}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var p = Process.Start(psi);
                p?.WaitForExit(2000);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[PLATFORM.NOTIFY] notify-send not available, falling back to log");
                Log.Information("{title} - {msg}", title, message);
            }
        }
    }
}
