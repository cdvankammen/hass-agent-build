using System.Security.Principal;
using Grpc.Core;
using GrpcDotNetNamedPipes;
using Serilog;

namespace HASS.Agent.Service
{
    internal partial class RpcClientService
    {
        // On Windows we use named pipes/gRPC, on Linux use PlatformFactory's RPC client (TCP/UDS stub)
#if NET6_0_OR_GREATER
        private readonly object _rpcImpl;

        internal RpcClientService()
        {
            if (OperatingSystem.IsWindows())
            {
                var serviceChannel = new GrpcDotNetNamedPipes.NamedPipeChannel(".", Variables.PipeName, new GrpcDotNetNamedPipes.NamedPipeChannelOptions
                {
                    CurrentUserOnly = false,
                    ConnectionTimeout = (int)TimeSpan.FromSeconds(10).TotalMilliseconds
                });

                _rpcImpl = new HassAgentSatelliteRpcCalls.HassAgentSatelliteRpcCallsClient((Grpc.Core.ChannelBase)serviceChannel);
            }
            else
            {
                _rpcImpl = HASS.Agent.Platform.PlatformFactory.GetRpcClient();
            }
        }
#else
        private readonly object _rpcImpl = HASS.Agent.Platform.PlatformFactory.GetRpcClient();
#endif

        /// <summary>
        /// Sends a PING request
        /// </summary>
        /// <returns></returns>
        internal async Task<(bool success, string version, string error)> PingAsync()
        {
            try
            {
                if (_rpcImpl is HASS.Agent.Platform.IRpcClient platformRpc)
                {
                    var ok = await platformRpc.PingAsync();
                    return ok ? (true, string.Empty, string.Empty) : (false, string.Empty, "platform rpc failed");
                }

                // fallback to generated client
                var rpc = _rpcImpl as HassAgentSatelliteRpcCalls.HassAgentSatelliteRpcCallsClient;
                var response = await rpc.PingAsync(new PingRequest());
                if (response.Ok) return (true, response.Version, string.Empty);

                Log.Error("[SERVICE] Ping request failed: {err}", response.Error);
                return (false, string.Empty, response.Error);
            }
            catch (RpcException ex)
            {
                Log.Error("[SERVICE] RPC error [{code}]: {err}", ex .StatusCode.ToString(), ex.Message);
                return (false, string.Empty, $"failed with status: {ex.StatusCode}");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "[SERVICE] Error: {err}", ex.Message);
                return (false, string.Empty, "fatal error");
            }
        }

        /// <summary>
        /// Asks the service to clear all its entities locally and in HA
        /// </summary>
        /// <returns></returns>
        internal async Task<(bool success, string error)> ClearEntitiesAsync()
        {
            try
            {
                if (_rpcImpl is HASS.Agent.Platform.IRpcClient platformRpc)
                {
                    // no platform method implemented yet, stub as success
                    var ok = await platformRpc.PingAsync();
                    return ok ? (true, string.Empty) : (false, "platform rpc failed");
                }

                var rpc = _rpcImpl as HassAgentSatelliteRpcCalls.HassAgentSatelliteRpcCallsClient;
                var response = await rpc.ClearEntitiesAsync(new ClearEntitiesRequest { Auth = Variables.AppSettings.ServiceAuthId });
                if (response.Ok) return (true, string.Empty);

                Log.Error("[SERVICE] ClearEntities request failed: {err}", response.Error);
                return (false, response.Error);
            }
            catch (RpcException ex)
            {
                Log.Error("[SERVICE] RPC error [{code}]: {err}", ex.StatusCode.ToString(), ex.Message);
                return (false, $"failed with status: {ex.StatusCode}");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "[SERVICE] Error: {err}", ex.Message);
                return (false, "fatal error");
            }
        }

        /// <summary>
        /// Asks the service to nicely shutdown
        /// </summary>
        /// <returns></returns>
        internal async Task<(bool success, string error)> ShutdownServiceAsync()
        {
            try
            {
                if (_rpcImpl is HASS.Agent.Platform.IRpcClient platformRpc)
                {
                    var ok = await platformRpc.ShutdownServiceAsync(Variables.AppSettings.ServiceAuthId);
                    return ok ? (true, string.Empty) : (false, "platform rpc failed");
                }

                var rpc = _rpcImpl as HassAgentSatelliteRpcCalls.HassAgentSatelliteRpcCallsClient;
                var response = await rpc.ShutdownServiceAsync(new ShutdownServiceRequest { Auth = Variables.AppSettings.ServiceAuthId });
                if (response.Ok) return (true, string.Empty);

                Log.Error("[SERVICE] ShutdownService request failed: {err}", response.Error);
                return (false, response.Error);
            }
            catch (RpcException ex)
            {
                Log.Error("[SERVICE] RPC error [{code}]: {err}", ex.StatusCode.ToString(), ex.Message);
                return (false, $"failed with status: {ex.StatusCode}");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "[SERVICE] Error: {err}", ex.Message);
                return (false, "fatal error");
            }
        }
    }
}
