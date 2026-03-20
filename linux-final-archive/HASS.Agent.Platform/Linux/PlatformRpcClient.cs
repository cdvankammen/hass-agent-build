using System;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;

namespace HASS.Agent.Platform.Linux
{
    public class PlatformRpcClient : HASS.Agent.Platform.IRpcClient
    {
        private readonly string _socketPath = "/var/run/hass-agent.sock";

        private async Task<JsonDocument?> SendAsync(object payload)
        {
            try
            {
                var json = JsonSerializer.Serialize(payload);
                var bytes = Encoding.UTF8.GetBytes(json + "\n");

                using var uds = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                var ep = new UnixDomainSocketEndPoint(_socketPath);
                await uds.ConnectAsync(ep);

                await uds.SendAsync(bytes, SocketFlags.None);

                var buffer = new byte[8192];
                var received = await uds.ReceiveAsync(buffer, SocketFlags.None);
                if (received == 0) return null;

                var resp = Encoding.UTF8.GetString(buffer, 0, received);
                return JsonDocument.Parse(resp);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[PLATFORM.LINUX][RPC] Error sending RPC: {err}", ex.Message);
                return null;
            }
        }

        public async Task<bool> PingAsync()
        {
            try
            {
                var resp = await SendAsync(new { cmd = "ping" });
                if (resp == null) return false;
                if (resp.RootElement.TryGetProperty("ok", out var ok)) return ok.GetBoolean();
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[PLATFORM.LINUX][RPC] Ping failed: {err}", ex.Message);
                return false;
            }
        }

        public async Task<bool> ShutdownServiceAsync(string auth)
        {
            try
            {
                var resp = await SendAsync(new { cmd = "shutdown", auth });
                if (resp == null) return false;
                if (resp.RootElement.TryGetProperty("ok", out var ok)) return ok.GetBoolean();
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[PLATFORM.LINUX][RPC] Shutdown failed: {err}", ex.Message);
                return false;
            }
        }

        public async Task<bool> ClearEntitiesAsync(string auth)
        {
            try
            {
                var resp = await SendAsync(new { cmd = "clear_entities", auth });
                if (resp == null) return false;
                if (resp.RootElement.TryGetProperty("ok", out var ok)) return ok.GetBoolean();
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[PLATFORM.LINUX][RPC] ClearEntities failed: {err}", ex.Message);
                return false;
            }
        }

        public async Task<string> GetVersionAsync()
        {
            try
            {
                var resp = await SendAsync(new { cmd = "ping" });
                if (resp == null) return string.Empty;
                if (resp.RootElement.TryGetProperty("version", out var v)) return v.GetString() ?? string.Empty;
                return string.Empty;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[PLATFORM.LINUX][RPC] GetVersion failed: {err}", ex.Message);
                return string.Empty;
            }
        }
    }
}
