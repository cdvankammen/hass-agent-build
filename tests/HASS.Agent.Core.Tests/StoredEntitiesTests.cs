using System.IO;
using System.Threading.Tasks;
using Xunit;
using HASS.Agent.Core;

public class StoredEntitiesTests
{
    [Fact]
    public async Task SaveAndLoadCommands()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "hassagent_test_commands.json");
        if (File.Exists(tmp)) File.Delete(tmp);

        var list = new System.Collections.Generic.List<CommandModel>
        {
            new CommandModel { Id = "1", Name = "Test", EntityType = "Switch", State = "OFF" }
        };

        await StoredEntities.SaveCommandsAsync(tmp, list);
        var loaded = await StoredEntities.LoadCommandsAsync(tmp);

        Assert.Single(loaded);
        Assert.Equal("1", loaded[0].Id);

        File.Delete(tmp);
    }

    [Fact]
    public async Task SaveAndLoadSensors()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "hassagent_test_sensors.json");
        if (File.Exists(tmp)) File.Delete(tmp);

        var list = new System.Collections.Generic.List<SensorModel>
        {
            new SensorModel { Id = "cpu", Name = "CPU", State = "0%" }
        };

        await StoredEntities.SaveSensorsAsync(tmp, list);
        var loaded = await StoredEntities.LoadSensorsAsync(tmp);

        Assert.Single(loaded);
        Assert.Equal("cpu", loaded[0].Id);

        File.Delete(tmp);
    }
}
