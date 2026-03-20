using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace HASS.Agent.Headless
{
    internal class AppSettingsDto
    {
        public string ServiceAuthId { get; set; } = string.Empty;
    }

    public class RpcServer
    {
        private readonly string _socketPath;
        private readonly IHostApplicationLifetime _appLifetime;
        private CancellationTokenSource? _cts;

        public RpcServer(IHostApplicationLifetime appLifetime)
        {
            _appLifetime = appLifetime;
            var env = Environment.GetEnvironmentVariable("HASS_AGENT_RPC_SOCKET");
            if (!string.IsNullOrEmpty(env)) _socketPath = env;
            else
            {
                // default to system socket path on Linux systems
                if (OperatingSystem.IsLinux()) _socketPath = "/var/run/hass-agent.sock";
                else if (OperatingSystem.IsMacOS()) _socketPath = "tcp://127.0.0.1:52222";
                else _socketPath = Path.Combine(Directory.GetCurrentDirectory(), "hass-agent.sock");
            }
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            Task.Run(() => RunAsync(_cts.Token));
        }

        public void Stop()
        {
            try
            {
                _cts?.Cancel();
            }
            catch { }
        }

        private async Task RunAsync(CancellationToken token)
        {
            try
            {
                if (File.Exists(_socketPath)) File.Delete(_socketPath);

                bool useTcp = _socketPath.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase);
                System.Net.Sockets.Socket? listener = null;
                System.Net.EndPoint? bindEp = null;

                if (useTcp)
                {
                    var parts = _socketPath.Substring("tcp://".Length).Split(':');
                    var host = parts[0];
                    var port = int.Parse(parts[1]);
                    listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    bindEp = new System.Net.IPEndPoint(System.Net.IPAddress.Parse(host), port);
                    listener.Bind(bindEp);
                    listener.Listen(5);
                }
                else
                {
                    listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    var ep = new UnixDomainSocketEndPoint(_socketPath);
                    listener.Bind(ep);
                    // try to set permissive socket file mode so service user can access it when placed under /var/run
                    try
                    {
                        if (OperatingSystem.IsLinux())
                        {
                            try
                            {
                                var psi = new System.Diagnostics.ProcessStartInfo("chmod", $"660 {_socketPath}") { UseShellExecute = false };
                                var p = System.Diagnostics.Process.Start(psi);
                                p?.WaitForExit();
                            }
                            catch { }
                        }
                    }
                    catch { }
                    listener.Listen(5);
                }

                Log.Information("[RPC] Listening on {path}", _socketPath);

                // create mqtt manager from core (uses env var to decide)
                HASS.Agent.Core.IMqttManager mqtt;
                var broker = Environment.GetEnvironmentVariable("HASS_AGENT_MQTT_BROKER");
                if (!string.IsNullOrEmpty(broker)) mqtt = new HASS.Agent.Core.MqttNetManager();
                else mqtt = new HASS.Agent.Core.DummyMqttManager();

                while (!token.IsCancellationRequested)
                {
                    using var client = await listener.AcceptAsync();
                    var buffer = new byte[8192];
                    var read = await client.ReceiveAsync(buffer, SocketFlags.None);
                    if (read == 0) continue;

                    var req = Encoding.UTF8.GetString(buffer, 0, read);
                    Log.Information("[RPC] Request: {r}", req);

                    JsonDocument? doc = null;
                    try { doc = JsonDocument.Parse(req); } catch { }

                    object resp = new { ok = false };
                    if (doc != null && doc.RootElement.TryGetProperty("cmd", out var cmd))
                    {
                        var c = cmd.GetString();
                        if (c == "ping")
                        {
                            resp = new { ok = true, version = "headless-local" };
                        }
                        else if (c == "shutdown")
                        {
                            // validate auth
                            var valid = true;
                            var expected = Environment.GetEnvironmentVariable("HASS_AGENT_SERVICE_AUTH");
                            if (string.IsNullOrEmpty(expected))
                            {
                                try
                                {
                                    var cfgPath = Path.Combine(Directory.GetCurrentDirectory(), "config", "appsettings.json");
                                    if (File.Exists(cfgPath))
                                    {
                                        var json = await File.ReadAllTextAsync(cfgPath, token);
                                        var settings = System.Text.Json.JsonSerializer.Deserialize<AppSettingsDto>(json);
                                        expected = settings?.ServiceAuthId ?? string.Empty;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log.Warning(ex, "[RPC] Unable to load appsettings for auth fallback");
                                }
                            }

                            if (!string.IsNullOrEmpty(expected))
                            {
                                if (doc.RootElement.TryGetProperty("auth", out var a))
                                {
                                    var provided = a.GetString() ?? string.Empty;
                                    valid = provided == expected;
                                }
                                else valid = false;
                            }

                            if (valid)
                            {
                                resp = new { ok = true };
                                _appLifetime.StopApplication();
                            }
                            else
                            {
                                resp = new { ok = false, error = "auth_failed" };
                            }
                        }
                        else if (c == "clear_entities")
                        {
                            var valid = true;
                            var expected2 = Environment.GetEnvironmentVariable("HASS_AGENT_SERVICE_AUTH");
                            if (!string.IsNullOrEmpty(expected2))
                            {
                                if (doc.RootElement.TryGetProperty("auth", out var a))
                                {
                                    var provided = a.GetString() ?? string.Empty;
                                    valid = provided == expected2;
                                }
                                else valid = false;
                            }

                            if (valid)
                            {
                                try
                                {
                                    await HASS.Agent.Core.DiscoveryPublisher.ClearAllAsync(mqtt);
                                    resp = new { ok = true };
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(ex, "[RPC] clear_entities failed");
                                    resp = new { ok = false, error = "clear_failed" };
                                }
                            }
                            else
                            {
                                resp = new { ok = false, error = "auth_failed" };
                            }
                        }
                    }

                    var outb = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(resp));
                    await client.SendAsync(outb, SocketFlags.None);
                    client.Shutdown(SocketShutdown.Both);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[RPC] Server error");
            }
            finally
            {
                try { if (File.Exists(_socketPath)) File.Delete(_socketPath); } catch { }
            }
        }
    }
}
