using System;
using System.Threading.Tasks;
using Serilog;

namespace HASS.Agent.Core
{
    public class DummyMqttManager : IMqttManager
    {
        public bool IsConnected() => false;

        public Task PublishAsync(string topic, string payload)
        {
            Log.Information("[DummyMQTT] Publish to {topic}: {payload}", topic, payload);
            return Task.CompletedTask;
        }

        public Task PublishAsync(string topic, string payload, bool retain)
        {
            Log.Information("[DummyMQTT] Publish to {topic}: {payload} (retain={retain})", topic, payload, retain);
            return Task.CompletedTask;
        }
    }
}
