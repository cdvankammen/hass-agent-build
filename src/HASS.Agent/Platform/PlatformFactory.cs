using System;

namespace HASS.Agent.Platform
{
    public static class PlatformFactory
    {
        private static ISettingsStore? _settingsStore;
        private static IRpcClient? _rpcClient;
        private static INotifier? _notifier;
        private static Input.IInputSimulator? _inputSimulator;

        public static ISettingsStore GetSettingsStore()
        {
            if (_settingsStore != null) return _settingsStore;
            if (OperatingSystem.IsWindows())
            {
                // keep default behavior for Windows (Registry-based) - caller should handle Windows specifics
                throw new PlatformNotSupportedException("Windows settings store not implemented in platform layer");
            }

            _settingsStore = new FileSettingsStore();
            return _settingsStore;
        }

        public static IRpcClient GetRpcClient()
        {
            if (_rpcClient != null) return _rpcClient;
            // By default use the local TCP gRPC client
            var addr = Environment.GetEnvironmentVariable("HASS_AGENT_RPC_ADDR") ?? "http://127.0.0.1:50051";
            _rpcClient = new GrpcTcpRpcClient(addr);
            return _rpcClient;
        }

        public static INotifier GetNotifier()
        {
            if (_notifier != null) return _notifier;
            if (OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows notifier not implemented in platform layer");
            _notifier = new LibNotifyNotifier();
            return _notifier;
        }

        public static Input.IInputSimulator GetInputSimulator()
        {
            if (_inputSimulator != null) return _inputSimulator;
            if (OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows input simulator not implemented in platform layer");
            _inputSimulator = new Input.XdotoolInputSimulator();
            return _inputSimulator;
        }
    }
}
