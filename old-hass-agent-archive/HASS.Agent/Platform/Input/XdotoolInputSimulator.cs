using System;
using System.Diagnostics;
using Serilog;

namespace HASS.Agent.Platform.Input
{
    public class XdotoolInputSimulator : IInputSimulator
    {
        private bool CommandExists(string name)
        {
            try
            {
                var psi = new ProcessStartInfo("which") {Arguments = name, RedirectStandardOutput = true, UseShellExecute = false};
                using var p = Process.Start(psi);
                if (p == null) return false;
                p.WaitForExit(1000);
                return p.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        public void SendKey(string key)
        {
            try
            {
                if (!CommandExists("xdotool"))
                {
                    Log.Warning("[INPUT] xdotool not found, cannot send key");
                    return;
                }

                var psi = new ProcessStartInfo("xdotool") { Arguments = $"key {key}", UseShellExecute = false };
                using var p = Process.Start(psi);
                p?.WaitForExit(2000);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[INPUT] Error sending key");
            }
        }

        public void SendText(string text)
        {
            try
            {
                if (!CommandExists("xdotool"))
                {
                    Log.Warning("[INPUT] xdotool not found, cannot send text");
                    return;
                }

                var psi = new ProcessStartInfo("xdotool") { Arguments = $"type --delay 10 \"{text}\"", UseShellExecute = false };
                using var p = Process.Start(psi);
                p?.WaitForExit(2000);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[INPUT] Error sending text");
            }
        }

        public void SendKeySequence(string sequence)
        {
            // naive separator: comma
            var parts = sequence.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var p in parts)
            {
                if (p.StartsWith("type:", StringComparison.OrdinalIgnoreCase))
                {
                    SendText(p.Substring(5));
                }
                else
                {
                    SendKey(p);
                }
            }
        }
    }
}
