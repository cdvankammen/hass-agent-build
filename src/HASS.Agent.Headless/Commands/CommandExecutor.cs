using System;
using System.Threading.Tasks;
using HASS.Agent.Core;
using HASS.Agent.Platform;
using HASS.Agent.Platform.Abstractions;
using Serilog;

namespace HASS.Agent.Headless.Commands
{
    /// <summary>
    /// Platform-agnostic command executor that delegates to appropriate platform implementations
    /// </summary>
    public class CommandExecutor
    {
        private readonly IInputSimulator? _inputSimulator;
        private readonly Func<string, bool>? _shellExecutor;
        
        public CommandExecutor()
        {
            try
            {
                _inputSimulator = PlatformFactory.GetInputSimulator();
            }
            catch (PlatformNotSupportedException)
            {
                Log.Warning("[EXECUTOR] Input simulator not available on this platform");
            }
            
            _shellExecutor = PlatformFactory.GetCommandExecutor();
        }
        
        /// <summary>
        /// Execute a command based on its type
        /// </summary>
        public async Task<bool> ExecuteAsync(CommandModel command)
        {
            if (command == null)
            {
                Log.Warning("[EXECUTOR] Null command received");
                return false;
            }
            
            Log.Information("[EXECUTOR] Executing command: {name} ({type})", command.Name, command.EntityType);
            
            // Run synchronous execution in task
            return await Task.Run(() => command.EntityType?.ToLowerInvariant() switch
            {
                "key" or "keycommand" => ExecuteKeyCommand(command),
                "multiplekeys" or "multiplekeyscommand" => ExecuteMultipleKeysCommand(command),
                "custom" or "customcommand" => ExecuteCustomCommand(command),
                "powershell" or "shell" or "bash" => ExecuteShellCommand(command),
                "launchurl" or "url" => ExecuteLaunchUrl(command),
                "setvolume" or "volume" => ExecuteVolumeCommand(command),
                "monitor" or "monitorsleep" or "monitorwake" => ExecuteMonitorCommand(command),
                "internal" => ExecuteInternalCommand(command),
                _ => ExecuteGenericCommand(command)
            });
        }
        
        private bool ExecuteKeyCommand(CommandModel command)
        {
            if (_inputSimulator == null)
            {
                Log.Warning("[EXECUTOR] Input simulator not available");
                return false;
            }
            
            if (!_inputSimulator.IsAvailable())
            {
                Log.Warning("[EXECUTOR] Input simulator not ready. Requirements:\n{req}", 
                    _inputSimulator.GetRequirements());
                return false;
            }
            
            // Try keycode first, then command as key sequence
            if (!string.IsNullOrWhiteSpace(command.KeyCode))
            {
                Log.Debug("[EXECUTOR] Sending key code: {code}", command.KeyCode);
                return _inputSimulator.SendKeySequence(command.KeyCode);
            }
            
            if (!string.IsNullOrWhiteSpace(command.Command))
            {
                Log.Debug("[EXECUTOR] Sending key sequence: {seq}", command.Command);
                return _inputSimulator.SendKeySequence(command.Command);
            }
            
            Log.Warning("[EXECUTOR] Key command has no keycode or command");
            return false;
        }
        
        private bool ExecuteMultipleKeysCommand(CommandModel command)
        {
            if (_inputSimulator == null || !_inputSimulator.IsAvailable())
            {
                Log.Warning("[EXECUTOR] Input simulator not available");
                return false;
            }
            
            if (command.Keys == null || command.Keys.Count == 0)
            {
                Log.Warning("[EXECUTOR] Multiple keys command has no keys");
                return false;
            }
            
            Log.Debug("[EXECUTOR] Sending {count} keys", command.Keys.Count);
            return _inputSimulator.SendMultipleKeys(command.Keys);
        }
        
        private bool ExecuteCustomCommand(CommandModel command)
        {
            if (string.IsNullOrWhiteSpace(command.Command))
            {
                Log.Warning("[EXECUTOR] Custom command is empty");
                return false;
            }
            
            if (_shellExecutor == null)
            {
                Log.Warning("[EXECUTOR] Shell executor not available");
                return false;
            }
            
            Log.Debug("[EXECUTOR] Executing custom command: {cmd}", command.Command);
            return _shellExecutor(command.Command);
        }
        
        private bool ExecuteShellCommand(CommandModel command)
        {
            if (string.IsNullOrWhiteSpace(command.Command))
            {
                Log.Warning("[EXECUTOR] Shell command is empty");
                return false;
            }
            
            if (_shellExecutor == null)
            {
                Log.Warning("[EXECUTOR] Shell executor not available");
                return false;
            }
            
            // On Linux/macOS use sh
            var fullCmd = $"/bin/sh -c \"{command.Command.Replace("\"", "\\\"")}\"";
            
