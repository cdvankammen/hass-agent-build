using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;

namespace HASS.Agent.Core
{
    public static class StoredEntities
    {
        public static async Task<List<CommandModel>> LoadCommandsAsync(string file)
        {
            try
            {
                if (!File.Exists(file)) return new List<CommandModel>();
                var txt = await File.ReadAllTextAsync(file);
                if (string.IsNullOrWhiteSpace(txt)) return new List<CommandModel>();
                return JsonSerializer.Deserialize<List<CommandModel>>(txt) ?? new List<CommandModel>();
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "Error loading commands from {file}", file);
                return new List<CommandModel>();
            }
        }

        public static async Task SaveCommandsAsync(string file, List<CommandModel> commands)
        {
            try
            {
                var dir = Path.GetDirectoryName(file);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);
                var txt = JsonSerializer.Serialize(commands, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(file, txt);
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "Error saving commands to {file}", file);
            }
        }

        public static async Task<List<SensorModel>> LoadSensorsAsync(string file)
        {
            try
            {
                if (!File.Exists(file)) return new List<SensorModel>();
                var txt = await File.ReadAllTextAsync(file);
                if (string.IsNullOrWhiteSpace(txt)) return new List<SensorModel>();
                return JsonSerializer.Deserialize<List<SensorModel>>(txt) ?? new List<SensorModel>();
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "Error loading sensors from {file}", file);
                return new List<SensorModel>();
            }
        }

        public static async Task SaveSensorsAsync(string file, List<SensorModel> sensors)
        {
            try
            {
                var dir = Path.GetDirectoryName(file);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);
                var txt = JsonSerializer.Serialize(sensors, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(file, txt);
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "Error saving sensors to {file}", file);
            }
        }
    }
}
