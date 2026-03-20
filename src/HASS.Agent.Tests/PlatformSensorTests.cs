using System;
using System.Collections.Generic;
using FluentAssertions;
using HASS.Agent.Platform.Linux.Sensors;
using Xunit;
using Xunit.Abstractions;

namespace HASS.Agent.Tests
{
    public class PlatformSensorTests
    {
        private readonly ITestOutputHelper _output;

        public PlatformSensorTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void DiskUsageSensor_ShouldReturnValidData()
        {
            // Arrange
            var sensor = new DiskUsageSensor("test_disk", "Test Disk");

            // Act
            var state = sensor.GetState();

            // Assert
            state.Should().NotBeNull();
            state.Should().ContainKey("state");
            state.Should().ContainKey("disks");
            state.Should().ContainKey("total_disks");
            
            state["state"].Should().BeOfType<string>();
            state["disks"].Should().BeAssignableTo<IEnumerable<object>>();
            
            _output.WriteLine($"Disk sensor state: {state["state"]}");
            _output.WriteLine($"Total disks: {state["total_disks"]}");
        }

        [Fact]
        public void NetworkInterfacesSensor_ShouldReturnValidData()
        {
            // Arrange
            var sensor = new NetworkInterfacesSensor("test_network", "Test Network");

            // Act
            var state = sensor.GetState();

            // Assert
            state.Should().NotBeNull();
            state.Should().ContainKey("state");
            state.Should().ContainKey("interfaces");
            state.Should().ContainKey("total_interfaces");
            state.Should().ContainKey("active_interfaces");
            
            state["state"].Should().BeOfType<string>();
            state["interfaces"].Should().BeAssignableTo<IEnumerable<object>>();
            
            _output.WriteLine($"Network sensor state: {state["state"]}");
            _output.WriteLine($"Total interfaces: {state["total_interfaces"]}");
            _output.WriteLine($"Active interfaces: {state["active_interfaces"]}");
        }

        [Fact]
        public void SystemResourcesSensor_ShouldReturnValidData()
        {
            // Arrange
            var sensor = new SystemResourcesSensor("test_system", "Test System");

            // Act
            var state = sensor.GetState();

            // Assert
            state.Should().NotBeNull();
            state.Should().ContainKey("state");
            state.Should().ContainKey("cpu_percent");
            state.Should().ContainKey("memory_total_mb");
            state.Should().ContainKey("memory_used_mb");
            state.Should().ContainKey("uptime_hours");
            
            state["state"].Should().Be("online");
            state["cpu_percent"].Should().BeOfType<double>();
            
            _output.WriteLine($"System sensor state: {state["state"]}");
            _output.WriteLine($"CPU usage: {state["cpu_percent"]}%");
            _output.WriteLine($"Memory total: {state["memory_total_mb"]} MB");
            _output.WriteLine($"Uptime: {state["uptime_hours"]} hours");
        }

        [Fact]
        public void Sensors_ShouldHaveRequiredProperties()
        {
            // Test that all sensors have the required duck-typed properties
            var sensors = new object[]
            {
                new DiskUsageSensor(),
                new NetworkInterfacesSensor(),
                new SystemResourcesSensor()
            };

            foreach (var sensor in sensors)
            {
                // Check for required properties
                var idProp = sensor.GetType().GetProperty("Id");
                var nameProp = sensor.GetType().GetProperty("Name");
                var getStateMethod = sensor.GetType().GetMethod("GetState");

                idProp.Should().NotBeNull($"Sensor {sensor.GetType().Name} should have Id property");
                nameProp.Should().NotBeNull($"Sensor {sensor.GetType().Name} should have Name property");
                getStateMethod.Should().NotBeNull($"Sensor {sensor.GetType().Name} should have GetState method");

                // Verify property values
                var id = idProp?.GetValue(sensor) as string;
                var name = nameProp?.GetValue(sensor) as string;

                id.Should().NotBeNullOrEmpty($"Sensor {sensor.GetType().Name} should have non-empty Id");
                name.Should().NotBeNullOrEmpty($"Sensor {sensor.GetType().Name} should have non-empty Name");

                // Verify GetState returns valid data
                var state = getStateMethod?.Invoke(sensor, null) as Dictionary<string, object>;
                state.Should().NotBeNull($"Sensor {sensor.GetType().Name} GetState should return valid data");
                state.Should().ContainKey("state", $"Sensor {sensor.GetType().Name} should include state");
            }
        }
    }
}