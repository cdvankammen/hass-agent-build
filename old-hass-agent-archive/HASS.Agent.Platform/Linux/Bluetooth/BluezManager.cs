using System;
using System.Threading.Tasks;
using Serilog;
using Tmds.DBus;

namespace HASS.Agent.Platform.Linux.Bluetooth
{
    public class BluezManager
    {
        private Connection? _connection;
        public bool DbUsAvailable { get; private set; } = false;

        public BluezManager()
        {
            try
            {
                _connection = Connection.System;
                DbUsAvailable = true;
                Log.Information("[BlueZ] DBus connection configured (system bus)");
            }
            catch (Exception ex)
            {
                DbUsAvailable = false;
                Log.Warning(ex, "[BlueZ] DBus not available, will use fallbacks");
            }
        }

        public async Task<string[]> ListAdaptersAsync()
        {
            if (!DbUsAvailable || _connection == null) return Array.Empty<string>();
            try
            {
                var dbus = _connection.CreateProxy<IDBus>("org.freedesktop.DBus", "/org/freedesktop/DBus");
                var names = await dbus.ListNamesAsync();
                // naive: return names containing org.bluez
                var arr = new System.Collections.Generic.List<string>();
                foreach (var n in names)
                {
                    if (n.StartsWith("org.bluez")) arr.Add(n);
                }
                return arr.ToArray();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[BlueZ] Listing adapters via DBus failed");
            }
            return Array.Empty<string>();
        }

        public static bool IsDbusAvailableStatic()
        {
            try
            {
                var conn = Connection.System;
                return conn != null;
            }
            catch { return false; }
        }
    }

    [DBusInterface("org.freedesktop.DBus")]
    interface IDBus : IDBusObject
    {
        Task<string[]> ListNamesAsync();
    }
}

namespace HASS.Agent.Platform.Linux.Bluetooth
{
    public static class BluezAvailability
    {
        public static bool BluezManagerIsAvailable() => BluezManager.IsDbusAvailableStatic();
    }
}
