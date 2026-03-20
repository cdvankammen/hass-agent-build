using System;
using HASS.Agent.Platform.Linux;
using HASS.Agent.Platform.Linux.Commands;
using HASS.Agent.Platform.Linux.Notifications;

namespace HASS.Agent.Platform
{
    public static class PlatformFactory
    {
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
    }
}
