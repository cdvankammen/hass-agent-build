using System;
using System.IO;
using Serilog;

namespace HASS.Agent.Platform.Linux.SystemMetrics
{
    public static class SystemMetricsAdapter
    {
        public static double GetCpuUsagePercent()
        {
            try
            {
                // Read /proc/stat twice and compute delta to approximate CPU usage
                string[] ReadStat()
                {
                    var lines = File.ReadAllLines("/proc/stat");
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("cpu ")) return line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    }

                    return Array.Empty<string>();
                }

                var a = ReadStat();
                if (a == null || a.Length == 0) return -1;
                System.Threading.Thread.Sleep(360);
                var b = ReadStat();
                if (b == null || b.Length == 0) return -1;

                long ParseFields(string[] arr, int idx)
                {
                    if (arr.Length > idx && long.TryParse(arr[idx], out var v)) return v;
                    return 0;
                }

                // fields: cpu user nice system idle iowait irq softirq steal guest guest_nice
                long userA = ParseFields(a, 1);
                long niceA = ParseFields(a, 2);
                long systemA = ParseFields(a, 3);
                long idleA = ParseFields(a, 4);
                long iowaitA = ParseFields(a, 5);
                long irqA = ParseFields(a, 6);
                long softirqA = ParseFields(a, 7);
                long stealA = ParseFields(a, 8);

                long userB = ParseFields(b, 1);
                long niceB = ParseFields(b, 2);
                long systemB = ParseFields(b, 3);
                long idleB = ParseFields(b, 4);
                long iowaitB = ParseFields(b, 5);
                long irqB = ParseFields(b, 6);
                long softirqB = ParseFields(b, 7);
                long stealB = ParseFields(b, 8);

                long idleTimeA = idleA + iowaitA;
                long idleTimeB = idleB + iowaitB;

                long nonIdleA = userA + niceA + systemA + irqA + softirqA + stealA;
                long nonIdleB = userB + niceB + systemB + irqB + softirqB + stealB;

                long totalA = idleTimeA + nonIdleA;
                long totalB = idleTimeB + nonIdleB;

                var totald = totalB - totalA;
                var idled = idleTimeB - idleTimeA;

                if (totald == 0) return 0;

                var cpuPerc = (double)(totald - idled) / totald * 100.0;
                return Math.Round(cpuPerc, 2);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[PLATFORM.LINUX][SYSMET] Error reading CPU usage");
                return -1;
            }
        }

        public static long GetAvailableMemoryBytes()
        {
            try
            {
                if (!File.Exists("/proc/meminfo")) return -1;
                var lines = File.ReadAllLines("/proc/meminfo");
                foreach (var line in lines)
                {
                    if (line.StartsWith("MemAvailable:"))
                    {
                        var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2 && long.TryParse(parts[1], out var kb)) return kb * 1024;
                    }
                }

                return -1;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[PLATFORM.LINUX][SYSMET] Error reading meminfo");
                return -1;
            }
        }
    }
}
