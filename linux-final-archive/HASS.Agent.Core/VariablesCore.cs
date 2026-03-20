using System.IO;

namespace HASS.Agent.Core
{
    public static class VariablesCore
    {
        public static string StartupPath { get; } = Directory.GetCurrentDirectory();

        // Config path resolution order:
        // 1. HASS_AGENT_CONFIG_PATH env var
        // 2. /etc/hass-agent
        // 3. $XDG_CONFIG_HOME/hass-agent
        // 4. $HOME/.config/hass-agent
        // 5. ./config (relative to startup)
        public static string ConfigPath { get; } = DetermineConfigPath();

        private static string DetermineConfigPath()
        {
            try
            {
                var env = Environment.GetEnvironmentVariable("HASS_AGENT_CONFIG_PATH");
                if (!string.IsNullOrWhiteSpace(env))
                {
                    if (!Directory.Exists(env)) Directory.CreateDirectory(env);
                    return env;
                }

                var etc = Path.Combine(Path.DirectorySeparatorChar.ToString(), "etc", "hass-agent");
                if (Directory.Exists(etc) || TryCreateDirectory(etc)) return etc;

                var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                if (!string.IsNullOrWhiteSpace(xdg))
                {
                    var p = Path.Combine(xdg, "hass-agent");
                    if (Directory.Exists(p) || TryCreateDirectory(p)) return p;
                }

                var home = Environment.GetEnvironmentVariable("HOME");
                if (!string.IsNullOrWhiteSpace(home))
                {
                    var p = Path.Combine(home, ".config", "hass-agent");
                    if (Directory.Exists(p) || TryCreateDirectory(p)) return p;
                }

                var fallback = Path.Combine(StartupPath, "config");
                if (!Directory.Exists(fallback)) Directory.CreateDirectory(fallback);
                return fallback;
            }
            catch
            {
                var fallback = Path.Combine(StartupPath, "config");
                try { if (!Directory.Exists(fallback)) Directory.CreateDirectory(fallback); } catch { }
                return fallback;
            }
        }

        private static bool TryCreateDirectory(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

