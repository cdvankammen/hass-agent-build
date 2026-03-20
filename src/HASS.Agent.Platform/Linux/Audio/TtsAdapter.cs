using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace HASS.Agent.Platform.Linux.Audio
{
    public static class TtsAdapter
    {
        public static bool IsAvailable()
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo { FileName = "which", Arguments = "espeak", RedirectStandardOutput = true, UseShellExecute = false });
                if (p != null)
                {
                    p.WaitForExit(300);
                    if (p.ExitCode == 0) return true;
                }

                using var p2 = Process.Start(new ProcessStartInfo { FileName = "which", Arguments = "spd-say", RedirectStandardOutput = true, UseShellExecute = false });
                if (p2 != null)
                {
                    p2.WaitForExit(300);
                    return p2.ExitCode == 0;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public static Task<bool> SpeakAsync(string text)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(text)) return false;

                    // prefer espeak
                    using var p = Process.Start(new ProcessStartInfo { FileName = "which", Arguments = "espeak", RedirectStandardOutput = true, UseShellExecute = false });
                    if (p != null)
                    {
                        p.WaitForExit(300);
                        if (p.ExitCode == 0)
                        {
                            var pr = Process.Start(new ProcessStartInfo { FileName = "espeak", Arguments = $"\"{text.Replace("\"", "\\\"")}\"", UseShellExecute = false });
                            if (pr != null) pr.Dispose();
                            return true;
                        }
                    }

                    using var p2 = Process.Start(new ProcessStartInfo { FileName = "which", Arguments = "spd-say", RedirectStandardOutput = true, UseShellExecute = false });
                    if (p2 != null)
                    {
                        p2.WaitForExit(300);
                        if (p2.ExitCode == 0)
                        {
                            var pr2 = Process.Start(new ProcessStartInfo { FileName = "spd-say", Arguments = $"\"{text.Replace("\"", "\\\"")}\"", UseShellExecute = false });
                            if (pr2 != null) pr2.Dispose();
                            return true;
                        }
                    }

                    return false;
                }
                catch
                {
                    return false;
                }
            });
        }
    }
}
