using System;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Serilog;

namespace HASS.Agent.Platform
{
    // gRPC-over-TCP client that uses the generated gRPC client when available
    public class GrpcTcpRpcClient : IRpcClient, IDisposable
    {
        private readonly GrpcChannel _channel;
        private readonly HassAgentSatelliteRpcCalls.HassAgentSatelliteRpcCallsClient _client;

        public GrpcTcpRpcClient(string address = "http://127.0.0.1:50051")
        {
            try
            {
                _channel = GrpcChannel.ForAddress(address);
                var invoker = _channel.CreateCallInvoker();
                _client = new HassAgentSatelliteRpcCalls.HassAgentSatelliteRpcCallsClient(invoker);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[PLATFORM.RPC] Failed to initialize gRPC client");
                throw;
            }
        }

        public async Task<bool> PingAsync()
        {
            try
            {
                var response = await _client.PingAsync(new PingRequest());
                return response != null && response.Ok;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[PLATFORM.RPC] Ping failed");
                return false;
            }
        }

        public async Task<bool> ShutdownServiceAsync(string auth)
        {
            try
            {
                var response = await _client.ShutdownServiceAsync(new ShutdownServiceRequest { Auth = auth });
                return response != null && response.Ok;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[PLATFORM.RPC] Shutdown failed");
                return false;
            }
        }

        public void Dispose()
        {
            _channel?.Dispose();
        }
    }
}
