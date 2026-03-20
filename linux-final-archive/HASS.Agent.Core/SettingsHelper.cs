using System;
using System.IO;
using System.Text.Json;
using Serilog;

namespace HASS.Agent.Core
{
    public static class SettingsHelper
    {
        // Read a couple of important flags from appsettings.json without needing GUI types.
        // Returns (MediaPlayerEnabled?, NotificationsEnabled?) - null means key not present.
        public static (bool? MediaPlayerEnabled, bool? NotificationsEnabled) ReadSettings(string path)
        {
            try
            {
                if (!File.Exists(path)) return (null, null);
                var raw = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(raw)) return (null, null);

                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                bool? media = null;
                bool? notif = null;
                if (root.TryGetProperty("MediaPlayerEnabled", out var m) && (m.ValueKind == JsonValueKind.True || m.ValueKind == JsonValueKind.False)) media = m.GetBoolean();
                if (root.TryGetProperty("NotificationsEnabled", out var n) && (n.ValueKind == JsonValueKind.True || n.ValueKind == JsonValueKind.False)) notif = n.GetBoolean();

                return (media, notif);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[SETTINGS.HELPER] Error reading appsettings: {err}", ex.Message);
                return (null, null);
            }
        }
    }
}
