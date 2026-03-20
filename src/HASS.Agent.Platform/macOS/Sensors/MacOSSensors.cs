using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using HASS.Agent.Platform.Abstractions;
using Serilog;

namespace HASS.Agent.Platform.macOS.Sensors
{
    /// <summary>
    /// macOS System Resources sensor using sysctl and vm_stat
    /// </summary>
    public class MacOSSystemResourcesSensor : ISensor
    {
        public string Id { get; }
        public string Name { get; }
        
        private double _lastCpuPercent;
        private DateTime _lastCpuCheck = DateTime.MinValue;
        
        public MacOSSystemResourcesSensor(string id = "system_resources", string name = "System Resources")
        {
            Id = id;
            Name = name;
        }
        
        public Dictionary<string, object> GetState()
        {
            try
            {
                var result = new Dictionary<string, object>();
                
                // CPU info using top command (macOS specific)
                var cpuPercent = GetCpuUsage();
                result["cpu_percent"] = cpuPercent;
                
                // Memory from vm_stat
                var memInfo = GetMemoryInfo();
                result["memory_total_mb"] = memInfo.totalMB;
                result["memory_available_mb"] = memInfo.availableMB;
                result["memory_used_mb"] = memInfo.usedMB;
                result["memory_percent"] = memInfo.usedPercent;
                
                // Load average from sysctl
                var loadAvg = GetLoadAverage();
                result["load_average"] = loadAvg;
                
                result["state"] = "online";
                result["uptime_hours"] = GetUptime();
                
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[MacOSSystemResourcesSensor] Error getting system resources");
                return new Dictionary<string, object> { ["state"] = "error", ["error"] = ex.Message };
            }
        }
        
        private double GetCpuUsage()
        {
            try
            {
                // Only update every 2 seconds to reduce overhead
                if ((DateTime.Now - _lastCpuCheck).TotalSeconds < 2)
                {
                    return _lastCpuPercent;
                }
                
                // Use top command to get CPU usage
                var psi = new ProcessStartInfo("top")
                {
                    Arguments = "-l 1 -n 0 -stats cpu",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using var process = Process.Start(psi);
                if (process == null) return _lastCpuPercent;
                
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);
                
                // Parse CPU line: "CPU usage: X.X% user, Y.Y% sys, Z.Z% idle"
                var cpuLine = output.Split('\n')
                    .FirstOrDefault(l => l.Contains("CPU usage"));
                
                if (!string.IsNullOrEmpty(cpuLine))
                {
                    // Extract idle percentage
                    var idleMatch = System.Text.RegularExpressions.Regex.Match(cpuLine, @"([\d.]+)%\s*idle");
                    if (idleMatch.Success && double.TryParse(idleMatch.Groups[1].Value, out var idle))
                    {
                        _lastCpuPercent = Math.Round(100 - idle, 1);
                    }
                }
                
                _lastCpuCheck = DateTime.Now;
                return _lastCpuPercent;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[MacOSSystemResourcesSensor] Error getting CPU usage");
                return _lastCpuPercent;
            }
        }
        
        private (long totalMB, long availableMB, long usedMB, double usedPercent) GetMemoryInfo()
        {
            try
            {
                // Get total memory from sysctl
                var totalBytes = GetSysctlLong("hw.memsize");
                var totalMB = totalBytes / (1024 * 1024);
                
                // Get page size
                var pageSize = GetSysctlInt("vm.pagesize");
                if (pageSize == 0) pageSize = 4096; // Default page size
                
                // Get memory stats from vm_stat
                var vmStatOutput = ExecuteCommand("vm_stat");
                var freePages = ParseVmStatValue(vmStatOutput, "Pages free");
                var inactivePages = ParseVmStatValue(vmStatOutput, "Pages inactive");
                var speculativePages = ParseVmStatValue(vmStatOutput, "Pages speculative");
                
                // Available = free + inactive + speculative (roughly)
                var availablePages = freePages + inactivePages + speculativePages;
                var availableMB = (availablePages * pageSize) / (1024 * 1024);
                
                var usedMB = totalMB - availableMB;
                var usedPercent = totalMB > 0 ? Math.Round((double)usedMB / totalMB * 100, 1) : 0;
                
                return (totalMB, availableMB, usedMB, usedPercent);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[MacOSSystemResourcesSensor] Error getting memory info");
                return (0, 0, 0, 0);
            }
        }
        
