using System;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
// NOTE: MQTTnet package versions expose types in slightly different namespaces
// avoid referencing MQTTnet.Client.Options directly to reduce version friction
using Serilog;

namespace HASS.Agent.Core
{
    public class MqttNetManager : IMqttManager, IAsyncDisposable
    {
        private IMqttClient? _client;
        private volatile bool _connected;
        private readonly string? _broker;

        public MqttNetManager()
        {
            _broker = Environment.GetEnvironmentVariable("HASS_AGENT_MQTT_BROKER");
            var username = Environment.GetEnvironmentVariable("HASS_AGENT_MQTT_USERNAME");
            var password = Environment.GetEnvironmentVariable("HASS_AGENT_MQTT_PASSWORD");
            var useTls = Environment.GetEnvironmentVariable("HASS_AGENT_MQTT_TLS");

            if (string.IsNullOrEmpty(_broker))
            {
                try
                {
                    var cfgPath = Path.Combine(VariablesCore.ConfigPath, "appsettings.json");
                    if (File.Exists(cfgPath))
                    {
                        var json = File.ReadAllText(cfgPath);
                        using var doc = System.Text.Json.JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("Hass", out var hass))
                        {
                            if (hass.TryGetProperty("MqttBroker", out var b)) _broker = b.GetString();
                            if (hass.TryGetProperty("MqttUsername", out var u)) username = u.GetString();
                            if (hass.TryGetProperty("MqttPassword", out var p)) password = p.GetString();
                            if (hass.TryGetProperty("MqttTls", out var t)) useTls = t.GetString();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Unable to read MQTT settings from appsettings.json");
                }
            }

            if (string.IsNullOrEmpty(_broker)) return;

            var factory = new MqttFactory();
            _client = factory.CreateMqttClient();

            // start background connect loop
            // only start connect loop when broker is configured
            _ = Task.Run(async () => await ConnectLoopAsync());
        }

        public bool IsConnected() => _client != null && _client.IsConnected;

        public async Task PublishAsync(string topic, string payload)
        {
            if (!IsConnected())
            {
                Log.Debug("MQTT not connected, dropping publish to {topic}", topic);
                return;
            }
            var msg = new MqttApplicationMessageBuilder().WithTopic(topic).WithPayload(payload).Build();
            await PublishMessageWithRetainFallback(msg, null);
        }

        public async Task PublishAsync(string topic, string payload, bool retain)
        {
            if (!IsConnected())
            {
                Log.Debug("MQTT not connected, dropping publish to {topic}", topic);
                return;
            }

            var builder = new MqttApplicationMessageBuilder().WithTopic(topic).WithPayload(payload);
            try { builder = builder.WithRetainFlag(retain); } catch { }
            var msg = builder.Build();
            await PublishMessageWithRetainFallback(msg, retain);
        }

        private async Task PublishMessageWithRetainFallback(MqttApplicationMessage msg, bool? retain)
        {
            if (_client == null || !_client.IsConnected)
            {
                Log.Debug("MQTT not connected, dropping publish to {topic}", msg.Topic);
                return;
            }

            // best effort: try to set explicit retain in options via reflection if PublishAsync overload requires options
            // Try simple publish first
            try
            {
                // ensure retain flag is set on message
                if (retain.HasValue)
                {
                    msg.Retain = retain.Value;
                }

                await _client.PublishAsync(msg);
                return;
            }
            catch
            {
                // Some MQTTnet versions expect PublishAsync(MqttApplicationMessage, MqttClientPublishOptions)
                try
                {
                    var optionsType = Type.GetType("MQTTnet.Client.Publishing.MqttClientPublishOptions, MQTTnet")
                                      ?? Type.GetType("MQTTnet.Client.Publishing.MqttClientPublishOptions, MQTTnet.Extensions.ManagedClient")
                                      ?? Type.GetType("MQTTnet.Client.Publishing.MqttClientPublishOptions");

                    if (optionsType != null && _client != null)
                    {
                        var options = Activator.CreateInstance(optionsType);
                        // try to set Retain flag via property if present
                        try { optionsType.GetProperty("Retain")?.SetValue(options, retain ?? false); } catch { }

                        var clientType = _client.GetType();
                        var method = clientType.GetMethod("PublishAsync", new Type[] { msg.GetType(), optionsType })
                                     ?? clientType.GetMethod("PublishAsync", new Type[] { msg.GetType(), typeof(System.Threading.CancellationToken) });
                        if (method != null && options != null)
                        {
                            var task = method.Invoke(_client, new object[] { msg, options });
                            if (task is System.Threading.Tasks.Task t) await t;
                            return;
                        }
                    }
                }
                catch { }

                // last resort: try publish via reflection without options
                try
                {
                    if (_client != null)
                    {
                        var clientType = _client.GetType();
                        var method = clientType.GetMethod("PublishAsync", new Type[] { msg.GetType() })
                                     ?? clientType.GetMethod("PublishAsync", new Type[] { msg.GetType(), typeof(System.Threading.CancellationToken) });
                        if (method != null)
                        {
                            var task = method.Invoke(_client, new object[] { msg });
                            if (task is System.Threading.Tasks.Task t) await t;
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "MQTT publish failed for topic {topic}", msg.Topic);
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_client != null && _client.IsConnected) await _client.DisconnectAsync();
            }
            catch { }
        }

        private async Task ConnectLoopAsync()
        {
            var username = Environment.GetEnvironmentVariable("HASS_AGENT_MQTT_USERNAME");
            var password = Environment.GetEnvironmentVariable("HASS_AGENT_MQTT_PASSWORD");
            var useTls = Environment.GetEnvironmentVariable("HASS_AGENT_MQTT_TLS");

            var broker = _broker;
            if (string.IsNullOrWhiteSpace(broker))
            {
                Log.Information("No MQTT broker configured, skipping connect loop");
                return;
            }

            var optsBuilder = new MqttClientOptionsBuilder().WithTcpServer(broker);
            if (!string.IsNullOrEmpty(username)) optsBuilder.WithCredentials(username, password ?? string.Empty);
            if (!string.IsNullOrEmpty(useTls) && (useTls == "1" || useTls.ToLower() == "true")) optsBuilder.WithTls();

            var opts = optsBuilder.Build();

            while (true)
            {
                try
                {
                    if (_client == null)
                    {
                        _client = new MqttFactory().CreateMqttClient();
                    }

                    if (_client != null && !_client.IsConnected)
                    {
                        Log.Information("Connecting to MQTT broker {broker}", broker);
                        await _client.ConnectAsync(opts);
                        _connected = _client.IsConnected;
                        Log.Information("MQTT connected: {ok}", _connected);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "MQTT connect failed, retrying in 5s");
                    _connected = false;
                }

                await Task.Delay(5000);
            }
        }
    }
}
