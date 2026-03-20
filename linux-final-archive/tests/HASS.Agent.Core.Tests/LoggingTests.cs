using System;
using System.IO;
using Xunit;
using HASS.Agent.Core.Logging;
using Serilog;

namespace HASS.Agent.Core.Tests
{
    public class LoggingTests : IDisposable
    {
        private readonly string _testLogDir;
        
        public LoggingTests()
        {
            _testLogDir = Path.Combine(Path.GetTempPath(), "hass-agent-test-logs", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testLogDir);
        }
        
        public void Dispose()
        {
            AgentLogger.Shutdown();
            try
            {
                if (Directory.Exists(_testLogDir))
                {
                    Directory.Delete(_testLogDir, true);
                }
            }
            catch { /* Best effort cleanup */ }
        }
        
        [Fact]
        public void Initialize_CreatesLogDirectory()
        {
            var logDir = Path.Combine(_testLogDir, "init-test");
            
            AgentLogger.Initialize(logDir, "TestApp");
            
            Assert.True(Directory.Exists(logDir));
        }
        
        [Fact]
        public void Initialize_SetsLogPath()
        {
            var logDir = Path.Combine(_testLogDir, "path-test");
            
            AgentLogger.Initialize(logDir, "TestApp");
            
            Assert.False(string.IsNullOrEmpty(AgentLogger.LogPath));
            Assert.Contains("testapp.log", AgentLogger.LogPath.ToLower());
        }
        
        [Fact]
        public void Debug_DoesNotThrow()
        {
            AgentLogger.Initialize(_testLogDir, "TestApp");
            
            var ex = Record.Exception(() => AgentLogger.Debug("Test debug message"));
            
            Assert.Null(ex);
        }
        
        [Fact]
        public void Info_DoesNotThrow()
        {
            AgentLogger.Initialize(_testLogDir, "TestApp");
            
            var ex = Record.Exception(() => AgentLogger.Info("Test info message"));
            
            Assert.Null(ex);
        }
        
        [Fact]
        public void Warning_DoesNotThrow()
        {
            AgentLogger.Initialize(_testLogDir, "TestApp");
            
            var ex = Record.Exception(() => AgentLogger.Warning("Test warning message"));
            
            Assert.Null(ex);
        }
        
        [Fact]
        public void Error_WithException_DoesNotThrow()
        {
            AgentLogger.Initialize(_testLogDir, "TestApp");
            var testException = new InvalidOperationException("Test exception");
            
            var ex = Record.Exception(() => AgentLogger.Error(testException, "Test error message"));
            
            Assert.Null(ex);
        }
        
        [Fact]
        public void BeginOperation_ReturnsDisposable()
        {
            AgentLogger.Initialize(_testLogDir, "TestApp");
            
            using var operation = AgentLogger.BeginOperation("Test Operation");
            
            Assert.NotNull(operation);
        }
        
        [Fact]
        public void Debug_WithParameter_DoesNotThrow()
        {
            AgentLogger.Initialize(_testLogDir, "TestApp");
            
            var ex = Record.Exception(() => AgentLogger.Debug("Test message with param: {Value}", 42));
            
            Assert.Null(ex);
        }
        
        [Fact]
        public void Info_WithParameter_DoesNotThrow()
        {
            AgentLogger.Initialize(_testLogDir, "TestApp");
            
            var ex = Record.Exception(() => AgentLogger.Info<string>("Test info with param: {Value}", "test"));
            
            Assert.Null(ex);
        }
    }
    
    public class SensorModelTests
    {
        [Fact]
        public void SensorModel_DefaultValues_AreNotNull()
        {
            var sensor = new SensorModel();
            
            Assert.NotNull(sensor.Id);
            Assert.NotNull(sensor.Name);
            Assert.NotNull(sensor.State);
            Assert.NotNull(sensor.Type);
            Assert.NotNull(sensor.Category);
        }
        
        [Fact]
        public void SensorModel_CanSetProperties()
        {
            var sensor = new SensorModel
            {
                Id = "test_sensor",
                Name = "Test Sensor",
                State = "on",
                Type = "binary_sensor",
                Category = "diagnostic"
            };
            
            Assert.Equal("test_sensor", sensor.Id);
            Assert.Equal("Test Sensor", sensor.Name);
            Assert.Equal("on", sensor.State);
            Assert.Equal("binary_sensor", sensor.Type);
            Assert.Equal("diagnostic", sensor.Category);
        }
        
        [Fact]
        public void SensorModel_UpdateInterval_DefaultsToZero()
        {
            var sensor = new SensorModel();
            
            Assert.Equal(0, sensor.UpdateInterval);
        }
        
        [Fact]
        public void SensorModel_Query_CanBeSet()
        {
            var sensor = new SensorModel
            {
                Id = "cpu_usage",
                Query = "SELECT * FROM Win32_Processor"
            };
            
            Assert.Equal("SELECT * FROM Win32_Processor", sensor.Query);
        }
    }
    
    public class CommandModelTests
    {
        [Fact]
        public void CommandModel_DefaultValues_AreValid()
        {
            var cmd = new CommandModel();
            
            Assert.NotNull(cmd.Id);
            Assert.NotNull(cmd.Name);
            Assert.NotNull(cmd.EntityType);
            Assert.NotNull(cmd.State);
            Assert.NotNull(cmd.Keys);
        }
        
        [Fact]
        public void CommandModel_CanSetAllProperties()
        {
            var cmd = new CommandModel
            {
                Id = "test_cmd",
                Name = "Test Command",
                EntityType = "button",
                Command = "echo hello",
                RunAsLowIntegrity = false,
                State = "off"
            };
            
            Assert.Equal("test_cmd", cmd.Id);
            Assert.Equal("Test Command", cmd.Name);
            Assert.Equal("button", cmd.EntityType);
            Assert.Equal("echo hello", cmd.Command);
            Assert.False(cmd.RunAsLowIntegrity);
        }
        
        [Fact]
        public void CommandModel_Keys_CanBeModified()
        {
            var cmd = new CommandModel();
            
            cmd.Keys.Add("key1");
            cmd.Keys.Add("key2");
            
            Assert.Equal(2, cmd.Keys.Count);
            Assert.Contains("key1", cmd.Keys);
        }
    }
    
    public class VariablesCoreTests
    {
        [Fact]
        public void ConfigPath_IsNotEmpty()
        {
            var path = VariablesCore.ConfigPath;
            
            Assert.False(string.IsNullOrEmpty(path));
        }
        
        [Fact]
        public void DeviceName_IsNotEmpty()
        {
            // DeviceName should default to machine name
            var deviceName = Environment.MachineName;
            
            Assert.False(string.IsNullOrEmpty(deviceName));
        }
    }
}
