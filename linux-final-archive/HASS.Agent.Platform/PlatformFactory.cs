using System;
using HASS.Agent.Platform.Abstractions;
using HASS.Agent.Platform.Linux;
using HASS.Agent.Platform.Linux.Commands;
using HASS.Agent.Platform.Linux.Notifications;

namespace HASS.Agent.Platform
{
    public static class PlatformFactory
    {
        private static IInputSimulator? _inputSimulator;
        
        public static object? GetRpcClient()
        {
            if (!OperatingSystem.IsWindows()) return new PlatformRpcClient();
            return null;
        }

        public static INotifier? GetNotifier()
        {
            if (!OperatingSystem.IsWindows()) return new Linux.Notifications.Notifier();
            return null;
        }

        public static System.Func<string, bool>? GetCommandExecutor()
        {
            if (!OperatingSystem.IsWindows()) return (cmd) => CommandAdapter.Execute(cmd);
            return null;
        }
        
        /// <summary>
        /// Get the platform-specific input simulator for keyboard/mouse simulation
        /// </summary>
        public static IInputSimulator GetInputSimulator()
        {
            if (_inputSimulator != null) return _inputSimulator;
            
            if (OperatingSystem.IsLinux())
            {
                _inputSimulator = new Linux.Input.LinuxInputSimulator();
            }
            else if (OperatingSystem.IsMacOS())
            {
                _inputSimulator = new macOS.Input.MacOSInputSimulator();
            }
            else
            {
                throw new PlatformNotSupportedException("Input simulation not available for this platform");
            }
            
            return _inputSimulator;
        }
        
        /// <summary>
        /// Check if input simulation is available on this platform
        /// </summary>
        public static bool IsInputSimulatorAvailable()
        {
            try
            {
                var sim = GetInputSimulator();
                return sim.IsAvailable();
            }
            catch
            {
                return false;
            }
        }
    }
}