            Log.Debug("[EXECUTOR] Executing shell command");
            return _shellExecutor(fullCmd);
        }
        
        private bool ExecuteLaunchUrl(CommandModel command)
        {
            if (string.IsNullOrWhiteSpace(command.Command))
            {
                Log.Warning("[EXECUTOR] URL is empty");
                return false;
            }
            
            try
            {
                var url = command.Command;
                
                // Use platform-specific URL opener
                if (OperatingSystem.IsLinux())
                {
                    return _shellExecutor?.Invoke($"xdg-open '{url}'") ?? false;
                }
                else if (OperatingSystem.IsMacOS())
                {
                    return _shellExecutor?.Invoke($"open '{url}'") ?? false;
                }
                else
                {
                    // Windows
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[EXECUTOR] Error launching URL {url}", command.Command);
                return false;
            }
        }
        
        private bool ExecuteVolumeCommand(CommandModel command)
        {
            if (string.IsNullOrWhiteSpace(command.Command))
            {
                Log.Warning("[EXECUTOR] Volume level not specified");
                return false;
            }
            
            if (!int.TryParse(command.Command, out var volume))
            {
                Log.Warning("[EXECUTOR] Invalid volume level: {vol}", command.Command);
                return false;
            }
            
            volume = Math.Clamp(volume, 0, 100);
            
            try
            {
                if (OperatingSystem.IsLinux())
                {
                    // Use pactl for PulseAudio or pamixer
                    return _shellExecutor?.Invoke($"pactl set-sink-volume @DEFAULT_SINK@ {volume}%") ?? false;
                }
                else if (OperatingSystem.IsMacOS())
                {
                    return _shellExecutor?.Invoke($"osascript -e 'set volume output volume {volume}'") ?? false;
                }
                else
                {
                    Log.Warning("[EXECUTOR] Volume control not implemented for Windows in cross-platform layer");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[EXECUTOR] Error setting volume to {vol}", volume);
                return false;
            }
        }
        
        private bool ExecuteMonitorCommand(CommandModel command)
        {
            try
            {
                var action = command.EntityType?.ToLowerInvariant() ?? command.Command?.ToLowerInvariant() ?? "";
                
                if (OperatingSystem.IsLinux())
                {
                    if (action.Contains("sleep") || action.Contains("off"))
                    {
                        return _shellExecutor?.Invoke("xset dpms force off") ?? false;
                    }
                    else if (action.Contains("wake") || action.Contains("on"))
                    {
                        return _shellExecutor?.Invoke("xset dpms force on") ?? false;
                    }
                }
                else if (OperatingSystem.IsMacOS())
                {
                    if (action.Contains("sleep") || action.Contains("off"))
                    {
                        return _shellExecutor?.Invoke("pmset displaysleepnow") ?? false;
                    }
                    else if (action.Contains("wake") || action.Contains("on"))
                    {
                        return _shellExecutor?.Invoke("caffeinate -u -t 1") ?? false;
                    }
                }
                
                Log.Warning("[EXECUTOR] Monitor command not implemented for this platform");
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[EXECUTOR] Error executing monitor command");
                return false;
            }
        }
        
        private bool ExecuteInternalCommand(CommandModel command)
        {
            var internalCmd = command.Command?.ToLowerInvariant() ?? "";
            
            Log.Debug("[EXECUTOR] Internal command: {cmd}", internalCmd);
            
            return internalCmd switch
            {
                "lock" => ExecuteLockScreen(),
                "logout" or "logoff" => ExecuteLogout(),
                "sleep" or "suspend" => ExecuteSleep(),
                "hibernate" => ExecuteHibernate(),
                "shutdown" => ExecuteShutdown(),
                "restart" or "reboot" => ExecuteRestart(),
                _ => false
            };
        }
        
        private bool ExecuteLockScreen()
        {
            if (OperatingSystem.IsLinux())
            {
                // Try common screen lockers
                return _shellExecutor?.Invoke("loginctl lock-session || xdg-screensaver lock || gnome-screensaver-command -l") ?? false;
            }
            else if (OperatingSystem.IsMacOS())
            {
                return _shellExecutor?.Invoke("pmset displaysleepnow") ?? false;
            }
            return false;
        }
        
        private bool ExecuteLogout()
        {
            if (OperatingSystem.IsLinux())
            {
                return _shellExecutor?.Invoke("loginctl terminate-user $USER") ?? false;
            }
            else if (OperatingSystem.IsMacOS())
            {
                return _shellExecutor?.Invoke("osascript -e 'tell application \"System Events\" to log out'") ?? false;
            }
            return false;
        }
        
        private bool ExecuteSleep()
        {
            if (OperatingSystem.IsLinux())
            {
                return _shellExecutor?.Invoke("systemctl suspend") ?? false;
            }
            else if (OperatingSystem.IsMacOS())
            {
                return _shellExecutor?.Invoke("pmset sleepnow") ?? false;
            }
            return false;
        }
        
        private bool ExecuteHibernate()
        {
            if (OperatingSystem.IsLinux())
            {
                return _shellExecutor?.Invoke("systemctl hibernate") ?? false;
            }
            else if (OperatingSystem.IsMacOS())
            {
                // macOS uses sleep mode that includes hibernate
                return _shellExecutor?.Invoke("pmset sleepnow") ?? false;
            }
            return false;
        }
        
        private bool ExecuteShutdown()
        {
            if (OperatingSystem.IsLinux())
            {
                return _shellExecutor?.Invoke("systemctl poweroff") ?? false;
            }
            else if (OperatingSystem.IsMacOS())
            {
                return _shellExecutor?.Invoke("osascript -e 'tell application \"System Events\" to shut down'") ?? false;
            }
            return false;
        }
        
        private bool ExecuteRestart()
        {
            if (OperatingSystem.IsLinux())
            {
                return _shellExecutor?.Invoke("systemctl reboot") ?? false;
            }
            else if (OperatingSystem.IsMacOS())
            {
                return _shellExecutor?.Invoke("osascript -e 'tell application \"System Events\" to restart'") ?? false;
            }
            return false;
        }
        
        private bool ExecuteGenericCommand(CommandModel command)
        {
            // Try to execute as shell command if we have one
            if (!string.IsNullOrWhiteSpace(command.Command))
            {
                return ExecuteCustomCommand(command);
            }
            
            Log.Warning("[EXECUTOR] Cannot execute command: unknown type '{type}' with no command", command.EntityType);
            return false;
        }
    }
}
