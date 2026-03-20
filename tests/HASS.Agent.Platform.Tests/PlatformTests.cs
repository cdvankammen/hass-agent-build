using System;
using System.Runtime.InteropServices;
using Xunit;
using HASS.Agent.Platform.Abstractions;
using HASS.Agent.Platform.Linux.Sensors;

namespace HASS.Agent.Platform.Tests
{
    public class ISensorInterfaceTests
    {
        [Fact]
        public void ISensor_Interface_HasRequiredMembers()
        {
            // This test verifies the interface contract
            var type = typeof(ISensor);
            
            // Should have Id property
            var idProperty = type.GetProperty("Id");
            Assert.NotNull(idProperty);
            Assert.Equal(typeof(string), idProperty.PropertyType);
            
            // Should have Name property
            var nameProperty = type.GetProperty("Name");
            Assert.NotNull(nameProperty);
            Assert.Equal(typeof(string), nameProperty.PropertyType);
            
            // Should have GetState method
            var getStateMethod = type.GetMethod("GetState");
            Assert.NotNull(getStateMethod);
        }
    }
    
    [Collection("Linux Sensors")]
    public class LinuxSensorTests
    {
        [Fact]
        public void DiskUsageSensor_CanBeCreated()
        {
            var sensor = new DiskUsageSensor();
            
            Assert.NotNull(sensor);
            Assert.NotNull(sensor.Id);
            Assert.NotNull(sensor.Name);
        }
        
        [Fact]
        public void DiskUsageSensor_Id_IsCorrect()
        {
            var sensor = new DiskUsageSensor("test_disk", "Test Disk");
            
            Assert.Equal("test_disk", sensor.Id);
            Assert.Equal("Test Disk", sensor.Name);
        }
        
        [Fact]
        public void DiskUsageSensor_GetState_ReturnsValidDictionary()
        {
            var sensor = new DiskUsageSensor();
            
            var state = sensor.GetState();
            
            Assert.NotNull(state);
            Assert.True(state.ContainsKey("state") || state.ContainsKey("disks"));
        }
        
        [Fact]
        public void NetworkInterfacesSensor_CanBeCreated()
        {
            var sensor = new NetworkInterfacesSensor();
            
            Assert.NotNull(sensor);
            Assert.Equal("network_interfaces", sensor.Id);
            Assert.Equal("Network Interfaces", sensor.Name);
        }
        
        [Fact]
        public void NetworkInterfacesSensor_GetState_ReturnsValidDictionary()
        {
            var sensor = new NetworkInterfacesSensor();
            
            var state = sensor.GetState();
            
            Assert.NotNull(state);
        }
        
        [Fact]
        public void BatterySensor_CanBeCreated()
        {
            var sensor = new BatterySensor();
            
            Assert.NotNull(sensor);
            Assert.Equal("battery", sensor.Id);
        }
        
        [Fact]
        public void BatterySensor_GetState_DoesNotThrow()
        {
            var sensor = new BatterySensor();
            
            var ex = Record.Exception(() => sensor.GetState());
            
            Assert.Null(ex);
        }
        
        [Fact]
        public void SystemResourcesSensor_CanBeCreated()
        {
            var sensor = new SystemResourcesSensor();
            
            Assert.NotNull(sensor);
            Assert.NotNull(sensor.Id);
            Assert.NotNull(sensor.Name);
        }
        
        [Fact]
        public void SystemResourcesSensor_GetState_ReturnsResourceInfo()
        {
            var sensor = new SystemResourcesSensor();
            
            var state = sensor.GetState();
            
            Assert.NotNull(state);
        }
        
        [Fact]
        public void TemperatureSensor_CanBeCreated()
        {
            var sensor = new TemperatureSensor();
            
            Assert.NotNull(sensor);
            Assert.Equal("temperature", sensor.Id);
        }
        
        [Fact]
        public void TemperatureSensor_GetState_DoesNotThrow()
        {
            var sensor = new TemperatureSensor();
            
            var ex = Record.Exception(() => sensor.GetState());
            
            Assert.Null(ex);
        }
    }
    
    public class CrossPlatformTests
    {
        [Fact]
        public void CurrentPlatform_IsDetected()
        {
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
            var isMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            
            // At least one should be true
            Assert.True(isWindows || isLinux || isMacOS);
        }
        
        [Fact]
        public void RuntimeIdentifier_IsValid()
        {
            var rid = RuntimeInformation.RuntimeIdentifier;
            
            Assert.NotNull(rid);
            Assert.NotEmpty(rid);
        }
        
        [Fact]
        public void ProcessArchitecture_IsDetected()
        {
            var arch = RuntimeInformation.ProcessArchitecture;
            
            // Should be one of the known architectures
            Assert.True(
                arch == Architecture.X64 || 
                arch == Architecture.X86 || 
                arch == Architecture.Arm64 ||
                arch == Architecture.Arm
            );
        }
        
        [Fact]
        public void FrameworkDescription_IsNet()
        {
            var framework = RuntimeInformation.FrameworkDescription;
            
            Assert.NotNull(framework);
            Assert.Contains(".NET", framework);
        }
    }
}
