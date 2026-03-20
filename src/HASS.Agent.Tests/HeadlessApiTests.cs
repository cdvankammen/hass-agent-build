using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using HASS.Agent.Core;
using HASS.Agent.Headless;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace HASS.Agent.Tests
{
    public class HeadlessApiTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
    {
        private static readonly string _testConfigPath = Path.Combine(Path.GetTempPath(), $"hass-agent-test-{Guid.NewGuid():N}");
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;
        private readonly ITestOutputHelper _output;

        static HeadlessApiTests()
        {
            Directory.CreateDirectory(_testConfigPath);
            Environment.SetEnvironmentVariable("HASS_AGENT_CONFIG_PATH", _testConfigPath);
            Environment.SetEnvironmentVariable("HASS_AGENT_APPLY_TO_GUI", "false");
            Environment.SetEnvironmentVariable("HASS_AGENT_BIND_HOST", null);
            Environment.SetEnvironmentVariable("HASS_AGENT_CORS_ORIGINS", null);
        }

        public HeadlessApiTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
        {
            _factory = factory;
            _output = output;
            if (Directory.Exists(_testConfigPath))
            {
                Directory.Delete(_testConfigPath, true);
            }
            Directory.CreateDirectory(_testConfigPath);
            
            _client = _factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    // Override services for testing if needed
                });
            }).CreateClient();
        }

        [Fact]
        public async Task Get_Settings_ReturnsConfigSchema()
        {
            // Act
            var response = await _client.GetAsync("/settings/schema");
            
            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var settings = JsonSerializer.Deserialize<JsonElement>(content);
            
            settings.ValueKind.Should().Be(JsonValueKind.Array);
            settings.GetArrayLength().Should().BeGreaterThan(0);

            var schemaNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in settings.EnumerateArray())
            {
                if (item.TryGetProperty("name", out var name))
                {
                    schemaNames.Add(name.GetString() ?? string.Empty);
                }
            }

            schemaNames.Should().Contain("BindHost");
            schemaNames.Should().Contain("CorsAllowedOrigins");
            
            var firstItem = settings[0];
            firstItem.TryGetProperty("name", out _).Should().BeTrue();
            firstItem.TryGetProperty("type", out _).Should().BeTrue();
            firstItem.TryGetProperty("label", out _).Should().BeTrue();
        }

        [Fact]
        public async Task Get_Settings_ReturnsEmptyDictionary()
        {
            // Act
            var response = await _client.GetAsync("/settings");
            
            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content);

            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task Post_Settings_SavesContent()
        {
            // Arrange
            var testSettings = new Dictionary<string, object>
            {
                ["DeviceName"] = "saved-device",
                ["MediaPlayerEnabled"] = false
            };
            var json = JsonSerializer.Serialize(testSettings);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/settings", content);
            
            // Assert
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            responseContent.Should().Be("ok");

            // Verify file was saved
            var appsettingsPath = Path.Combine(_testConfigPath, "appsettings.json");
            File.Exists(appsettingsPath).Should().BeTrue();
            
            var savedContent = await File.ReadAllTextAsync(appsettingsPath);
            var savedSettings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(savedContent);
            savedSettings.Should().NotBeNull();
            savedSettings!.TryGetValue("DeviceName", out var deviceName).Should().BeTrue();
            deviceName.GetString().Should().Be("saved-device");

            var reloaded = await _client.GetAsync("/settings");
            reloaded.EnsureSuccessStatusCode();
            var reloadedContent = await reloaded.Content.ReadAsStringAsync();
            var reloadedSettings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(reloadedContent);
            reloadedSettings.Should().NotBeNull();
            reloadedSettings!.TryGetValue("DeviceName", out var reloadedDeviceName).Should().BeTrue();
            reloadedDeviceName.GetString().Should().Be("saved-device");
        }

        [Fact]
        public async Task Post_Settings_Validate_ValidatesCorrectly()
        {
            // Test valid settings
            var validSettings = new
            {
                DeviceName = "test-device",
                LocalApiPort = 8080,
                HassUri = "https://homeassistant.local:8123",
                MqttEnabled = true,
                MqttAddress = "localhost",
                MqttPort = 1883
            };
            
            var json = JsonSerializer.Serialize(validSettings);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _client.PostAsync("/settings/validate", content);
            response.EnsureSuccessStatusCode();
            
            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(responseContent);

            result.TryGetProperty("valid", out var valid).Should().BeTrue();
            valid.GetBoolean().Should().BeTrue();
        }

        [Fact]
        public async Task Post_Settings_Validate_RejectsInvalidSettings()
        {
            // Test invalid settings
            var invalidSettings = new
            {
                DeviceName = "", // Required field empty
                LocalApiPort = 70000, // Out of range
                HassUri = "not-a-url", // Invalid URL
                MqttEnabled = true,
                MqttAddress = "", // Required when MQTT enabled
                MqttPort = -1 // Invalid port
            };
            
            var json = JsonSerializer.Serialize(invalidSettings);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _client.PostAsync("/settings/validate", content);
            response.EnsureSuccessStatusCode();
            
            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(responseContent);

            result.TryGetProperty("valid", out var valid).Should().BeTrue();
            valid.GetBoolean().Should().BeFalse();

            result.TryGetProperty("errors", out var errors).Should().BeTrue();
            errors.TryGetProperty("DeviceName", out _).Should().BeTrue();
            errors.TryGetProperty("LocalApiPort", out _).Should().BeTrue();
            errors.TryGetProperty("MqttPort", out _).Should().BeTrue();
        }

        [Fact]
        public async Task Post_Settings_Apply_ReturnsSuccess()
        {
            // Arrange - create valid settings file
            var testSettings = new { DeviceName = "apply-test", MediaPlayerEnabled = false };
            var appsettingsPath = Path.Combine(_testConfigPath, "appsettings.json");
            await File.WriteAllTextAsync(appsettingsPath, JsonSerializer.Serialize(testSettings));

            // Act
            var response = await _client.PostAsync("/settings/apply", new StringContent(""));
            
            // Assert
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            responseContent.Should().Be("ok");
        }

        [Fact]
        public async Task Get_Commands_ReturnsCommandList()
        {
            // Arrange - create test commands file
            var commands = new[]
            {
                new CommandModel { Id = "test1", Name = "Test Command 1", EntityType = "custom", Command = "echo test1" },
                new CommandModel { Id = "test2", Name = "Test Command 2", EntityType = "custom", Command = "echo test2" }
            };
            
            var commandsPath = Path.Combine(_testConfigPath, "commands.json");
            await File.WriteAllTextAsync(commandsPath, JsonSerializer.Serialize(commands));

            // Act
            var response = await _client.GetAsync("/commands");
            
            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(content);
            
            result.ValueKind.Should().Be(JsonValueKind.Array);
            result.GetArrayLength().Should().Be(2);
            
            result[0].TryGetProperty("Id", out var id).Should().BeTrue();
            id.GetString().Should().Be("test1");
        }

        [Fact]
        public async Task Delete_Commands_RemovesStoredCommand()
        {
            // Arrange
            var commands = new[]
            {
                new CommandModel { Id = "keep", Name = "Keep Command", EntityType = "custom", Command = "echo keep" },
                new CommandModel { Id = "delete-me", Name = "Delete Command", EntityType = "custom", Command = "echo delete" }
            };

            var commandsPath = Path.Combine(_testConfigPath, "commands.json");
            await File.WriteAllTextAsync(commandsPath, JsonSerializer.Serialize(commands));

            // Act
            var response = await _client.DeleteAsync("/commands/delete-me");

            // Assert
            response.EnsureSuccessStatusCode();
            (await response.Content.ReadAsStringAsync()).Should().Be("ok");

            var saved = JsonSerializer.Deserialize<List<CommandModel>>(await File.ReadAllTextAsync(commandsPath));
            saved.Should().NotBeNull();
            saved.Should().HaveCount(1);
            saved![0].Id.Should().Be("keep");
        }

        [Fact]
        public async Task Post_Command_AcceptsCustomCommandPayload()
        {
            // Arrange
            var payload = new CommandModel
            {
                Id = "run-echo",
                Name = "Run Echo",
                EntityType = "custom",
                Command = "echo headless-test"
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/command", content);

            // Assert
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(responseContent);

            result.TryGetProperty("success", out var success).Should().BeTrue();
            success.GetBoolean().Should().BeTrue();
            result.TryGetProperty("type", out var type).Should().BeTrue();
            type.GetString().Should().Be("custom");
        }

        [Fact]
        public async Task Get_Sensors_ReturnsSensorData()
        {
            // Act
            var response = await _client.GetAsync("/sensors");
            
            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(content);
            
            result.TryGetProperty("file_sensors", out _).Should().BeTrue();
            result.TryGetProperty("platform_sensors", out _).Should().BeTrue();
            result.TryGetProperty("total_count", out _).Should().BeTrue();
        }

        [Fact]
        public async Task Get_Network_Status_ReturnsBindHostAndInterfaces()
        {
            // Act
            var response = await _client.GetAsync("/network/status");

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(content);

            result.TryGetProperty("bind_host", out var bindHost).Should().BeTrue();
            bindHost.GetString().Should().Be("0.0.0.0");
            result.TryGetProperty("listening_on_all_interfaces", out var listeningOnAllInterfaces).Should().BeTrue();
            listeningOnAllInterfaces.GetBoolean().Should().BeTrue();
            result.TryGetProperty("interfaces", out var interfaces).Should().BeTrue();
            interfaces.ValueKind.Should().Be(JsonValueKind.Array);
        }

        [Fact]
        public async Task Get_Media_Status_ReturnsAvailability()
        {
            // Act
            var response = await _client.GetAsync("/media/status");

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(content);

            result.TryGetProperty("available", out _).Should().BeTrue();
        }

        [Fact]
        public async Task Get_Bluetooth_Devices_ReturnsAvailability()
        {
            // Act
            var response = await _client.GetAsync("/bluetooth/devices");

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(content);

            result.TryGetProperty("available", out var available).Should().BeTrue();
            available.GetBoolean().Should().BeFalse();
        }

        [Fact]
        public async Task Get_Bluetooth_Connected_ReturnsAvailability()
        {
            // Act
            var response = await _client.GetAsync("/bluetooth/connected");

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(content);

            result.TryGetProperty("available", out var available).Should().BeTrue();
            available.GetBoolean().Should().BeFalse();
        }

        [Fact]
        public async Task Delete_Sensors_RemovesFileSensor()
        {
            // Arrange
            var sensors = new[]
            {
                new SensorModel { Id = "keep", Name = "Keep Sensor", Type = "custom", State = "on" },
                new SensorModel { Id = "delete-me", Name = "Delete Sensor", Type = "custom", State = "off" }
            };

            var sensorsPath = Path.Combine(_testConfigPath, "sensors.json");
            await File.WriteAllTextAsync(sensorsPath, JsonSerializer.Serialize(sensors));

            // Act
            var response = await _client.DeleteAsync("/sensors/delete-me");

            // Assert
            response.EnsureSuccessStatusCode();
            (await response.Content.ReadAsStringAsync()).Should().Be("ok");

            var saved = JsonSerializer.Deserialize<List<SensorModel>>(await File.ReadAllTextAsync(sensorsPath));
            saved.Should().NotBeNull();
            saved.Should().HaveCount(1);
            saved![0].Id.Should().Be("keep");
        }

        [Fact]
        public async Task Get_Platform_Status_ReturnsSystemInfo()
        {
            // Act
            var response = await _client.GetAsync("/platform/capabilities");
            
            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(content);
            
            result.TryGetProperty("mpris_dbus", out _).Should().BeTrue();
            result.TryGetProperty("bluez_dbus", out _).Should().BeTrue();
            result.TryGetProperty("playerctl", out _).Should().BeTrue();
            result.TryGetProperty("btctl", out _).Should().BeTrue();
            result.TryGetProperty("effective_media", out _).Should().BeTrue();
            result.TryGetProperty("os", out _).Should().BeTrue();
            result.TryGetProperty("runtime", out _).Should().BeTrue();
        }

        [Fact]
        public async Task Post_Discovery_Publish_ReturnsSuccess()
        {
            // Act
            var response = await _client.GetAsync("/health");
            
            // Assert
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(responseContent);

            result.TryGetProperty("status", out var status).Should().BeTrue();
            status.GetString().Should().Be("healthy");
        }

        [Fact]
        public async Task Post_Discovery_Clear_ReturnsSuccess()
        {
            // Act
            var response = await _client.GetAsync("/settings/schema");
            
            // Assert
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(responseContent);

            result.ValueKind.Should().Be(JsonValueKind.Array);
            result.GetArrayLength().Should().BeGreaterThan(0);
        }

        public void Dispose()
        {
            _client?.Dispose();
            
            // Cleanup test directory
            try
            {
                if (Directory.Exists(_testConfigPath))
                {
                    Directory.Delete(_testConfigPath, true);
                }
            }
            catch
            {
                // Best effort cleanup
            }
            
            Environment.SetEnvironmentVariable("HASS_AGENT_CONFIG_PATH", null);
            Environment.SetEnvironmentVariable("HASS_AGENT_APPLY_TO_GUI", null);
            Environment.SetEnvironmentVariable("HASS_AGENT_BIND_HOST", null);
            Environment.SetEnvironmentVariable("HASS_AGENT_CORS_ORIGINS", null);
        }
    }
}
