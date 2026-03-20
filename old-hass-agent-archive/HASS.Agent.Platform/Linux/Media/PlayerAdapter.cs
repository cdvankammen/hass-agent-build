using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace HASS.Agent.Platform.Linux.Media
{
    public static class PlayerAdapter
    {
        // Minimal adapter using 'mpv'. Tracks a single playback process and provides basic controls.
        private static Process? _playerProcess;
        private static readonly object _lock = new object();

        public static bool IsPlayerAvailable()
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo { FileName = "which", Arguments = "mpv", RedirectStandardOutput = true, UseShellExecute = false });
                if (p == null) return false;
                p.WaitForExit(500);
                return p.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        public static Task<bool> PlayAsync(string uri)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (!IsPlayerAvailable()) return false;

                    Stop(); // stop any existing player

                    var psi = new ProcessStartInfo
                    {
                        FileName = "mpv",
                        Arguments = $"--no-terminal --really-quiet --force-window=no \"{uri}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = false,
                        RedirectStandardError = false,
                    };

                    lock (_lock)
                    {
                        _playerProcess = Process.Start(psi);
                    }

                    return _playerProcess != null;
                }
                catch
                {
                    return false;
                }
            });
        }

        public static void Stop()
        {
            try
            {
                lock (_lock)
                {
                    if (_playerProcess == null) return;
                    try { if (!_playerProcess.HasExited) _playerProcess.Kill(); } catch { }
                    try { _playerProcess.Dispose(); } catch { }
                    _playerProcess = null;
                }
            }
            catch
            {
                // best effort
            }
        }

        public static void Pause()
        {
            try
            {
                lock (_lock)
                {
                    if (_playerProcess == null || _playerProcess.HasExited) return;
                    // send SIGSTOP
                    var killer = Process.Start(new ProcessStartInfo { FileName = "kill", Arguments = $"-STOP {_playerProcess.Id}", UseShellExecute = false });
                    if (killer != null) killer.Dispose();
                }
            }
            catch
            {
                // ignore
            }
        }

        public static void Resume()
        {
            try
            {
                lock (_lock)
                {
                    if (_playerProcess == null || _playerProcess.HasExited) return;
                    // send SIGCONT
                    var continuer = Process.Start(new ProcessStartInfo { FileName = "kill", Arguments = $"-CONT {_playerProcess.Id}", UseShellExecute = false });
                    if (continuer != null) continuer.Dispose();
                }
            }
            catch
            {
                // ignore
            }
        }

        public static bool IsPlaying()
        {
            try
            {
                lock (_lock)
                {
                    return _playerProcess != null && !_playerProcess.HasExited;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}

