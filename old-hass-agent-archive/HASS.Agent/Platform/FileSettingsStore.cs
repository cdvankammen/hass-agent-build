using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Serilog;

namespace HASS.Agent.Platform
{
    public class FileSettingsStore : ISettingsStore
    {
        private readonly string _configFile;
        private Dictionary<string,string> _store = new();

        public FileSettingsStore()
        {
            var configDir = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrEmpty(configDir)) configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".config");
            var appDir = Path.Combine(configDir, "hass-agent");
            if (!Directory.Exists(appDir)) Directory.CreateDirectory(appDir);
            _configFile = Path.Combine(appDir, "settings.json");

            Load();
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_configFile)) return;
                var txt = File.ReadAllText(_configFile);
                _store = JsonSerializer.Deserialize<Dictionary<string,string>>(txt) ?? new Dictionary<string,string>();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[PLATFORM.SETTINGS] Failed to load settings file");
                _store = new Dictionary<string,string>();
            }
        }

        private void Save()
        {
            try
            {
                var txt = JsonSerializer.Serialize(_store, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configFile, txt);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[PLATFORM.SETTINGS] Failed to save settings file");
            }
        }

        public string Get(string key, string? defaultValue = null)
        {
            return _store.ContainsKey(key) ? _store[key] : (defaultValue ?? string.Empty);
        }

        public void Set(string key, string value)
        {
            _store[key] = value;
            Save();
        }

        public bool Exists(string key) => _store.ContainsKey(key);
    }
}
