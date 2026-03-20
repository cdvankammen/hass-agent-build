using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using HASS.Agent.Platform.Abstractions;
using Serilog;

namespace HASS.Agent.Platform.Linux.Sensors
{
    public class DiskUsageSensor : ISensor
    {
        public string Id { get; }
        public string Name { get; }
        
        public DiskUsageSensor(string id = "disk_usage", string name = "Disk Usage")
        {
            Id = id;
            Name = name;
        }
        
        public Dictionary<string, object> GetState()
        {
            try
            {
                var result = new Dictionary<string, object>();
                var drives = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady);
                
                var disks = new List<object>();
                foreach (var drive in drives)
                {
                    try
                    {
                        var totalGB = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                        var freeGB = drive.TotalFreeSpace / (1024.0 * 1024.0 * 1024.0);
                        var usedGB = totalGB - freeGB;
                        var usedPercent = totalGB > 0 ? (usedGB / totalGB) * 100 : 0;
                        
                        disks.Add(new
                        {
                            name = drive.Name.TrimEnd('/'),
                            total_gb = Math.Round(totalGB, 2),
                            free_gb = Math.Round(freeGB, 2),
                            used_gb = Math.Round(usedGB, 2),
                            used_percent = Math.Round(usedPercent, 1),
                            filesystem = drive.DriveFormat
                        });
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "[DiskSensor] Error reading drive {drive}", drive.Name);
                    }
                }
                
