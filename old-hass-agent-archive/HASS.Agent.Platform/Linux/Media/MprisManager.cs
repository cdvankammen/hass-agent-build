using System;
using System.Threading.Tasks;
using Serilog;
using Tmds.DBus;

namespace HASS.Agent.Platform.Linux.Media
{
    public class MprisManager
    {
        private Connection? _connection;
        public bool DbUsAvailable { get; private set; } = false;

        public MprisManager()
        {
            try
            {
                _connection = Connection.Session;
                DbUsAvailable = true;
                Log.Information("[MPRIS] DBus connection configured (system bus)");
            }
            catch (Exception ex)
            {
                DbUsAvailable = false;
                Log.Warning(ex, "[MPRIS] DBus not available, will use fallbacks");
            }
        }

        public async Task<bool> PlayPauseAsync()
        {
            if (!DbUsAvailable || _connection == null) return false;
            try
            {
                // Simple attempt: call org.freedesktop.DBus.ListNames to find MPRIS players
                var dbus = _connection.CreateProxy<IDBus>("org.freedesktop.DBus", "/org/freedesktop/DBus");
                var names = await dbus.ListNamesAsync();
                foreach (var n in names)
                {
                    if (n.StartsWith("org.mpris.MediaPlayer2."))
                    {
                        // call PlayPause on the player (introspection path)
                        var player = _connection.CreateProxy<IMediaPlayer>(n, "/org/mpris/MediaPlayer2");
                        await player.PlayPauseAsync();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[MPRIS] PlayPause via DBus failed");
            }
            return false;
        }

        public static bool IsDbusAvailableStatic()
        {
            try
            {
                var conn = Connection.Session;
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

    [DBusInterface("org.mpris.MediaPlayer2.Player")]
    interface IMediaPlayer : IDBusObject
    {
        Task PlayPauseAsync();
    }
}

// helper for headless
namespace HASS.Agent.Platform.Linux.Media
{
    public static class MprisAvailability
    {
        public static bool MprisManagerIsAvailable() => MprisManager.IsDbusAvailableStatic();
    }
}
