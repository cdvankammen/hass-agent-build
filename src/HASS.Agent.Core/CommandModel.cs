using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace HASS.Agent.Core
{
    public class CommandModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public string Args { get; set; } = string.Empty;
        public string KeyCode { get; set; } = string.Empty;
        public System.Collections.Generic.List<string> Keys { get; set; } = new System.Collections.Generic.List<string>();
        public bool RunAsLowIntegrity { get; set; }
    }

    public static class CommandsLoader
    {
        public static List<CommandModel> Load(string file)
        {
            if (!File.Exists(file)) return new List<CommandModel>();
            var txt = File.ReadAllText(file);
            try
            {
                return JsonSerializer.Deserialize<List<CommandModel>>(txt) ?? new List<CommandModel>();
            }
            catch
            {
                return new List<CommandModel>();
            }
        }

        public static void Save(string file, List<CommandModel> commands)
        {
            var txt = JsonSerializer.Serialize(commands, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(file, txt);
        }
    }
}
