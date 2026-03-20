using System.Collections.Generic;
using System.Threading.Tasks;
using Serilog;

namespace HASS.Agent.Core
{
    public class CommandsManager
    {
        private readonly IMqttManager _mqtt;
        private string _commandsFile = string.Empty;

        public void SetCommandsFile(string file) => _commandsFile = file;

        public CommandsManager(IMqttManager mqtt)
        {
            _mqtt = mqtt;
        }

        public async Task ExecuteCommandAsync(CommandModel cmd)
        {
            Log.Information("Executing command {id} - {name}", cmd.Id, cmd.Name);
            var payload = System.Text.Json.JsonSerializer.Serialize(cmd);

            // persist state change if file configured
            if (!string.IsNullOrEmpty(_commandsFile))
            {
                var all = await StoredEntities.LoadCommandsAsync(_commandsFile);
                var found = all.Find(c => c.Id == cmd.Id);
                if (found != null)
                {
                    found.State = cmd.State;
                }
                else
                {
                    // add new command entry if it doesn't exist
                    var entry = new CommandModel
                    {
                        Id = cmd.Id,
                        Name = cmd.Name,
                        EntityType = cmd.EntityType,
                        State = cmd.State,
                        Command = cmd.Command,
                        KeyCode = cmd.KeyCode,
                        Keys = cmd.Keys,
                        RunAsLowIntegrity = cmd.RunAsLowIntegrity
                    };
                    all.Add(entry);
                }

                await StoredEntities.SaveCommandsAsync(_commandsFile, all);
            }

            await _mqtt.PublishAsync($"hassagent/command/{cmd.Id}", payload);
        }
    }
}
