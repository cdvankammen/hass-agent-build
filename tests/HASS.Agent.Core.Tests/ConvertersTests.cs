using System;
using Xunit;
using HASS.Agent.Core;

public class ConvertersTests
{
    [Fact]
    public void CommandConverter_Roundtrip()
    {
        var cfg = new ConfiguredCommand { Id = Guid.NewGuid(), Name = "Cmd1", Type = "Custom", Command = "echo hi", EntityType = "Switch", KeyCode = "K", Keys = new System.Collections.Generic.List<string>{"A","B"}, RunAsLowIntegrity = true };
        var model = CommandConverter.ToCommandModel(cfg);
        var back = CommandConverter.FromCommandModel(model);
        Assert.Equal(cfg.Name, model.Name);
        Assert.Equal(model.Name, back.Name);
        Assert.Equal(cfg.EntityType, model.EntityType);
    }

    [Fact]
    public void SensorConverter_Roundtrip()
    {
        var cfg = new ConfiguredSensor { Id = Guid.NewGuid(), Name = "CPU", Type = "CpuLoad", UpdateInterval = 10 };
        var model = SensorConverter.ToSensorModel(cfg);
        var back = SensorConverter.FromSensorModel(model);
        Assert.Equal(cfg.Name, model.Name);
        Assert.Equal(model.Name, back.Name);
    }

    [Fact]
    public void LegacyCommandMapper_MapsFields()
    {
        var legacy = new System.Collections.Generic.List<ConfiguredCommand>
        {
            new ConfiguredCommand { Id = Guid.NewGuid(), Name = "L1", Type = "Custom", Command = "doit", EntityType = "Switch", KeyCode = "K" }
        };

        var mapped = LegacyCommandMapper.MapConfiguredCommands(legacy);
        Assert.Single(mapped);
        Assert.Equal("L1", mapped[0].Name);
        Assert.Equal("doit", mapped[0].Command);
    }

    [Fact]
    public void SensorConverter_MapsExtraFields()
    {
        var cfg = new ConfiguredSensor { Id = Guid.NewGuid(), Name = "Net", Type = "NetworkSensors", UpdateInterval = 20, Query = "eth0", Scope = "local" , Category = "cat", Counter = "cnt", Instance = "inst", WindowName = "win" };
        var model = SensorConverter.ToSensorModel(cfg);
        var back = SensorConverter.FromSensorModel(model);
        Assert.Equal(cfg.Name, model.Name);
        Assert.Equal(model.Name, back.Name);
    }

    [Fact]
    public void LegacyCommandMapper_KnownTypes()
    {
        var legacy = new System.Collections.Generic.List<ConfiguredCommand>
        {
            new ConfiguredCommand { Id = Guid.NewGuid(), Name = "Keys", Type = "MultipleKeysCommand", Keys = new System.Collections.Generic.List<string>{"A","B"} },
            new ConfiguredCommand { Id = Guid.NewGuid(), Name = "Int", Type = "InternalCommand", Command = "cfg" }
        };

        var mapped = LegacyCommandMapper.MapConfiguredCommands(legacy);
        Assert.Equal(2, mapped.Count);
        Assert.Contains(mapped, m => m.Name == "Keys" && m.Keys.Count == 2);
        Assert.Contains(mapped, m => m.Name == "Int" && m.Command == "cfg");
    }

    [Fact]
    public void LegacyCommandMapper_PowerShellAndKeys()
    {
        var legacy = new System.Collections.Generic.List<ConfiguredCommand>
        {
            new ConfiguredCommand { Id = Guid.NewGuid(), Name = "PS", Type = "PowershellCommand", Command = "powershell.exe", Args = "-NoProfile -Command \"Get-Process\"" },
            new ConfiguredCommand { Id = Guid.NewGuid(), Name = "Keys", Type = "KeyCommand", KeyCode = "K1", Keys = new System.Collections.Generic.List<string>{"A","B"} }
        };

        var mapped = LegacyCommandMapper.MapConfiguredCommands(legacy);
        Assert.Equal(2, mapped.Count);
        Assert.Contains(mapped, m => m.Name == "PS" && m.EntityType == "powershell" && m.Command.Contains("Get-Process"));
        Assert.Contains(mapped, m => m.Name == "Keys" && m.EntityType == "key" && m.Keys.Count == 2 && m.KeyCode == "K1");
    }
}
