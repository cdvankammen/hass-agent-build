using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace HASS.Agent.Core.Tests
{
    public class ConfigurationServiceTests
    {
        [Fact]
        public void ConfigurationService_HasCorrectConfigPath()
        {
            // The config path should be set from VariablesCore
            var expectedConfigPath = VariablesCore.ConfigPath;
            Assert.False(string.IsNullOrEmpty(expectedConfigPath));
        }
        
        [Fact]
        public void VariablesCore_ConfigPath_IsValid()
        {
            var configPath = VariablesCore.ConfigPath;
            Assert.False(string.IsNullOrEmpty(configPath));
            
            // Should end with config or .config/hass-agent on Linux
            Assert.True(
                configPath.EndsWith("config") || 
                configPath.EndsWith("hass-agent") ||
                configPath.Contains("hass-agent"),
                $"Config path should contain 'hass-agent' or 'config': {configPath}"
            );
        }
    }
    
    public class CommandModelExtendedTests
    {
        [Fact]
        public void CommandModel_ShouldHandleNullValues()
        {
            var cmd = new CommandModel();
            
            Assert.NotNull(cmd.Id);
            Assert.NotNull(cmd.Name);
            Assert.NotNull(cmd.EntityType);
            Assert.NotNull(cmd.State);
        }
        
        [Fact]
        public void CommandModel_Keys_DefaultsToEmptyList()
        {
            var cmd = new CommandModel();
            Assert.NotNull(cmd.Keys);
            Assert.Empty(cmd.Keys);
        }
    }
    
    public class StoredEntitiesTests
    {
        [Fact]
        public async Task LoadCommandsAsync_ReturnsEmptyList_WhenFileDoesNotExist()
        {
            var nonExistentFile = "/tmp/non_existent_commands_" + Guid.NewGuid() + ".json";
            var result = await StoredEntities.LoadCommandsAsync(nonExistentFile);
            
            Assert.NotNull(result);
            Assert.Empty(result);
        }
        
        [Fact]
        public async Task LoadSensorsAsync_ReturnsEmptyList_WhenFileDoesNotExist()
        {
            var nonExistentFile = "/tmp/non_existent_sensors_" + Guid.NewGuid() + ".json";
            var result = await StoredEntities.LoadSensorsAsync(nonExistentFile);
            
            Assert.NotNull(result);
            Assert.Empty(result);
        }
        
        [Fact]
        public async Task SaveAndLoadCommands_RoundTrip()
        {
            var tempFile = "/tmp/test_commands_" + Guid.NewGuid() + ".json";
            
            try
            {
                var commands = new List<CommandModel>
                {
                    new CommandModel
                    {
                        Id = "test-cmd-1",
                        Name = "Test Command",
                        EntityType = "button",
                        State = "off"
                    }
                };
                
                await StoredEntities.SaveCommandsAsync(tempFile, commands);
                var loaded = await StoredEntities.LoadCommandsAsync(tempFile);
                
                Assert.Single(loaded);
                Assert.Equal("test-cmd-1", loaded[0].Id);
                Assert.Equal("Test Command", loaded[0].Name);
            }
            finally
            {
                if (System.IO.File.Exists(tempFile))
                    System.IO.File.Delete(tempFile);
            }
        }
        
        [Fact]
        public async Task SaveAndLoadSensors_RoundTrip()
        {
            var tempFile = "/tmp/test_sensors_" + Guid.NewGuid() + ".json";
            
            try
            {
                var sensors = new List<SensorModel>
                {
                    new SensorModel
                    {
                        Id = "test-sensor-1",
                        Name = "Test Sensor"
                    }
                };
                
                await StoredEntities.SaveSensorsAsync(tempFile, sensors);
                var loaded = await StoredEntities.LoadSensorsAsync(tempFile);
                
                Assert.Single(loaded);
                Assert.Equal("test-sensor-1", loaded[0].Id);
                Assert.Equal("Test Sensor", loaded[0].Name);
            }
            finally
            {
                if (System.IO.File.Exists(tempFile))
                    System.IO.File.Delete(tempFile);
            }
        }
    }
    
    public class DummyMqttManagerTests
    {
        [Fact]
        public void DummyMqttManager_ImplementsIMqttManager()
        {
            var mqtt = new DummyMqttManager();
            Assert.IsAssignableFrom<IMqttManager>(mqtt);
        }
        
        [Fact]
        public async Task DummyMqttManager_PublishDoesNotThrow()
        {
            var mqtt = new DummyMqttManager();
            
            // Should not throw
            await mqtt.PublishAsync("test/topic", "test payload");
            await mqtt.PublishAsync("test/topic", "test payload", retain: true);
        }
    }
}
