using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace HASS.Agent.Core
{
    public class SensorModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int UpdateInterval { get; set; }
        public string Query { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Counter { get; set; } = string.Empty;
        public string Instance { get; set; } = string.Empty;
        public string WindowName { get; set; } = string.Empty;
    }

    public static class SensorsLoader
    {
        public static List<SensorModel> Load(string file)
        {
            if (!File.Exists(file)) return new List<SensorModel>();
            var txt = File.ReadAllText(file);
            try
            {
                return JsonSerializer.Deserialize<List<SensorModel>>(txt) ?? new List<SensorModel>();
            }
            catch
            {
                return new List<SensorModel>();
            }
        }

        public static void Save(string file, List<SensorModel> sensors)
        {
            var txt = JsonSerializer.Serialize(sensors, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(file, txt);
        }
    }
}
