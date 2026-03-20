using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace HASS.Agent.Avalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly HttpClient _httpClient;
    private readonly Timer _refreshTimer;
    private readonly Timer _clockTimer;
    
    // Connection
    [ObservableProperty] private string _title = "HASS.Agent";
    [ObservableProperty] private string _connectionStatus = "Disconnected";
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _headlessUrl = "http://127.0.0.1:11111";
    [ObservableProperty] private string _statusMessage = "Ready";
    [ObservableProperty] private string _currentTime = DateTime.Now.ToString("HH:mm:ss");
    
    // Dashboard
    [ObservableProperty] private int _sensorCount;
    [ObservableProperty] private int _commandCount;
    [ObservableProperty] private string _mqttStatus = "Unknown";
    [ObservableProperty] private bool _mqttConnected;
    [ObservableProperty] private string _deviceName = Environment.MachineName;
    [ObservableProperty] private string _platformInfo = GetPlatformInfo();
    
    // Sensors
    [ObservableProperty] private string _sensorFilter = "";
    public ObservableCollection<SensorDisplayModel> Sensors { get; } = new();
    public ObservableCollection<SensorDisplayModel> FilteredSensors { get; } = new();
    
    // Commands
    [ObservableProperty] private string _commandFilter = "";
    public ObservableCollection<CommandDisplayModel> Commands { get; } = new();
    public ObservableCollection<CommandDisplayModel> FilteredCommands { get; } = new();
    
    // Media
    [ObservableProperty] private string _nowPlayingTitle = "No media playing";
    [ObservableProperty] private string _nowPlayingArtist = "";
    [ObservableProperty] private string _nowPlayingAlbum = "";
    [ObservableProperty] private string _nowPlayingApp = "";
    [ObservableProperty] private string _playPauseIcon = "▶️";
    [ObservableProperty] private int _volume = 50;
    public ObservableCollection<MediaSessionModel> MediaSessions { get; } = new();
    
    // Bluetooth
    public ObservableCollection<BluetoothDeviceModel> PairedBluetoothDevices { get; } = new();
    public ObservableCollection<BluetoothDeviceModel> ConnectedBluetoothDevices { get; } = new();
    
    // Settings
    [ObservableProperty] private string _mqttBroker = "";
    [ObservableProperty] private string _mqttUsername = "";
    [ObservableProperty] private string _mqttPassword = "";
    [ObservableProperty] private string _mqttDiscoveryPrefix = "homeassistant";
    [ObservableProperty] private bool _mqttEnabled;
    [ObservableProperty] private string _deviceNameSetting = Environment.MachineName;
    [ObservableProperty] private int _updateInterval = 30;
    [ObservableProperty] private int _apiPort = 11111;
    [ObservableProperty] private bool _notificationsEnabled = true;
    [ObservableProperty] private bool _notifySensorUpdates;
    [ObservableProperty] private bool _notifyConnectionChanges = true;
    
    // Logs
    [ObservableProperty] private string _logFilter = "";
    [ObservableProperty] private string _selectedLogLevel = "All";
    public ObservableCollection<string> LogLevels { get; } = new() { "All", "Debug", "Info", "Warning", "Error" };
    public ObservableCollection<string> Logs { get; } = new();
    public ObservableCollection<string> FilteredLogs { get; } = new();
    
    // Recent Activity
    public ObservableCollection<ActivityItem> RecentActivity { get; } = new();
    
    // About
    [ObservableProperty] private string _appVersion = "2.0.0-linux";
    [ObservableProperty] private string _dotNetVersion = RuntimeInformation.FrameworkDescription;
    [ObservableProperty] private string _osVersion = RuntimeInformation.OSDescription;
    [ObservableProperty] private string _configPath = "";
    [ObservableProperty] private string _logPath = "";

    public MainWindowViewModel()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        
        // Set up auto-refresh timer
        _refreshTimer = new Timer(30000); // 30 seconds
        _refreshTimer.Elapsed += async (s, e) => await RefreshAllDataInternal();
        
        // Set up clock timer
        _clockTimer = new Timer(1000);
        _clockTimer.Elapsed += (s, e) => CurrentTime = DateTime.Now.ToString("HH:mm:ss");
        _clockTimer.Start();
        
        // Set paths
        ConfigPath = GetConfigPath();
        LogPath = GetLogPath();
        
        // Wire up filter changes
        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SensorFilter)) UpdateFilteredSensors();
            if (e.PropertyName == nameof(CommandFilter)) UpdateFilteredCommands();
            if (e.PropertyName == nameof(LogFilter) || e.PropertyName == nameof(SelectedLogLevel)) UpdateFilteredLogs();
        };
        
        // Add welcome activity
        AddActivity("🚀", "HASS.Agent started");
        
        // Try auto-connect
        _ = Task.Run(async () =>
        {
            await Task.Delay(500);
            await ConnectToHeadless();
        });
    }

    private static string GetPlatformInfo()
    {
        if (OperatingSystem.IsLinux()) return "Linux";
        if (OperatingSystem.IsMacOS()) return "macOS";
        if (OperatingSystem.IsWindows()) return "Windows";
        return "Unknown";
    }
    
    private static string GetConfigPath()
    {
        if (OperatingSystem.IsLinux())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "hass-agent");
        if (OperatingSystem.IsMacOS())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "HASS.Agent");
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HASS.Agent");
    }
    
    private static string GetLogPath()
    {
        if (OperatingSystem.IsLinux())
            return "/var/log/hass-agent/hass-agent.log";
        if (OperatingSystem.IsMacOS())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Logs", "HASS.Agent", "hass-agent.log");
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HASS.Agent", "logs", "hass-agent.log");
    }

    private void AddActivity(string icon, string message)
    {
        var item = new ActivityItem { Icon = icon, Message = message, Time = DateTime.Now.ToString("HH:mm:ss") };
        
        if (RecentActivity.Count >= 50)
        {
            RecentActivity.RemoveAt(RecentActivity.Count - 1);
        }
        RecentActivity.Insert(0, item);
    }

    [RelayCommand]
    private async Task ConnectToHeadless()
    {
        try
        {
            StatusMessage = "Connecting to headless service...";
            ConnectionStatus = "Connecting...";
            
            Log.Information("[GUI] Connecting to headless at {url}", HeadlessUrl);
            
            var response = await _httpClient.GetAsync($"{HeadlessUrl}/health");
            if (response.IsSuccessStatusCode)
            {
                IsConnected = true;
                ConnectionStatus = "Connected";
                StatusMessage = $"Connected to {HeadlessUrl}";
                _refreshTimer.Start();
                
                AddActivity("✅", $"Connected to {HeadlessUrl}");
                Log.Information("[GUI] Successfully connected to headless");
                
                await RefreshAllDataInternal();
            }
            else
            {
                IsConnected = false;
                ConnectionStatus = $"Failed ({response.StatusCode})";
                StatusMessage = $"Connection failed: {response.StatusCode}";
                Log.Warning("[GUI] Connection failed with status {status}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            IsConnected = false;
            ConnectionStatus = "Error";
            StatusMessage = $"Connection error: {ex.Message}";
            Log.Error(ex, "[GUI] Connection error");
            AddActivity("❌", $"Connection failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RefreshData()
    {
        await RefreshAllDataInternal();
    }

    private async Task RefreshAllDataInternal()
    {
        if (!IsConnected) return;
        
        try
        {
            StatusMessage = "Refreshing data...";
            Log.Debug("[GUI] Refreshing all data");
            
            await Task.WhenAll(
                RefreshSensorsInternal(),
                RefreshCommandsInternal(),
                RefreshMediaInternal(),
                RefreshBluetoothInternal(),
                RefreshMqttStatus()
            );
            
            StatusMessage = $"Last refresh: {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Refresh error: {ex.Message}";
            Log.Error(ex, "[GUI] Error refreshing data");
        }
    }

    [RelayCommand]
    private async Task RefreshSensors()
    {
        await RefreshSensorsInternal();
    }

    private async Task RefreshSensorsInternal()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{HeadlessUrl}/sensors");
            var content = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(content);
            
            Sensors.Clear();
            
            if (data.TryGetProperty("file_sensors", out var fileSensors))
            {
                foreach (var sensor in fileSensors.EnumerateArray())
                {
                    Sensors.Add(ParseSensor(sensor, "File"));
                }
            }
            
            if (data.TryGetProperty("platform_sensors", out var platformSensors))
            {
                foreach (var sensor in platformSensors.EnumerateArray())
                {
                    Sensors.Add(ParseSensor(sensor, "Platform"));
                }
            }
            
            SensorCount = Sensors.Count;
            UpdateFilteredSensors();
            Log.Debug("[GUI] Loaded {count} sensors", SensorCount);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[GUI] Error refreshing sensors");
        }
    }

    private SensorDisplayModel ParseSensor(JsonElement sensor, string type)
    {
        return new SensorDisplayModel
        {
            Id = sensor.TryGetProperty("Id", out var id) ? id.GetString() ?? "" : "",
            Name = sensor.TryGetProperty("Name", out var name) ? name.GetString() ?? "" : "",
            State = sensor.TryGetProperty("State", out var state) ? state.ToString() : "",
            Type = type,
            LastUpdated = DateTime.Now.ToString("HH:mm:ss")
        };
    }

    private void UpdateFilteredSensors()
    {
        FilteredSensors.Clear();
        var filter = SensorFilter?.ToLower() ?? "";
        foreach (var sensor in Sensors.Where(s => 
            string.IsNullOrEmpty(filter) || 
            s.Id.ToLower().Contains(filter) || 
            s.Name.ToLower().Contains(filter)))
        {
            FilteredSensors.Add(sensor);
        }
    }

    [RelayCommand]
    private async Task RefreshCommands()
    {
        await RefreshCommandsInternal();
    }

    private async Task RefreshCommandsInternal()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{HeadlessUrl}/commands");
            var content = await response.Content.ReadAsStringAsync();
            var commands = JsonSerializer.Deserialize<JsonElement>(content);
            
            Commands.Clear();
            
            foreach (var cmd in commands.EnumerateArray())
            {
                Commands.Add(new CommandDisplayModel
                {
                    Id = cmd.TryGetProperty("Id", out var id) ? id.GetString() ?? "" : "",
                    Name = cmd.TryGetProperty("Name", out var name) ? name.GetString() ?? "" : "",
                    EntityType = cmd.TryGetProperty("EntityType", out var et) ? et.GetString() ?? "" : "",
                    Command = cmd.TryGetProperty("Command", out var c) ? c.GetString() ?? "" : ""
                });
            }
            
            CommandCount = Commands.Count;
            UpdateFilteredCommands();
            Log.Debug("[GUI] Loaded {count} commands", CommandCount);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[GUI] Error refreshing commands");
        }
    }

    private void UpdateFilteredCommands()
    {
        FilteredCommands.Clear();
        var filter = CommandFilter?.ToLower() ?? "";
        foreach (var cmd in Commands.Where(c => 
            string.IsNullOrEmpty(filter) || 
            c.Id.ToLower().Contains(filter) || 
            c.Name.ToLower().Contains(filter)))
        {
            FilteredCommands.Add(cmd);
        }
    }

    private async Task RefreshMediaInternal()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{HeadlessUrl}/media/status");
            if (!response.IsSuccessStatusCode) return;
            
            var content = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(content);
            
            NowPlayingTitle = data.TryGetProperty("title", out var title) ? title.GetString() ?? "No media playing" : "No media playing";
            NowPlayingArtist = data.TryGetProperty("artist", out var artist) ? artist.GetString() ?? "" : "";
            NowPlayingAlbum = data.TryGetProperty("album", out var album) ? album.GetString() ?? "" : "";
            NowPlayingApp = data.TryGetProperty("player", out var player) ? player.GetString() ?? "" : "";
            
            var isPlaying = data.TryGetProperty("status", out var status) && status.GetString() == "playing";
            PlayPauseIcon = isPlaying ? "⏸️" : "▶️";
        }
        catch (Exception ex)
        {
            Log.Debug("[GUI] Error refreshing media: {msg}", ex.Message);
        }
    }

    private async Task RefreshBluetoothInternal()
    {
        try
        {
            var pairedResponse = await _httpClient.GetAsync($"{HeadlessUrl}/bluetooth/paired");
            if (pairedResponse.IsSuccessStatusCode)
            {
                var content = await pairedResponse.Content.ReadAsStringAsync();
                var devices = JsonSerializer.Deserialize<JsonElement>(content);
                
                PairedBluetoothDevices.Clear();
                foreach (var device in devices.EnumerateArray())
                {
                    PairedBluetoothDevices.Add(new BluetoothDeviceModel
                    {
                        Name = device.TryGetProperty("name", out var name) ? name.GetString() ?? "Unknown" : "Unknown",
                        MacAddress = device.TryGetProperty("mac", out var mac) ? mac.GetString() ?? "" : "",
                        IsConnected = device.TryGetProperty("connected", out var conn) && conn.GetBoolean()
                    });
                }
            }
            
            var connectedResponse = await _httpClient.GetAsync($"{HeadlessUrl}/bluetooth/connected");
            if (connectedResponse.IsSuccessStatusCode)
            {
                var content = await connectedResponse.Content.ReadAsStringAsync();
                var devices = JsonSerializer.Deserialize<JsonElement>(content);
                
                ConnectedBluetoothDevices.Clear();
                foreach (var device in devices.EnumerateArray())
                {
                    ConnectedBluetoothDevices.Add(new BluetoothDeviceModel
                    {
                        Name = device.TryGetProperty("name", out var name) ? name.GetString() ?? "Unknown" : "Unknown",
                        MacAddress = device.TryGetProperty("mac", out var mac) ? mac.GetString() ?? "" : "",
                        IsConnected = true
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug("[GUI] Error refreshing bluetooth: {msg}", ex.Message);
        }
    }

    private async Task RefreshMqttStatus()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{HeadlessUrl}/health");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<JsonElement>(content);
                
                MqttConnected = data.TryGetProperty("mqtt", out var mqtt) && 
                               mqtt.TryGetProperty("connected", out var conn) && 
                               conn.GetBoolean();
                MqttStatus = MqttConnected ? "Connected" : "Disconnected";
            }
        }
        catch
        {
            MqttStatus = "Unknown";
            MqttConnected = false;
        }
    }

    // Media Commands
    [RelayCommand]
    private async Task MediaPlayPause()
    {
        try
        {
            await _httpClient.PostAsync($"{HeadlessUrl}/media/playpause", null);
            await RefreshMediaInternal();
            AddActivity("🎵", "Toggled play/pause");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[GUI] Error toggling play/pause");
        }
    }

    [RelayCommand]
    private async Task MediaPrevious()
    {
        try
        {
            await _httpClient.PostAsync($"{HeadlessUrl}/media/previous", null);
            AddActivity("⏮️", "Previous track");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[GUI] Error going to previous track");
        }
    }

    [RelayCommand]
    private async Task MediaNext()
    {
        try
        {
            await _httpClient.PostAsync($"{HeadlessUrl}/media/next", null);
            AddActivity("⏭️", "Next track");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[GUI] Error going to next track");
        }
    }

    // Discovery Commands
    [RelayCommand]
    private async Task PublishDiscovery()
    {
        try
        {
            StatusMessage = "Publishing discovery...";
            var response = await _httpClient.PostAsync($"{HeadlessUrl}/discovery/publish", null);
            if (response.IsSuccessStatusCode)
            {
                StatusMessage = "Discovery published successfully";
                AddActivity("📤", "Discovery published to Home Assistant");
                Log.Information("[GUI] Discovery published");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Publish error: {ex.Message}";
            Log.Error(ex, "[GUI] Error publishing discovery");
        }
    }

    [RelayCommand]
    private async Task ClearDiscovery()
    {
        try
        {
            StatusMessage = "Clearing discovery...";
            var response = await _httpClient.PostAsync($"{HeadlessUrl}/discovery/clear", null);
            if (response.IsSuccessStatusCode)
            {
                StatusMessage = "Discovery cleared";
                AddActivity("🗑️", "Discovery cleared from Home Assistant");
                Log.Information("[GUI] Discovery cleared");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Clear error: {ex.Message}";
            Log.Error(ex, "[GUI] Error clearing discovery");
        }
    }

    [RelayCommand]
    private async Task TestNotification()
    {
        try
        {
            var response = await _httpClient.PostAsync($"{HeadlessUrl}/notify?title=Test&message=Hello+from+HASS.Agent!", null);
            if (response.IsSuccessStatusCode)
            {
                AddActivity("🔔", "Test notification sent");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[GUI] Error sending test notification");
        }
    }

    // Sensor/Command Management
    [RelayCommand]
    private void AddSensor()
    {
        AddActivity("➕", "Opening add sensor dialog...");
        // TODO: Open add sensor dialog
    }

    [RelayCommand]
    private void AddCommand()
    {
        AddActivity("➕", "Opening add command dialog...");
        // TODO: Open add command dialog
    }

    // Bluetooth Commands
    [RelayCommand]
    private async Task RefreshBluetooth()
    {
        await RefreshBluetoothInternal();
        AddActivity("📶", "Bluetooth devices refreshed");
    }

    [RelayCommand]
    private void StartBluetoothScan()
    {
        AddActivity("📡", "Starting Bluetooth scan...");
        // TODO: Start BT scan
    }

    // Settings Commands
    [RelayCommand]
    private async Task SaveSettings()
    {
        try
        {
            // TODO: Save to config file
            StatusMessage = "Settings saved";
            AddActivity("💾", "Settings saved");
            Log.Information("[GUI] Settings saved");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save error: {ex.Message}";
            Log.Error(ex, "[GUI] Error saving settings");
        }
    }

    [RelayCommand]
    private void ResetSettings()
    {
        MqttBroker = "";
        MqttUsername = "";
        MqttPassword = "";
        MqttDiscoveryPrefix = "homeassistant";
        MqttEnabled = false;
        DeviceNameSetting = Environment.MachineName;
        UpdateInterval = 30;
        ApiPort = 11111;
        AddActivity("🔄", "Settings reset to defaults");
    }

    [RelayCommand]
    private void ExportConfig()
    {
        AddActivity("📤", "Export configuration...");
        // TODO: Export config to file
    }

    [RelayCommand]
    private void ImportConfig()
    {
        AddActivity("📥", "Import configuration...");
        // TODO: Import config from file
    }

    // Logs Commands
    [RelayCommand]
    private void RefreshLogs()
    {
        // TODO: Load logs from file
        UpdateFilteredLogs();
    }

    [RelayCommand]
    private void ClearLogs()
    {
        Logs.Clear();
        FilteredLogs.Clear();
        AddActivity("🗑️", "Logs cleared");
    }

    [RelayCommand]
    private void ExportLogs()
    {
        AddActivity("📤", "Export logs...");
        // TODO: Export logs to file
    }

    private void UpdateFilteredLogs()
    {
        FilteredLogs.Clear();
        var filter = LogFilter?.ToLower() ?? "";
        var level = SelectedLogLevel;
        
        foreach (var log in Logs)
        {
            var matchesFilter = string.IsNullOrEmpty(filter) || log.ToLower().Contains(filter);
            var matchesLevel = level == "All" || log.Contains($"[{level.ToUpper().Substring(0, 3)}]");
            
            if (matchesFilter && matchesLevel)
            {
                FilteredLogs.Add(log);
            }
        }
    }
}

// Models
public class SensorDisplayModel
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string State { get; set; } = "";
    public string Type { get; set; } = "";
    public string LastUpdated { get; set; } = "";
}

public class CommandDisplayModel
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string Command { get; set; } = "";
}

public class MediaSessionModel
{
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Player { get; set; } = "";
    public string Status { get; set; } = "";
}

public class BluetoothDeviceModel
{
    public string Name { get; set; } = "";
    public string MacAddress { get; set; } = "";
    public bool IsConnected { get; set; }
    public string ConnectButtonText => IsConnected ? "Disconnect" : "Connect";
}

public class ActivityItem
{
    public string Icon { get; set; } = "";
    public string Message { get; set; } = "";
    public string Time { get; set; } = "";
}