        private string GetLoadAverage()
        {
            try
            {
                var output = ExecuteCommand("sysctl", "-n vm.loadavg");
                // Output format: { 1.23 4.56 7.89 }
                var cleaned = output.Trim().Trim('{', '}', ' ');
                var parts = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    return $"{parts[0]} {parts[1]} {parts[2]}";
                }
                return "0.00 0.00 0.00";
            }
            catch
            {
                return "0.00 0.00 0.00";
            }
        }
        
        private double GetUptime()
        {
            try
            {
                // Get boot time from sysctl
                var output = ExecuteCommand("sysctl", "-n kern.boottime");
                // Output format: { sec = 1234567890, usec = 123456 } ...
                var match = System.Text.RegularExpressions.Regex.Match(output, @"sec\s*=\s*(\d+)");
                if (match.Success && long.TryParse(match.Groups[1].Value, out var bootTimeSec))
                {
                    var bootTime = DateTimeOffset.FromUnixTimeSeconds(bootTimeSec);
                    var uptime = DateTimeOffset.Now - bootTime;
                    return Math.Round(uptime.TotalHours, 2);
                }
                return 0;
            }
            catch
            {
                return 0;
            }
        }
        
        private long GetSysctlLong(string name)
        {
            try
            {
                var output = ExecuteCommand("sysctl", $"-n {name}");
                if (long.TryParse(output.Trim(), out var value))
                {
                    return value;
                }
            }
            catch { }
            return 0;
        }
        
        private int GetSysctlInt(string name)
        {
            try
            {
                var output = ExecuteCommand("sysctl", $"-n {name}");
                if (int.TryParse(output.Trim(), out var value))
                {
                    return value;
                }
            }
            catch { }
            return 0;
        }
        
