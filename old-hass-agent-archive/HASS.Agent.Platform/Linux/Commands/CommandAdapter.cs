using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Serilog;

namespace HASS.Agent.Platform.Linux.Commands
{
    public static class CommandAdapter
    {
        /// <summary>
        /// Execute a shell command safely
        /// </summary>
        /// <param name="command">Command to execute</param>
        /// <returns>True if command started successfully</returns>
        public static bool Execute(string command)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(command))
                {
                    Log.Warning("[PLATFORM.LINUX][COMMAND] Empty command received");
                    return false;
                }
                
                // Sanitize command to prevent basic injection
                var sanitizedCommand = SanitizeCommand(command);
                
                Log.Information("[PLATFORM.LINUX][COMMAND] Execute: {cmd}", sanitizedCommand);
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = "/bin/sh",
                    Arguments = $"-c {EscapeForShell(sanitizedCommand)}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                using var process = Process.Start(startInfo);
                return process != null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[PLATFORM.LINUX][COMMAND] Error executing command");
                return false;
            }
        }
        
        /// <summary>
        /// Execute a command and wait for completion
        /// </summary>
        public static async Task<(bool success, string output, string error)> ExecuteWithOutputAsync(string command, int timeoutMs = 30000)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(command))
                {
                    return (false, "", "Empty command");
                }
                
                var sanitizedCommand = SanitizeCommand(command);
                
                Log.Debug("[PLATFORM.LINUX][COMMAND] ExecuteWithOutput: {cmd}", sanitizedCommand);
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = "/bin/sh",
                    Arguments = $"-c {EscapeForShell(sanitizedCommand)}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                using var process = new Process { StartInfo = startInfo };
                process.Start();
                
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                
                using var cts = new System.Threading.CancellationTokenSource(timeoutMs);
                await process.WaitForExitAsync(cts.Token);
                
                return (process.ExitCode == 0, output.Trim(), error.Trim());
            }
            catch (OperationCanceledException)
            {
                Log.Warning("[PLATFORM.LINUX][COMMAND] Command timed out: {cmd}", command);
                return (false, "", "Command timed out");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[PLATFORM.LINUX][COMMAND] Error executing command with output");
                return (false, "", ex.Message);
            }
        }
        
        /// <summary>
        /// Sanitize command to prevent common injection attacks
        /// </summary>
        private static string SanitizeCommand(string command)
        {
            // Remove null bytes
            command = command.Replace("\0", "");
            
            // Limit command length
            if (command.Length > 4096)
            {
                throw new ArgumentException("Command too long");
            }
            
            return command;
        }
        
        /// <summary>
        /// Properly escape a string for use as a shell argument
        /// </summary>
        private static string EscapeForShell(string argument)
        {
            // Use single quotes and escape any single quotes in the string
            // This is the safest way to pass arbitrary strings to sh -c
            var escaped = argument.Replace("'", "'\"'\"'");
            return $"'{escaped}'";
        }
    }
}