                result["disks"] = disks;
                result["state"] = disks.Count > 0 ? "online" : "offline";
                result["total_disks"] = disks.Count;
                
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DiskSensor] Error getting disk usage");
                return new Dictionary<string, object> { ["state"] = "error", ["error"] = ex.Message };
            }
        }
    }
    
    public class NetworkInterfacesSensor : ISensor
    {
        public string Id { get; }
        public string Name { get; }
        
        public NetworkInterfacesSensor(string id = "network_interfaces", string name = "Network Interfaces")
        {
            Id = id;
            Name = name;
        }
        
        public Dictionary<string, object> GetState()
        {
            try
            {
                var result = new Dictionary<string, object>();
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .ToList();
                
                var nics = new List<object>();
                foreach (var ni in interfaces)
                {
                    try
                    {
                        var props = ni.GetIPProperties();
                        var ipv4 = props.UnicastAddresses
                            .Where(ua => ua.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            .Select(ua => ua.Address.ToString())
                            .ToList();
                        
                        var ipv6 = props.UnicastAddresses
                            .Where(ua => ua.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                            .Select(ua => ua.Address.ToString())
                            .ToList();
                        
                        nics.Add(new
                        {
                            name = ni.Name,
                            description = ni.Description,
                            type = ni.NetworkInterfaceType.ToString(),
                            status = ni.OperationalStatus.ToString(),
                            speed_mbps = ni.Speed > 0 ? ni.Speed / 1_000_000 : 0,
                            ipv4_addresses = ipv4,
                            ipv6_addresses = ipv6,
                            mac_address = ni.GetPhysicalAddress().ToString(),
                            is_up = ni.OperationalStatus == OperationalStatus.Up
                        });
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "[NetworkSensor] Error reading interface {name}", ni.Name);
                    }
                }
                
                result["interfaces"] = nics;
                result["state"] = nics.Any(n => (bool)((dynamic)n).is_up) ? "connected" : "disconnected";
                result["total_interfaces"] = nics.Count;
                result["active_interfaces"] = nics.Count(n => (bool)((dynamic)n).is_up);
                
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[NetworkSensor] Error getting network interfaces");
                return new Dictionary<string, object> { ["state"] = "error", ["error"] = ex.Message };
            }
        }
    }
    
    public class SystemResourcesSensor : ISensor
    {
        public string Id { get; }
        public string Name { get; }
        
        // Store previous CPU values for delta calculation
        private long _prevIdle;
        private long _prevTotal;
        private double _lastCpuPercent;
        
        public SystemResourcesSensor(string id = "system_resources", string name = "System Resources")
        {
            Id = id;
            Name = name;
        }
        
        public Dictionary<string, object> GetState()
        {
            try
            {
                var result = new Dictionary<string, object>();
                
                // CPU info from /proc/stat (delta-based)
                var cpuPercent = GetCpuUsage();
                result["cpu_percent"] = cpuPercent;
                
                // Memory from /proc/meminfo
                var memInfo = GetMemoryInfo();
                result.Add("memory_total_mb", memInfo.totalMB);
                result.Add("memory_available_mb", memInfo.availableMB);
                result.Add("memory_used_mb", memInfo.usedMB);
                result.Add("memory_percent", memInfo.usedPercent);
                
                // Load average from /proc/loadavg
                var loadAvg = GetLoadAverage();
                result["load_average"] = loadAvg;
                
                result["state"] = "online";
                result["uptime_hours"] = GetUptime();
                
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[SystemResourcesSensor] Error getting system resources");
                return new Dictionary<string, object> { ["state"] = "error", ["error"] = ex.Message };
            }
        }
        
        private double GetCpuUsage()
        {
            try
            {
                if (!OperatingSystem.IsLinux()) return 0;
                
                var stat = File.ReadAllText("/proc/stat").Split('\n')[0];
                var values = stat.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (values.Length < 5) return 0;
                
                // Parse CPU values: user, nice, system, idle, iowait, irq, softirq
                var user = long.Parse(values[1]);
                var nice = long.Parse(values[2]);
                var system = long.Parse(values[3]);
                var idle = long.Parse(values[4]);
                var iowait = values.Length > 5 ? long.Parse(values[5]) : 0;
                var irq = values.Length > 6 ? long.Parse(values[6]) : 0;
                var softirq = values.Length > 7 ? long.Parse(values[7]) : 0;
                
                var totalIdle = idle + iowait;
                var total = user + nice + system + idle + iowait + irq + softirq;
                
                // Calculate delta from previous reading
                if (_prevTotal > 0)
                {
                    var deltaTotal = total - _prevTotal;
                    var deltaIdle = totalIdle - _prevIdle;
                    
                    if (deltaTotal > 0)
                    {
                        _lastCpuPercent = Math.Round((1.0 - (double)deltaIdle / deltaTotal) * 100, 1);
                    }
                }
                
                // Store current values for next delta
                _prevTotal = total;
                _prevIdle = totalIdle;
                
                return _lastCpuPercent;
            }
            catch
            {
                return 0;
            }
        }
        
        private (long totalMB, long availableMB, long usedMB, double usedPercent) GetMemoryInfo()
        {
            try
            {
                if (!OperatingSystem.IsLinux()) return (0, 0, 0, 0);
                
                var meminfo = File.ReadAllLines("/proc/meminfo")
                    .Where(line => line.StartsWith("MemTotal:") || line.StartsWith("MemAvailable:"))
                    .ToList();
                
                var total = 0L;
                var available = 0L;
                
                foreach (var line in meminfo)
                {
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && long.TryParse(parts[1], out var value))
                    {
                        if (line.StartsWith("MemTotal:")) total = value / 1024; // Convert KB to MB
                        if (line.StartsWith("MemAvailable:")) available = value / 1024;
                    }
                }
                
                var used = total - available;
                var usedPercent = total > 0 ? (double)used / total * 100 : 0;
                
                return (total, available, used, Math.Round(usedPercent, 1));
            }
            catch
            {
                return (0, 0, 0, 0);
            }
        }
        
        private string GetLoadAverage()
        {
            try
            {
                if (!OperatingSystem.IsLinux()) return "0.00 0.00 0.00";
                
                var loadavg = File.ReadAllText("/proc/loadavg").Trim();
                var parts = loadavg.Split(' ');
                return parts.Length >= 3 ? $"{parts[0]} {parts[1]} {parts[2]}" : "0.00 0.00 0.00";
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
                if (!OperatingSystem.IsLinux()) return 0;
                
                var uptime = File.ReadAllText("/proc/uptime").Trim().Split(' ')[0];
                if (double.TryParse(uptime, out var seconds))
                {
                    return Math.Round(seconds / 3600, 2); // Convert to hours
                }
            }
            catch { }
            
            return 0;
        }
    }
    
    /// <summary>
    /// Battery sensor for laptops (reads from /sys/class/power_supply)
    /// </summary>
    public class BatterySensor : ISensor
    {
        public string Id { get; }
        public string Name { get; }
        
        public BatterySensor(string id = "battery", string name = "Battery")
        {
            Id = id;
            Name = name;
        }
        
        public Dictionary<string, object> GetState()
        {
            try
            {
                var result = new Dictionary<string, object>();
                
                if (!OperatingSystem.IsLinux())
                {
                    result["state"] = "unavailable";
                    return result;
                }
                
                var batteryPath = "/sys/class/power_supply/BAT0";
                if (!Directory.Exists(batteryPath))
                {
                    batteryPath = "/sys/class/power_supply/BAT1";
                }
                
                if (!Directory.Exists(batteryPath))
                {
                    result["state"] = "no_battery";
                    result["present"] = false;
                    return result;
                }
                
                var capacity = ReadFileAsInt(Path.Combine(batteryPath, "capacity"));
                var status = ReadFileAsString(Path.Combine(batteryPath, "status"));
                var energyFull = ReadFileAsLong(Path.Combine(batteryPath, "energy_full"));
                var energyNow = ReadFileAsLong(Path.Combine(batteryPath, "energy_now"));
                var powerNow = ReadFileAsLong(Path.Combine(batteryPath, "power_now"));
                
                result["state"] = capacity.ToString();
                result["capacity_percent"] = capacity;
                result["status"] = status ?? "Unknown";
                result["charging"] = status?.Equals("Charging", StringComparison.OrdinalIgnoreCase) ?? false;
                result["present"] = true;
                
                if (energyFull > 0 && energyNow > 0)
                {
                    result["energy_wh"] = Math.Round(energyNow / 1000000.0, 2);
                    result["energy_full_wh"] = Math.Round(energyFull / 1000000.0, 2);
                }
                
                if (powerNow > 0)
                {
                    result["power_w"] = Math.Round(powerNow / 1000000.0, 2);
                    
                    // Estimate time remaining
                    if (status?.Equals("Discharging", StringComparison.OrdinalIgnoreCase) ?? false)
                    {
                        var hoursRemaining = energyNow / (double)powerNow;
                        result["time_remaining_hours"] = Math.Round(hoursRemaining, 2);
                    }
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[BatterySensor] Error getting battery state");
                return new Dictionary<string, object> { ["state"] = "error", ["error"] = ex.Message };
            }
        }
        
        private static int ReadFileAsInt(string path)
        {
            try
            {
                if (File.Exists(path) && int.TryParse(File.ReadAllText(path).Trim(), out var value))
                    return value;
            }
            catch { }
            return 0;
        }
        
        private static long ReadFileAsLong(string path)
        {
            try
            {
                if (File.Exists(path) && long.TryParse(File.ReadAllText(path).Trim(), out var value))
                    return value;
            }
            catch { }
            return 0;
        }
        
        private static string? ReadFileAsString(string path)
        {
            try
            {
                if (File.Exists(path))
                    return File.ReadAllText(path).Trim();
            }
            catch { }
            return null;
        }
    }
    
    /// <summary>
    /// Temperature sensors (reads from /sys/class/thermal and /sys/class/hwmon)
    /// </summary>
    public class TemperatureSensor : ISensor
    {
        public string Id { get; }
        public string Name { get; }
        
        public TemperatureSensor(string id = "temperature", string name = "Temperature")
        {
            Id = id;
            Name = name;
        }
        
        public Dictionary<string, object> GetState()
        {
            try
            {
                var result = new Dictionary<string, object>();
                var temps = new List<object>();
                
                if (!OperatingSystem.IsLinux())
                {
                    result["state"] = "unavailable";
                    return result;
                }
                
                // Read from thermal zones
                var thermalPath = "/sys/class/thermal";
                if (Directory.Exists(thermalPath))
                {
                    foreach (var zone in Directory.GetDirectories(thermalPath, "thermal_zone*"))
                    {
                        try
                        {
                            var typePath = Path.Combine(zone, "type");
                            var tempPath = Path.Combine(zone, "temp");
                            
                            if (File.Exists(tempPath) && long.TryParse(File.ReadAllText(tempPath).Trim(), out var temp))
                            {
                                var type = File.Exists(typePath) ? File.ReadAllText(typePath).Trim() : Path.GetFileName(zone);
                                temps.Add(new
                                {
                                    name = type,
                                    temp_c = Math.Round(temp / 1000.0, 1),
                                    source = "thermal_zone"
                                });
                            }
                        }
                        catch { }
                    }
                }
                
                // Read from hwmon (for more detailed hardware sensors)
                var hwmonPath = "/sys/class/hwmon";
                if (Directory.Exists(hwmonPath))
                {
                    foreach (var device in Directory.GetDirectories(hwmonPath))
                    {
                        try
                        {
                            var deviceName = File.Exists(Path.Combine(device, "name")) 
                                ? File.ReadAllText(Path.Combine(device, "name")).Trim() 
                                : Path.GetFileName(device);
                            
                            foreach (var tempFile in Directory.GetFiles(device, "temp*_input"))
                            {
                                if (long.TryParse(File.ReadAllText(tempFile).Trim(), out var temp))
                                {
                                    temps.Add(new
                                    {
                                        name = $"{deviceName}_{Path.GetFileNameWithoutExtension(tempFile)}",
                                        temp_c = Math.Round(temp / 1000.0, 1),
                                        source = "hwmon"
                                    });
                                }
                            }
                        }
                        catch { }
                    }
                }
                
                result["temperatures"] = temps;
                result["state"] = temps.Count > 0 ? "online" : "no_sensors";
                result["sensor_count"] = temps.Count;
                
                // Set highest temp as main state
                if (temps.Count > 0)
                {
                    var maxTemp = temps.Cast<dynamic>().Max(t => (double)t.temp_c);
                    result["max_temp_c"] = maxTemp;
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[TemperatureSensor] Error getting temperature state");
                return new Dictionary<string, object> { ["state"] = "error", ["error"] = ex.Message };
            }
        }
    }
    
    /// <summary>
    /// Active window sensor (uses xdotool on X11)
    /// </summary>
    public class ActiveWindowSensor : ISensor
    {
        public string Id { get; }
        public string Name { get; }
        
        public ActiveWindowSensor(string id = "active_window", string name = "Active Window")
        {
            Id = id;
            Name = name;
        }
        
        public Dictionary<string, object> GetState()
        {
            try
            {
                var result = new Dictionary<string, object>();
                
                if (!OperatingSystem.IsLinux())
                {
                    result["state"] = "unavailable";
                    return result;
                }
                
                var windowTitle = ExecuteCommand("xdotool", "getactivewindow getwindowname");
                var windowClass = ExecuteCommand("xdotool", "getactivewindow getwindowclassname");
                var windowPid = ExecuteCommand("xdotool", "getactivewindow getwindowpid");
                
                if (string.IsNullOrEmpty(windowTitle) && string.IsNullOrEmpty(windowClass))
                {
                    result["state"] = "unavailable";
                    result["available"] = false;
                    return result;
                }
                
                result["state"] = windowTitle ?? "unknown";
                result["title"] = windowTitle ?? "";
                result["class"] = windowClass ?? "";
                result["pid"] = int.TryParse(windowPid, out var pid) ? pid : 0;
                result["available"] = true;
                
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ActiveWindowSensor] Error getting active window");
                return new Dictionary<string, object> { ["state"] = "error", ["error"] = ex.Message };
            }
        }
        
        private static string? ExecuteCommand(string filename, string args)
        {
            try
            {
                using var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = filename,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                });
                
                if (proc != null)
                {
                    var output = proc.StandardOutput.ReadToEnd().Trim();
                    proc.WaitForExit(1000);
                    return proc.ExitCode == 0 ? output : null;
                }
            }
            catch { }
            return null;
        }
    }
    
    /// <summary>
    /// User sessions sensor (reads from who command)
    /// </summary>
    public class UserSessionsSensor : ISensor
    {
        public string Id { get; }
        public string Name { get; }
        
        public UserSessionsSensor(string id = "user_sessions", string name = "User Sessions")
        {
            Id = id;
            Name = name;
        }
        
        public Dictionary<string, object> GetState()
        {
            try
            {
                var result = new Dictionary<string, object>();
                
                if (!OperatingSystem.IsLinux())
                {
                    result["state"] = "unavailable";
                    return result;
                }
                
                // Get current user
                result["current_user"] = Environment.UserName;
                
                // Get logged in users
                try
                {
                    using var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "who",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    });
                    
                    if (proc != null)
                    {
                        var output = proc.StandardOutput.ReadToEnd();
                        proc.WaitForExit(1000);
                        
                        var sessions = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                            .Select(line =>
                            {
                                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                return new
                                {
                                    user = parts.Length > 0 ? parts[0] : "unknown",
                                    terminal = parts.Length > 1 ? parts[1] : "unknown",
                                    login_time = parts.Length > 2 ? string.Join(" ", parts.Skip(2).Take(2)) : "unknown"
                                };
                            })
                            .ToList();
                        
                        result["sessions"] = sessions;
                        result["session_count"] = sessions.Count;
                        result["state"] = sessions.Count.ToString();
                    }
                }
                catch
                {
                    result["session_count"] = 0;
                    result["state"] = "0";
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[UserSessionsSensor] Error getting user sessions");
                return new Dictionary<string, object> { ["state"] = "error", ["error"] = ex.Message };
            }
        }
    }
}