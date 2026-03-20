using System.Collections.Generic;
using System.Text.Json;
using HASS.Agent.Core;
using HASS.Agent.Platform;
using HASS.Agent.Platform.Linux;
using HASS.Agent.Platform.Linux.Media;
using HASS.Agent.Platform.Linux.Bluetooth;
using Serilog;

namespace HASS.Agent.Headless.Services
{
    /// <summary>
    /// Manages configuration loading, validation, and persistence
    /// </summary>
    public class ConfigurationService
    {
        private readonly string _configPath;
        private readonly string _appSettingsPath;
        
        public ConfigurationService()
        {
            _configPath = VariablesCore.ConfigPath;
            _appSettingsPath = Path.Combine(_configPath, "appsettings.json");
            EnsureConfigDirectoryExists();
        }
        
        private void EnsureConfigDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(_configPath))
                {
                    Directory.CreateDirectory(_configPath);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[CONFIG] Failed to create config directory: {path}", _configPath);
            }
        }
        
        public string ConfigPath => _configPath;
        public string AppSettingsPath => _appSettingsPath;
        
        public bool ReadConfiguredBool(string key, bool defaultValue)
        {
            try
            {
                if (!File.Exists(_appSettingsPath)) return defaultValue;
                
                var json = File.ReadAllText(_appSettingsPath);
                using var doc = JsonDocument.Parse(json);
                
                if (doc.RootElement.TryGetProperty(key, out var prop))
                {
                    if (prop.ValueKind == JsonValueKind.True) return true;
                    if (prop.ValueKind == JsonValueKind.False) return false;
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[CONFIG] Error reading bool config {key}", key);
            }
            return defaultValue;
        }
        
        public string? ReadConfiguredString(string key, string? defaultValue = null)
        {
            try
            {
                if (!File.Exists(_appSettingsPath)) return defaultValue;
                
                var json = File.ReadAllText(_appSettingsPath);
                using var doc = JsonDocument.Parse(json);
                
                if (doc.RootElement.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String)
                {
                    return prop.GetString() ?? defaultValue;
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[CONFIG] Error reading string config {key}", key);
            }
            return defaultValue;
        }
        
        public int ReadConfiguredInt(string key, int defaultValue)
        {
            try
            {
                if (!File.Exists(_appSettingsPath)) return defaultValue;
                
                var json = File.ReadAllText(_appSettingsPath);
                using var doc = JsonDocument.Parse(json);
                
                if (doc.RootElement.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.Number)
                {
                    return prop.GetInt32();
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[CONFIG] Error reading int config {key}", key);
            }
            return defaultValue;
        }
        
        public Dictionary<string, object> GetAllSettings()
        {
            var settings = new Dictionary<string, object>();
            try
            {
                if (File.Exists(_appSettingsPath))
                {
                    var json = File.ReadAllText(_appSettingsPath);
                    using var doc = JsonDocument.Parse(json);
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        settings[prop.Name] = prop.Value.ValueKind switch
                        {
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            JsonValueKind.Number => prop.Value.TryGetInt32(out var i) ? i : prop.Value.GetDouble(),
                            JsonValueKind.String => prop.Value.GetString() ?? "",
                            _ => prop.Value.GetRawText()
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[CONFIG] Error reading all settings");
            }
            return settings;
        }
        
        public bool SaveSettings(Dictionary<string, object> settings)
        {
            try
            {
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_appSettingsPath, json);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[CONFIG] Error saving settings");
                return false;
            }
        }
        
        public Dictionary<string, string> ValidateSettings(JsonElement settings)
        {
            var errors = new Dictionary<string, string>();
            
            // DeviceName required
            if (!settings.TryGetProperty("DeviceName", out var device) || 
                string.IsNullOrWhiteSpace(device.GetString()))
            {
                errors["DeviceName"] = "DeviceName is required";
            }
            
            // LocalApiPort range
            if (settings.TryGetProperty("LocalApiPort", out var portEl))
            {
                if (portEl.ValueKind == JsonValueKind.Number && portEl.TryGetInt32(out var p))
                {
                    if (p < 1 || p > 65535) errors["LocalApiPort"] = "Port must be between 1 and 65535";
                }
            }
            
            // MqttPort range
            if (settings.TryGetProperty("MqttPort", out var mqttPort))
            {
                if (mqttPort.ValueKind == JsonValueKind.Number && mqttPort.TryGetInt32(out var mp))
                {
                    if (mp < 1 || mp > 65535) errors["MqttPort"] = "Port must be between 1 and 65535";
                }
            }
            
            return errors;
        }
        
        public List<Dictionary<string, object>> GetSettingsSchema()
        {
            return new List<Dictionary<string, object>>
            {
                new() { ["name"] = "DeviceName", ["type"] = "string", ["label"] = "Device name", ["required"] = true },
                new() { ["name"] = "SanitizeName", ["type"] = "boolean", ["label"] = "Sanitize device name" },
                new() { ["name"] = "InterfaceLanguage", ["type"] = "string", ["label"] = "Interface language (locale)" },
                new() { ["name"] = "EnableStateNotifications", ["type"] = "boolean", ["label"] = "Enable state notifications" },
                new() { ["name"] = "CheckForUpdates", ["type"] = "boolean", ["label"] = "Check for updates" },
                new() { ["name"] = "DisconnectedGracePeriodSeconds", ["type"] = "integer", ["label"] = "Disconnected grace period (sec)" },
                new() { ["name"] = "LocalApiEnabled", ["type"] = "boolean", ["label"] = "Local API enabled" },
                new() { ["name"] = "BindHost", ["type"] = "string", ["label"] = "Local API bind host" },
                new() { ["name"] = "LocalApiPort", ["type"] = "integer", ["label"] = "Local API port" },
                new() { ["name"] = "CorsAllowedOrigins", ["type"] = "string", ["label"] = "Allowed CORS origins" },
                new() { ["name"] = "NotificationsEnabled", ["type"] = "boolean", ["label"] = "Notifications enabled" },
                new() { ["name"] = "MediaPlayerEnabled", ["type"] = "boolean", ["label"] = "Media player enabled" },
                new() { ["name"] = "HassUri", ["type"] = "string", ["label"] = "Home Assistant URI" },
                new() { ["name"] = "HassToken", ["type"] = "string", ["label"] = "Home Assistant token" },
                new() { ["name"] = "HassAllowUntrustedCertificates", ["type"] = "boolean", ["label"] = "Allow untrusted HA certs" },
                new() { ["name"] = "MqttEnabled", ["type"] = "boolean", ["label"] = "MQTT enabled" },
                new() { ["name"] = "MqttAddress", ["type"] = "string", ["label"] = "MQTT address" },
                new() { ["name"] = "MqttPort", ["type"] = "integer", ["label"] = "MQTT port" },
                new() { ["name"] = "MqttUseTls", ["type"] = "boolean", ["label"] = "MQTT use TLS" },
                new() { ["name"] = "MqttAllowUntrustedCertificates", ["type"] = "boolean", ["label"] = "MQTT allow untrusted certs" },
                new() { ["name"] = "MqttUsername", ["type"] = "string", ["label"] = "MQTT username" },
                new() { ["name"] = "MqttPassword", ["type"] = "string", ["label"] = "MQTT password" },
                new() { ["name"] = "MqttDiscoveryPrefix", ["type"] = "string", ["label"] = "MQTT discovery prefix" },
                new() { ["name"] = "MqttClientId", ["type"] = "string", ["label"] = "MQTT client id" },
                new() { ["name"] = "MqttUseRetainFlag", ["type"] = "boolean", ["label"] = "MQTT retain flag" }
            };
        }
    }
}
