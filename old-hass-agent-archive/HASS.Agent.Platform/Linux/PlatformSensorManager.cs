using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HASS.Agent.Core;
using HASS.Agent.Platform.Abstractions;
using HASS.Agent.Platform.Linux.Sensors;
using Serilog;

namespace HASS.Agent.Platform.Linux
{
    public class PlatformSensorManager
    {
        private readonly IMqttManager _mqtt;
        private readonly List<ISensor> _sensors = new();
        private Timer? _updateTimer;
        private readonly string _deviceName;
        private readonly int _updateIntervalSeconds;
        
        public PlatformSensorManager(IMqttManager mqtt, string deviceName = "hass-agent", int updateIntervalSeconds = 30)
        {
            _mqtt = mqtt;
            _deviceName = deviceName;
            _updateIntervalSeconds = updateIntervalSeconds;
            
            // Initialize platform sensors
            _sensors.Add(new DiskUsageSensor());
            _sensors.Add(new NetworkInterfacesSensor());
            _sensors.Add(new SystemResourcesSensor());
            _sensors.Add(new BatterySensor());
            _sensors.Add(new TemperatureSensor());
            _sensors.Add(new UserSessionsSensor());
            
            // Only add ActiveWindowSensor on Linux with X11 (check for DISPLAY env)
            if (OperatingSystem.IsLinux() && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")))
            {
                _sensors.Add(new ActiveWindowSensor());
            }
            
            Log.Information("[PlatformSensorManager] Initialized with {count} sensors", _sensors.Count);
        }
        
        public void Start()
        {
            Log.Information("[PlatformSensorManager] Starting sensor updates every {interval}s", _updateIntervalSeconds);
            _updateTimer = new Timer(UpdateSensors, null, TimeSpan.Zero, TimeSpan.FromSeconds(_updateIntervalSeconds));
        }
        
        public void Stop()
        {
            _updateTimer?.Dispose();
            _updateTimer = null;
            Log.Information("[PlatformSensorManager] Stopped");
        }
        
        private async void UpdateSensors(object? state)
        {
            try
            {
                foreach (var sensor in _sensors)
                {
                    await UpdateSensor(sensor);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[PlatformSensorManager] Error in sensor update cycle");
            }
        }
        
        private async Task UpdateSensor(ISensor sensor)
        {
            try
            {
                var id = sensor.Id;
                var name = sensor.Name;
                var sensorState = sensor.GetState();
                
                if (string.IsNullOrEmpty(id) || sensorState == null) return;
                
                // Publish to MQTT
                var topic = $"homeassistant/sensor/{_deviceName}_{id}/state";
                var payload = JsonSerializer.Serialize(sensorState, new JsonSerializerOptions { WriteIndented = false });
                
                await _mqtt.PublishAsync(topic, payload, false);
                
                // Also publish discovery if this is the first time
                await PublishDiscovery(id, name, sensorState);
                
                Log.Debug("[PlatformSensorManager] Updated sensor {id}", id);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[PlatformSensorManager] Error updating sensor");
            }
        }
        
        private async Task PublishDiscovery(string id, string name, Dictionary<string, object> state)
        {
            try
            {
                var discoveryTopic = $"homeassistant/sensor/{_deviceName}_{id}/config";
                var discovery = new
                {
                    unique_id = $"{_deviceName}_{id}",
                    name = $"{_deviceName} {name}",
                    state_topic = $"homeassistant/sensor/{_deviceName}_{id}/state",
                    json_attributes_topic = $"homeassistant/sensor/{_deviceName}_{id}/state",
                    device = new
                    {
                        identifiers = new[] { _deviceName },
                        name = _deviceName,
                        manufacturer = "HASS.Agent",
                        model = "Platform Sensor",
                        sw_version = "1.0"
                    },
                    value_template = "{{ value_json.state }}",
                    icon = GetSensorIcon(id)
                };
                
                var discoveryPayload = JsonSerializer.Serialize(discovery, new JsonSerializerOptions { WriteIndented = false });
                await _mqtt.PublishAsync(discoveryTopic, discoveryPayload, true);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[PlatformSensorManager] Error publishing discovery for {id}", id);
            }
        }
        
        private static string GetSensorIcon(string id)
        {
            return id switch
            {
                var x when x.Contains("disk") => "mdi:harddisk",
                var x when x.Contains("network") => "mdi:ethernet",
                var x when x.Contains("system") || x.Contains("cpu") || x.Contains("memory") => "mdi:chip",
                _ => "mdi:information"
            };
        }
        
        public List<object> GetCurrentSensorData()
        {
            var result = new List<object>();
            
            foreach (var sensor in _sensors)
            {
                try
                {
                    var state = sensor.GetState();
                    
                    result.Add(new
                    {
                        Id = sensor.Id,
                        Name = sensor.Name,
                        State = state.ContainsKey("state") ? state["state"] : "unknown",
                        Type = "platform",
                        Data = state
                    });
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[PlatformSensorManager] Error getting sensor data for {id}", sensor.Id);
                }
            }
            
            return result;
        }
    }
}