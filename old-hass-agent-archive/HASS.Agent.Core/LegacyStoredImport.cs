using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;

namespace HASS.Agent.Core
{
    public static class LegacyStoredImport
    {
        public static async Task<List<ConfiguredCommand>> LoadConfiguredCommandsAsync(string file)
        {
            try
            {
                if (!File.Exists(file)) return new List<ConfiguredCommand>();
                var txt = await File.ReadAllTextAsync(file);
                if (string.IsNullOrWhiteSpace(txt)) return new List<ConfiguredCommand>();
                return JsonConvert.DeserializeObject<List<ConfiguredCommand>>(txt) ?? new List<ConfiguredCommand>();
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "Error loading legacy commands from {file}", file);
                return new List<ConfiguredCommand>();
            }
        }

        public static async Task<List<ConfiguredSensor>> LoadConfiguredSensorsAsync(string file)
        {
            try
            {
                if (!File.Exists(file)) return new List<ConfiguredSensor>();
                var txt = await File.ReadAllTextAsync(file);
                if (string.IsNullOrWhiteSpace(txt)) return new List<ConfiguredSensor>();
                return JsonConvert.DeserializeObject<List<ConfiguredSensor>>(txt) ?? new List<ConfiguredSensor>();
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "Error loading legacy sensors from {file}", file);
                return new List<ConfiguredSensor>();
            }
        }
    }
}