        private long ParseVmStatValue(string vmStatOutput, string key)
        {
            try
            {
                var line = vmStatOutput.Split('\n')
                    .FirstOrDefault(l => l.Contains(key));
                if (line != null)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(line, @":\s*(\d+)");
                    if (match.Success && long.TryParse(match.Groups[1].Value, out var value))
                    {
                        return value;
                    }
                }
            }
            catch { }
            return 0;
        }
        
        private string ExecuteCommand(string command, string arguments = "")
        {
            try
            {
                var psi = new ProcessStartInfo(command)
                {
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using var process = Process.Start(psi);
                if (process == null) return string.Empty;
                
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);
                return output;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
    
    /// <summary>
    /// macOS Battery sensor using pmset and ioreg
    /// </summary>
    public class MacOSBatterySensor : ISensor
    {
        public string Id { get; }
        public string Name { get; }
        
        public MacOSBatterySensor(string id = "battery", string name = "Battery")
        {
            Id = id;
            Name = name;
        }
        
        public Dictionary<string, object> GetState()
        {
            try
            {
                var result = new Dictionary<string, object>();
                
                // Use pmset -g batt for battery info
                var output = ExecuteCommand("pmset", "-g batt");
                
                if (string.IsNullOrWhiteSpace(output) || output.Contains("No batteries"))
                {
                    result["state"] = "unavailable";
                    result["has_battery"] = false;
                    return result;
                }
                
                result["has_battery"] = true;
                
                // Parse battery percentage
                var percentMatch = System.Text.RegularExpressions.Regex.Match(output, @"(\d+)%");
                if (percentMatch.Success && int.TryParse(percentMatch.Groups[1].Value, out var percent))
                {
                    result["percent"] = percent;
                    result["state"] = percent.ToString();
                }
                
                // Parse charging state
                if (output.Contains("AC Power"))
                {
                    result["power_source"] = "ac";
                    result["is_charging"] = output.Contains("charging") && !output.Contains("not charging");
                }
                else if (output.Contains("Battery Power"))
                {
                    result["power_source"] = "battery";
                    result["is_charging"] = false;
                }
                
                // Parse time remaining
                var timeMatch = System.Text.RegularExpressions.Regex.Match(output, @"(\d+):(\d+)\s*remaining");
                if (timeMatch.Success)
                {
                    var hours = int.Parse(timeMatch.Groups[1].Value);
                    var minutes = int.Parse(timeMatch.Groups[2].Value);
                    result["time_remaining_minutes"] = hours * 60 + minutes;
                }
                
                // Check if fully charged
                if (output.Contains("charged"))
                {
                    result["is_fully_charged"] = true;
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[MacOSBatterySensor] Error getting battery info");
                return new Dictionary<string, object> { ["state"] = "error", ["error"] = ex.Message };
            }
        }
        
        private string ExecuteCommand(string command, string arguments = "")
        {
            try
            {
                var psi = new ProcessStartInfo(command)
                {
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using var process = Process.Start(psi);
                if (process == null) return string.Empty;
                
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);
                return output;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
    
    /// <summary>
    /// macOS Display sensor using system_profiler
    /// </summary>
    public class MacOSDisplaySensor : ISensor
    {
        public string Id { get; }
        public string Name { get; }
        
        public MacOSDisplaySensor(string id = "display", string name = "Display")
        {
            Id = id;
            Name = name;
        }
        
        public Dictionary<string, object> GetState()
        {
            try
            {
                var result = new Dictionary<string, object>();
                
                // Use system_profiler for display info
                var output = ExecuteCommand("system_profiler", "SPDisplaysDataType -json");
                
                if (string.IsNullOrWhiteSpace(output))
                {
                    result["state"] = "unavailable";
                    return result;
                }
                
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(output);
                    var displays = new List<object>();
                    
                    if (doc.RootElement.TryGetProperty("SPDisplaysDataType", out var displayData))
                    {
                        foreach (var gpu in displayData.EnumerateArray())
                        {
                            if (gpu.TryGetProperty("spdisplays_ndrvs", out var monitorsArray))
                            {
                                foreach (var monitor in monitorsArray.EnumerateArray())
                                {
                                    var displayInfo = new Dictionary<string, object>();
                                    
                                    if (monitor.TryGetProperty("_name", out var name))
                                        displayInfo["name"] = name.GetString() ?? "Unknown";
                                    
                                    if (monitor.TryGetProperty("_spdisplays_resolution", out var res))
                                        displayInfo["resolution"] = res.GetString() ?? "Unknown";
                                    
                                    if (monitor.TryGetProperty("spdisplays_main", out var isMain))
                                        displayInfo["is_main"] = isMain.GetString() == "spdisplays_yes";
                                    
                                    displays.Add(displayInfo);
                                }
                            }
                        }
                    }
                    
                    result["displays"] = displays;
                    result["display_count"] = displays.Count;
                    result["state"] = displays.Count > 0 ? "connected" : "disconnected";
                }
                catch
                {
                    result["state"] = "error";
                    result["raw_output"] = output.Length > 500 ? output.Substring(0, 500) : output;
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[MacOSDisplaySensor] Error getting display info");
                return new Dictionary<string, object> { ["state"] = "error", ["error"] = ex.Message };
            }
        }
        
        private string ExecuteCommand(string command, string arguments = "")
        {
            try
            {
                var psi = new ProcessStartInfo(command)
                {
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using var process = Process.Start(psi);
                if (process == null) return string.Empty;
                
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(10000); // system_profiler can be slow
                return output;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
    
    /// <summary>
    /// macOS Active App sensor using AppleScript
    /// </summary>
    public class MacOSActiveAppSensor : ISensor
    {
        public string Id { get; }
        public string Name { get; }
        
        public MacOSActiveAppSensor(string id = "active_app", string name = "Active Application")
        {
            Id = id;
            Name = name;
        }
        
        public Dictionary<string, object> GetState()
        {
            try
            {
                var result = new Dictionary<string, object>();
                
                // Use AppleScript to get frontmost application
                // Note: Arguments passed directly without shell interpretation
                var output = ExecuteAppleScript("tell application \"System Events\" to get name of first application process whose frontmost is true");
                
                var appName = output.Trim();
                if (!string.IsNullOrEmpty(appName))
                {
                    result["state"] = appName;
                    result["app_name"] = appName;
                }
                else
                {
                    result["state"] = "unknown";
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[MacOSActiveAppSensor] Error getting active app");
                return new Dictionary<string, object> { ["state"] = "error", ["error"] = ex.Message };
            }
        }
        
        private string ExecuteAppleScript(string script)
        {
            try
            {
                var psi = new ProcessStartInfo("osascript")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using var process = Process.Start(psi);
                if (process == null) return string.Empty;
                
                // Write script to stdin to avoid shell escaping issues
                process.StandardInput.WriteLine(script);
                process.StandardInput.Close();
                
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);
                return output;
            }
            catch
            {
                return string.Empty;
            }
        }
        
        private string ExecuteCommand(string command, string arguments = "")
        {
            try
            {
                var psi = new ProcessStartInfo(command)
                {
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using var process = Process.Start(psi);
                if (process == null) return string.Empty;
                
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);
                return output;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
    
    /// <summary>
    /// macOS Screen Brightness sensor
    /// </summary>
    public class MacOSBrightnessSensor : ISensor
    {
        public string Id { get; }
        public string Name { get; }
        
        public MacOSBrightnessSensor(string id = "brightness", string name = "Screen Brightness")
        {
            Id = id;
            Name = name;
        }
        
        public Dictionary<string, object> GetState()
        {
            try
            {
                var result = new Dictionary<string, object>();
                
                // Try using brightness command if available, or ioreg
                var output = ExecuteCommand("ioreg", "-rc AppleBacklightDisplay");
                
                if (string.IsNullOrWhiteSpace(output))
                {
                    // Try AppleDisplay for external monitors
                    output = ExecuteCommand("ioreg", "-rc AppleDisplay");
                }
                
                if (string.IsNullOrWhiteSpace(output))
                {
                    result["state"] = "unavailable";
                    return result;
                }
                
                // Parse brightness from IODisplayParameters
                var brightnessMatch = System.Text.RegularExpressions.Regex.Match(output, "\"brightness\".*?\"value\"=(\\d+)");
                var maxMatch = System.Text.RegularExpressions.Regex.Match(output, "\"brightness\".*?\"max\"=(\\d+)");
                
                if (brightnessMatch.Success && maxMatch.Success)
                {
                    var current = int.Parse(brightnessMatch.Groups[1].Value);
                    var max = int.Parse(maxMatch.Groups[1].Value);
                    var percent = max > 0 ? (int)Math.Round((double)current / max * 100) : 0;
                    
                    result["brightness_raw"] = current;
                    result["brightness_max"] = max;
                    result["brightness_percent"] = percent;
                    result["state"] = percent.ToString();
                }
                else
                {
                    result["state"] = "unavailable";
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[MacOSBrightnessSensor] Error getting brightness");
                return new Dictionary<string, object> { ["state"] = "error", ["error"] = ex.Message };
            }
        }
        
        private string ExecuteCommand(string command, string arguments = "")
        {
            try
            {
                var psi = new ProcessStartInfo(command)
                {
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using var process = Process.Start(psi);
                if (process == null) return string.Empty;
                
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);
                return output;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
