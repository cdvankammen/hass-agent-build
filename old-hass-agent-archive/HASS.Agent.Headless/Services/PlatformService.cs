using System.Diagnostics;
using HASS.Agent.Core;
using HASS.Agent.Platform;
using HASS.Agent.Platform.Linux;
using HASS.Agent.Platform.Linux.Media;
using HASS.Agent.Platform.Linux.Bluetooth;
using Serilog;

namespace HASS.Agent.Headless.Services
{
    /// <summary>
    /// Manages platform-specific adapters (media, bluetooth, notifications, sensors)
    /// </summary>
    public class PlatformService : IDisposable
    {
        private readonly ConfigurationService _config;
        private readonly IMqttManager _mqtt;
        
        private MediaManager? _mediaManager;
        private BluetoothManager? _bluetoothManager;
        private PlatformSensorManager? _sensorManager;
        private INotifier? _notifier;
        
        private bool _mediaEnabled;
        private bool _disposed;
        
        public PlatformService(ConfigurationService config, IMqttManager mqtt)
        {
            _config = config;
            _mqtt = mqtt;
        }
        
        public MediaManager? MediaManager => _mediaManager;
        public BluetoothManager? BluetoothManager => _bluetoothManager;
        public PlatformSensorManager? SensorManager => _sensorManager;
        public INotifier? Notifier => _notifier;
        public bool MediaEnabled => _mediaEnabled;
        
        /// <summary>
        /// Initialize all platform adapters based on configuration
        /// </summary>
        public void Initialize()
        {
            var autoStart = GetEnvBool("HASS_AGENT_AUTO_START_PLATFORM", false) ?? false;
            
            InitializeMedia(autoStart);
            InitializeBluetooth(autoStart);
            InitializeSensors();
            InitializeNotifier();
            
            Log.Information("[PLATFORM] Platform services initialized");
        }
        
        private void InitializeMedia(bool autoStart)
        {
            try
            {
                var configuredMedia = _config.ReadConfiguredBool("MediaPlayerEnabled", true);
                var envMedia = GetEnvBool("HASS_AGENT_ENABLE_MEDIA", null);
                _mediaEnabled = envMedia ?? configuredMedia;
                
                if (envMedia.HasValue)
                {
                    Log.Information("[PLATFORM] Media enabled overridden by env: {val}", envMedia);
                }
                
                if (_mediaEnabled || autoStart)
                {
                    _mediaManager = new MediaManager();
                    var playerctlAvailable = MediaManager.IsAvailable();
                    
                    if (playerctlAvailable)
                    {
                        _mediaManager.Start();
                        Log.Information("[PLATFORM] MediaManager started");
                    }
                    else
                    {
                        Log.Warning("[PLATFORM] playerctl not found, MediaManager features limited");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[PLATFORM] MediaManager failed to start");
            }
        }
        
        private void InitializeBluetooth(bool autoStart)
        {
            try
            {
                if (autoStart)
                {
                    _bluetoothManager = new BluetoothManager();
                    
                    if (BluetoothManager.IsAvailable())
                    {
                        _bluetoothManager.StartScan();
                        Log.Information("[PLATFORM] BluetoothManager started");
                    }
                    else
                    {
                        Log.Warning("[PLATFORM] bluetoothctl not found, Bluetooth features limited");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[PLATFORM] BluetoothManager failed to start");
            }
        }
        
        private void InitializeSensors()
        {
            try
            {
                var deviceName = _config.ReadConfiguredString("DeviceName", "hass-agent") ?? "hass-agent";
                _sensorManager = new PlatformSensorManager(_mqtt, deviceName);
                _sensorManager.Start();
                Log.Information("[PLATFORM] Platform sensors started");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[PLATFORM] Failed to start platform sensors");
            }
        }
        
        private void InitializeNotifier()
        {
            try
            {
                _notifier = PlatformFactory.GetNotifier();
                if (_notifier != null)
                {
                    Log.Information("[PLATFORM] Notifier initialized");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[PLATFORM] Failed to initialize notifier");
            }
        }
        
        /// <summary>
        /// Send a notification if notifier is available
        /// </summary>
        public void Notify(string title, string message)
        {
            try
            {
                _notifier?.Notify(title, message);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[PLATFORM] Failed to send notification");
            }
        }
        
        /// <summary>
        /// Get current sensor data from all platform sensors
        /// </summary>
        public List<object> GetCurrentSensorData()
        {
            return _sensorManager?.GetCurrentSensorData() ?? new List<object>();
        }
        
        /// <summary>
        /// Check platform capabilities
        /// </summary>
        public PlatformCapabilities GetCapabilities()
        {
            var caps = new PlatformCapabilities();
            
            // Check MPRIS DBus
            try
            {
                caps.MprisDbusAvailable = _mediaManager != null;
            }
            catch { }
            
            // Check BlueZ DBus
            try
            {
                caps.BluezDbusAvailable = _bluetoothManager != null;
            }
            catch { }
            
            // Check playerctl
            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = "playerctl",
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                });
                if (p != null)
                {
                    p.WaitForExit(200);
                    caps.PlayerctlAvailable = p.ExitCode == 0;
                }
            }
            catch { }
            
            // Check bluetoothctl
            try
            {
                using var b = Process.Start(new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = "bluetoothctl",
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                });
                if (b != null)
                {
                    b.WaitForExit(200);
                    caps.BluetoothctlAvailable = b.ExitCode == 0;
                }
            }
            catch { }
            
            caps.MediaEnabled = _mediaEnabled;
            
            return caps;
        }
        
        /// <summary>
        /// Reload configuration and restart services as needed
        /// </summary>
        public void ReloadConfiguration()
        {
            Log.Information("[PLATFORM] Reloading configuration...");
            
            var newMediaEnabled = _config.ReadConfiguredBool("MediaPlayerEnabled", true);
            
            if (newMediaEnabled != _mediaEnabled)
            {
                _mediaEnabled = newMediaEnabled;
                
                if (_mediaEnabled && _mediaManager == null)
                {
                    InitializeMedia(false);
                }
                else if (!_mediaEnabled && _mediaManager != null)
                {
                    _mediaManager.Stop();
                    _mediaManager = null;
                }
            }
            
            // Update device name for sensors
            var deviceName = _config.ReadConfiguredString("DeviceName", "hass-agent") ?? "hass-agent";
            if (_sensorManager != null)
            {
                _sensorManager.Stop();
                _sensorManager = new PlatformSensorManager(_mqtt, deviceName);
                _sensorManager.Start();
            }
            
            Log.Information("[PLATFORM] Configuration reloaded");
        }
        
        private static bool? GetEnvBool(string name, bool? defaultValue)
        {
            var val = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(val)) return defaultValue;
            return val == "1" || val.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
        
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            try
            {
                _mediaManager?.Stop();
                _bluetoothManager?.StopScan();
                _sensorManager?.Stop();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[PLATFORM] Error during disposal");
            }
        }
    }
    
    public class PlatformCapabilities
    {
        public bool MprisDbusAvailable { get; set; }
        public bool BluezDbusAvailable { get; set; }
        public bool PlayerctlAvailable { get; set; }
        public bool BluetoothctlAvailable { get; set; }
        public bool MediaEnabled { get; set; }
    }
}
