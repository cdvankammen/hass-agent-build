using System.Threading.Tasks;
using Serilog;

namespace HASS.Agent.Core
{
    public static class DiscoveryPublisher
    {
        public static async Task PublishAllAsync(IMqttManager mqtt)
        {
            if (mqtt == null || !mqtt.IsConnected())
            {
                Log.Information("MQTT not connected - skipping discovery publish");
                return;
            }

            // Publish a simple discovery announcement for commands and sensors
            var commands = CommandsLoader.Load(VariablesCore.ConfigPath + "/commands.json");
            foreach (var c in commands)
            {
                var topic = $"homeassistant/switch/hass_agent_command_{c.Id}/config";
                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    name = c.Name,
                    unique_id = c.Id,
                    command_topic = $"hassagent/command/{c.Id}",
                    state_topic = $"hassagent/command/{c.Id}/state",
                    value_template = "{{ value_json.state }}"
                });
                await mqtt.PublishAsync(topic, payload, true);
            }

            var sensors = SensorsLoader.Load(VariablesCore.ConfigPath + "/sensors.json");
            foreach (var s in sensors)
            {
                var topic = $"homeassistant/sensor/hass_agent_sensor_{s.Id}/config";
                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    name = s.Name,
                    unique_id = s.Id,
                    state_topic = $"hassagent/sensor/{s.Id}/state",
                });
                await mqtt.PublishAsync(topic, payload, true);
            }
        }

        public static async Task ClearAllAsync(IMqttManager mqtt)
        {
            if (mqtt == null || !mqtt.IsConnected())
            {
                Log.Information("MQTT not connected - skipping discovery clear");
                return;
            }

            var commands = CommandsLoader.Load(VariablesCore.ConfigPath + "/commands.json");
            foreach (var c in commands)
            {
                var topic = $"homeassistant/switch/hass_agent_command_{c.Id}/config";
                // publish empty payload with retain to clear
                await mqtt.PublishAsync(topic, string.Empty, true);
            }

            var sensors = SensorsLoader.Load(VariablesCore.ConfigPath + "/sensors.json");
            foreach (var s in sensors)
            {
                var topic = $"homeassistant/sensor/hass_agent_sensor_{s.Id}/config";
                await mqtt.PublishAsync(topic, string.Empty, true);
            }
        }
    }
}
