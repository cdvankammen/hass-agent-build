using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Serilog;

namespace HASS.Agent.Platform.Linux.Media
{
    public class MediaManager
    {
        private readonly MprisManager? _mpris;

        public MediaManager()
        {
            Log.Information("[Media] Linux MediaManager initialized (MPRIS/playerctl)");
            try { _mpris = new MprisManager(); } catch { }
        }

        public void Start()
        {
            Log.Information("[Media] Start MPRIS monitoring");
            
            if (_mpris != null && _mpris.DbUsAvailable)
            {
                Log.Information("[Media] Using DBus MPRIS for media control");
                return;
            }

            if (IsPlayerctlAvailable())
            {
                Log.Information("[Media] playerctl available for controlling MPRIS players");
            }
            else
            {
                Log.Warning("[Media] Neither DBus nor playerctl available");
            }
        }

        public void Stop()
        {
            Log.Information("[Media] Stop MPRIS monitoring");
        }

        public async Task PlayAsync()
        {
            Log.Debug("[Media] Play called");
            await ExecutePlayerctlAsync("play");
        }

        public async Task PauseAsync()
        {
            Log.Debug("[Media] Pause called");
            await ExecutePlayerctlAsync("pause");
        }

        public async Task PlayPauseAsync()
        {
            Log.Debug("[Media] PlayPause called");
            if (_mpris != null && _mpris.DbUsAvailable)
            {
                await _mpris.PlayPauseAsync();
                return;
            }
            await ExecutePlayerctlAsync("play-pause");
        }

        public void PlayPause()
        {
            PlayPauseAsync().GetAwaiter().GetResult();
        }

        public async Task NextAsync()
        {
            Log.Debug("[Media] Next called");
            await ExecutePlayerctlAsync("next");
        }

        public async Task PreviousAsync()
        {
            Log.Debug("[Media] Previous called");
            await ExecutePlayerctlAsync("previous");
        }

        public async Task<MediaStatus> GetStatusAsync()
        {
            var status = new MediaStatus();
            
            try
            {
                // Get player status via playerctl
                status.Status = await GetPlayerctlOutputAsync("status") ?? "Stopped";
                status.Title = await GetPlayerctlOutputAsync("metadata title") ?? "";
                status.Artist = await GetPlayerctlOutputAsync("metadata artist") ?? "";
                status.Album = await GetPlayerctlOutputAsync("metadata album") ?? "";
                status.Position = await GetPlayerctlOutputAsync("position") ?? "0";
                status.Available = true;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[Media] Error getting status");
                status.Available = false;
            }
            
            return status;
        }

        private async Task ExecutePlayerctlAsync(string args)
        {
            try
            {
                using var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = "playerctl",
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });
                
                if (proc != null)
                {
                    await proc.WaitForExitAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Media] playerctl {args} failed", args);
            }
        }

        private async Task<string?> GetPlayerctlOutputAsync(string args)
        {
            try
            {
                using var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = "playerctl",
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });
                
                if (proc != null)
                {
                    var output = await proc.StandardOutput.ReadToEndAsync();
                    await proc.WaitForExitAsync();
                    return output.Trim();
                }
            }
            catch { }
            return null;
        }

        private static bool IsPlayerctlAvailable()
        {
            try
            {
                using var which = Process.Start(new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = "playerctl",
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                });
                
                if (which != null)
                {
                    which.WaitForExit(200);
                    return which.ExitCode == 0;
                }
            }
            catch { }
            return false;
        }

        public static bool IsAvailable() => IsPlayerctlAvailable();
    }

    public class MediaStatus
    {
        public bool Available { get; set; }
        public string Status { get; set; } = "Unknown";
        public string Title { get; set; } = "";
        public string Artist { get; set; } = "";
        public string Album { get; set; } = "";
        public string Position { get; set; } = "0";
    }
}
