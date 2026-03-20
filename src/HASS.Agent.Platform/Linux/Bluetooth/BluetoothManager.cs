using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Serilog;

namespace HASS.Agent.Platform.Linux.Bluetooth
{
    public class BluetoothManager
    {
        private readonly BluezManager? _bluez;

        public BluetoothManager()
        {
            Log.Information("[Bluetooth] Linux BluetoothManager initialized (BlueZ/bluetoothctl)");
            try { _bluez = new BluezManager(); } catch { }
        }

        public void StartScan()
        {
            Log.Information("[Bluetooth] Start scanning (bluetoothctl)");
            try
            {
                if (_bluez != null && _bluez.DbUsAvailable)
                {
                    var adapters = _bluez.ListAdaptersAsync().GetAwaiter().GetResult();
                    if (adapters.Length > 0)
                    {
                        Log.Information("[BlueZ] adapters found: {0}", string.Join(",", adapters));
                    }
                }

                RunBluetoothctl("scan on");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Bluetooth] bluetoothctl scan on failed");
            }
        }

        public void StopScan()
        {
            Log.Information("[Bluetooth] Stop scanning (bluetoothctl)");
            try
            {
                RunBluetoothctl("scan off");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Bluetooth] bluetoothctl scan off failed");
            }
        }

        public async Task<List<BluetoothDevice>> GetPairedDevicesAsync()
        {
            var devices = new List<BluetoothDevice>();
            
            try
            {
                var output = await ExecuteBluetoothctlAsync("paired-devices");
                if (!string.IsNullOrEmpty(output))
                {
                    foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    {
                        // Format: "Device XX:XX:XX:XX:XX:XX DeviceName"
                        var parts = line.Split(' ', 3);
                        if (parts.Length >= 3 && parts[0] == "Device")
                        {
                            devices.Add(new BluetoothDevice
                            {
                                MacAddress = parts[1],
                                Name = parts[2],
                                IsPaired = true
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Bluetooth] Error getting paired devices");
            }
            
            return devices;
        }

        public async Task<List<BluetoothDevice>> GetConnectedDevicesAsync()
        {
            var devices = new List<BluetoothDevice>();
            
            try
            {
                var pairedDevices = await GetPairedDevicesAsync();
                
                foreach (var device in pairedDevices)
                {
                    var info = await ExecuteBluetoothctlAsync($"info {device.MacAddress}");
                    if (info?.Contains("Connected: yes") == true)
                    {
                        device.IsConnected = true;
                        devices.Add(device);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Bluetooth] Error getting connected devices");
            }
            
            return devices;
        }

        public async Task<bool> ConnectAsync(string macAddress)
        {
            try
            {
                var result = await ExecuteBluetoothctlAsync($"connect {macAddress}");
                return result?.Contains("Connection successful") == true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Bluetooth] Error connecting to {mac}", macAddress);
                return false;
            }
        }

        public async Task<bool> DisconnectAsync(string macAddress)
        {
            try
            {
                var result = await ExecuteBluetoothctlAsync($"disconnect {macAddress}");
                return result?.Contains("Successful disconnected") == true || 
                       result?.Contains("not connected") == true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Bluetooth] Error disconnecting from {mac}", macAddress);
                return false;
            }
        }

        private static async Task<string?> ExecuteBluetoothctlAsync(string args)
        {
            try
            {
                using var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = "bluetoothctl",
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
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

        private static void RunBluetoothctl(string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "bluetoothctl",
                Arguments = args,
                UseShellExecute = false
            };
            using var process = Process.Start(psi);
            process?.Dispose();
        }

        public static bool IsAvailable()
        {
            try
            {
                var which = Process.Start(new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = "bluetoothctl",
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
    }

    public class BluetoothDevice
    {
        public string MacAddress { get; set; } = "";
        public string Name { get; set; } = "";
        public bool IsPaired { get; set; }
        public bool IsConnected { get; set; }
    }
}
