using System;
using System.Diagnostics;
using Serilog;

namespace HASS.Agent.Platform.Linux.Notification
{
    public static class NotificationManager
    {
        public static void Notify(string title, string body)
        {
            try
            {
                // try notify-send first
                var psi = new ProcessStartInfo("notify-send", $"\"{EscapeArg(title)}\" \"{EscapeArg(body)}\"") { UseShellExecute = false };
                var p = Process.Start(psi);
                p?.WaitForExit();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to run notify-send");
            }
        }

        private static string EscapeArg(string s)
        {
            return s?.Replace("\"", "'\"'\"") ?? string.Empty;
        }
    }
}
