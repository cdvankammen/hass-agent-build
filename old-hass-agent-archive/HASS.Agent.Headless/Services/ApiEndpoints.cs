using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using HASS.Agent.Core;
using Serilog;

namespace HASS.Agent.Headless.Services
{
    /// <summary>
    /// Configures all REST API endpoints for the headless service
    /// </summary>
    public static class ApiEndpoints
    {
        public static void MapApiEndpoints(
            this WebApplication app,
            ConfigurationService config,
            PlatformService platform,
            IMqttManager mqtt,
            CommandsManager commandsManager)
        {
            // Health check
            app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));
            
            // Commands endpoints
            MapCommandsEndpoints(app, config);
            
            // Sensors endpoints
            MapSensorsEndpoints(app, config, platform);
            
            // Settings endpoints
            MapSettingsEndpoints(app, config, platform);
            
            // Platform status endpoint
            MapPlatformEndpoints(app, platform);
            
            // Media control endpoints
            MapMediaEndpoints(app, platform);
            
            // Bluetooth endpoints
            MapBluetoothEndpoints(app, platform);
            
            // Command execution endpoint
            MapExecutionEndpoints(app, commandsManager);
            
            // Service management endpoints
            MapServiceEndpoints(app);
        }
        
        private static void MapCommandsEndpoints(WebApplication app, ConfigurationService config)
        {
            var commandsFile = Path.Combine(config.ConfigPath, "commands.json");
            
            app.MapGet("/commands", async ctx =>
            {
                var list = CommandsLoader.Load(commandsFile);
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsJsonAsync(list);
            });
            
            app.MapPost("/commands", async ctx =>
            {
                try
                {
                    var commands = await JsonSerializer.DeserializeAsync<List<CommandModel>>(ctx.Request.Body);
                    if (commands != null)
                    {
                        await StoredEntities.SaveCommandsAsync(commandsFile, commands);
                        await ctx.Response.WriteAsync("ok");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error saving commands");
                }
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync("invalid");
            });
        }
        
        private static void MapSensorsEndpoints(WebApplication app, ConfigurationService config, PlatformService platform)
        {
            var sensorsFile = Path.Combine(config.ConfigPath, "sensors.json");
            
            app.MapGet("/sensors", async ctx =>
            {
                var fileSensors = SensorsLoader.Load(sensorsFile);
                var platformSensorData = platform.GetCurrentSensorData();
                
                var allSensors = new
                {
                    file_sensors = fileSensors,
                    platform_sensors = platformSensorData,
                    total_count = fileSensors.Count + platformSensorData.Count
                };
                
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsJsonAsync(allSensors);
            });
            
            app.MapPost("/sensors", async ctx =>
            {
                try
                {
                    var sensors = await JsonSerializer.DeserializeAsync<List<SensorModel>>(ctx.Request.Body);
                    if (sensors != null)
                    {
                        await StoredEntities.SaveSensorsAsync(sensorsFile, sensors);
                        await ctx.Response.WriteAsync("ok");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error saving sensors");
                }
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync("invalid");
            });
        }
        
        private static void MapSettingsEndpoints(WebApplication app, ConfigurationService config, PlatformService platform)
        {
            app.MapGet("/settings", async ctx =>
            {
                var settings = config.GetAllSettings();
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsJsonAsync(settings);
            });
            
            app.MapGet("/settings/schema", async ctx =>
            {
                var schema = config.GetSettingsSchema();
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsJsonAsync(schema);
            });
            
            app.MapPost("/settings", async ctx =>
            {
                try
                {
                    var settings = await JsonSerializer.DeserializeAsync<Dictionary<string, object>>(ctx.Request.Body);
                    if (settings != null && config.SaveSettings(settings))
                    {
                        // Reload platform with new settings
                        platform.ReloadConfiguration();
                        await ctx.Response.WriteAsync("ok");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error saving settings");
                }
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync("invalid");
            });
            
            app.MapPost("/settings/validate", async ctx =>
            {
                try
                {
                    var obj = await JsonSerializer.DeserializeAsync<JsonElement>(ctx.Request.Body);
                    var errors = config.ValidateSettings(obj);
                    
                    ctx.Response.ContentType = "application/json";
                    if (errors.Count == 0)
                    {
                        await ctx.Response.WriteAsJsonAsync(new { valid = true });
                    }
                    else
                    {
                        await ctx.Response.WriteAsJsonAsync(new { valid = false, errors });
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error validating settings");
                    ctx.Response.StatusCode = 400;
                    await ctx.Response.WriteAsync("invalid");
                }
            });
            
            app.MapPost("/settings/apply", async ctx =>
            {
                try
                {
                    platform.ReloadConfiguration();
                    await ctx.Response.WriteAsync("ok");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error applying settings");
                    ctx.Response.StatusCode = 500;
                    await ctx.Response.WriteAsync("error");
                }
            });
        }
        
        private static void MapPlatformEndpoints(WebApplication app, PlatformService platform)
        {
            app.MapGet("/platform/status", async ctx =>
            {
                try
                {
                    var caps = platform.GetCapabilities();
                    await ctx.Response.WriteAsJsonAsync(caps);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error getting platform status");
                    ctx.Response.StatusCode = 500;
                    await ctx.Response.WriteAsync("error");
                }
            });
            
            app.MapGet("/platform/capabilities", async ctx =>
            {
                try
                {
                    var caps = platform.GetCapabilities();
                    await ctx.Response.WriteAsJsonAsync(new
                    {
                        mpris_dbus = caps.MprisDbusAvailable,
                        bluez_dbus = caps.BluezDbusAvailable,
                        playerctl = caps.PlayerctlAvailable,
                        btctl = caps.BluetoothctlAvailable,
                        effective_media = caps.MediaEnabled,
                        os = Environment.OSVersion.ToString(),
                        runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
                    });
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error getting platform capabilities");
                    ctx.Response.StatusCode = 500;
                    await ctx.Response.WriteAsync("error");
                }
            });
        }
        
        private static void MapMediaEndpoints(WebApplication app, PlatformService platform)
        {
            app.MapPost("/media/play", async ctx =>
            {
                try
                {
                    var media = platform.MediaManager;
                    if (media != null)
                    {
                        await media.PlayAsync();
                        await ctx.Response.WriteAsync("ok");
                    }
                    else
                    {
                        ctx.Response.StatusCode = 503;
                        await ctx.Response.WriteAsync("media not available");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error with media play");
                    ctx.Response.StatusCode = 500;
                    await ctx.Response.WriteAsync("error");
                }
            });
            
            app.MapPost("/media/pause", async ctx =>
            {
                try
                {
                    var media = platform.MediaManager;
                    if (media != null)
                    {
                        await media.PauseAsync();
                        await ctx.Response.WriteAsync("ok");
                    }
                    else
                    {
                        ctx.Response.StatusCode = 503;
                        await ctx.Response.WriteAsync("media not available");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error with media pause");
                    ctx.Response.StatusCode = 500;
                    await ctx.Response.WriteAsync("error");
                }
            });
            
            app.MapPost("/media/next", async ctx =>
            {
                try
                {
                    var media = platform.MediaManager;
                    if (media != null)
                    {
                        await media.NextAsync();
                        await ctx.Response.WriteAsync("ok");
                    }
                    else
                    {
                        ctx.Response.StatusCode = 503;
                        await ctx.Response.WriteAsync("media not available");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error with media next");
                    ctx.Response.StatusCode = 500;
                    await ctx.Response.WriteAsync("error");
                }
            });
            
            app.MapPost("/media/previous", async ctx =>
            {
                try
                {
                    var media = platform.MediaManager;
                    if (media != null)
                    {
                        await media.PreviousAsync();
                        await ctx.Response.WriteAsync("ok");
                    }
                    else
                    {
                        ctx.Response.StatusCode = 503;
                        await ctx.Response.WriteAsync("media not available");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error with media previous");
                    ctx.Response.StatusCode = 500;
                    await ctx.Response.WriteAsync("error");
                }
            });
            
            app.MapGet("/media/status", async ctx =>
            {
                try
                {
                    var media = platform.MediaManager;
                    if (media != null)
                    {
                        var status = await media.GetStatusAsync();
                        await ctx.Response.WriteAsJsonAsync(status);
                    }
                    else
                    {
                        await ctx.Response.WriteAsJsonAsync(new { available = false });
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error getting media status");
                    ctx.Response.StatusCode = 500;
                    await ctx.Response.WriteAsync("error");
                }
            });
        }
        
        private static void MapBluetoothEndpoints(WebApplication app, PlatformService platform)
        {
            app.MapGet("/bluetooth/devices", async ctx =>
            {
                try
                {
                    var bt = platform.BluetoothManager;
                    if (bt != null)
                    {
                        var devices = await bt.GetPairedDevicesAsync();
                        await ctx.Response.WriteAsJsonAsync(devices);
                    }
                    else
                    {
                        await ctx.Response.WriteAsJsonAsync(new { available = false, devices = Array.Empty<object>() });
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error getting bluetooth devices");
                    ctx.Response.StatusCode = 500;
                    await ctx.Response.WriteAsync("error");
                }
            });
            
            app.MapGet("/bluetooth/connected", async ctx =>
            {
                try
                {
                    var bt = platform.BluetoothManager;
                    if (bt != null)
                    {
                        var devices = await bt.GetConnectedDevicesAsync();
                        await ctx.Response.WriteAsJsonAsync(devices);
                    }
                    else
                    {
                        await ctx.Response.WriteAsJsonAsync(new { available = false, devices = Array.Empty<object>() });
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error getting connected bluetooth devices");
                    ctx.Response.StatusCode = 500;
                    await ctx.Response.WriteAsync("error");
                }
            });
            
            app.MapPost("/bluetooth/connect", async ctx =>
            {
                try
                {
                    var body = await JsonSerializer.DeserializeAsync<JsonElement>(ctx.Request.Body);
                    if (body.TryGetProperty("mac", out var macProp))
                    {
                        var mac = macProp.GetString();
                        var bt = platform.BluetoothManager;
                        
                        if (bt != null && !string.IsNullOrEmpty(mac))
                        {
                            var success = await bt.ConnectAsync(mac);
                            await ctx.Response.WriteAsJsonAsync(new { success, mac });
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error connecting to bluetooth device");
                }
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync("invalid");
            });
            
            app.MapPost("/bluetooth/disconnect", async ctx =>
            {
                try
                {
                    var body = await JsonSerializer.DeserializeAsync<JsonElement>(ctx.Request.Body);
                    if (body.TryGetProperty("mac", out var macProp))
                    {
                        var mac = macProp.GetString();
                        var bt = platform.BluetoothManager;
                        
                        if (bt != null && !string.IsNullOrEmpty(mac))
                        {
                            var success = await bt.DisconnectAsync(mac);
                            await ctx.Response.WriteAsJsonAsync(new { success, mac });
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error disconnecting from bluetooth device");
                }
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync("invalid");
            });
        }
        
        private static void MapExecutionEndpoints(WebApplication app, CommandsManager commandsManager)
        {
            app.MapPost("/command", async ctx =>
            {
                try
                {
                    var cmd = await JsonSerializer.DeserializeAsync<CommandModel>(ctx.Request.Body);
                    if (cmd != null)
                    {
                        await commandsManager.ExecuteCommandAsync(cmd);
                        await ctx.Response.WriteAsync("ok");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error parsing command");
                }
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync("invalid");
            });
            
            app.MapPost("/execute", async ctx =>
            {
                try
                {
                    var body = await JsonSerializer.DeserializeAsync<JsonElement>(ctx.Request.Body);
                    if (body.TryGetProperty("command", out var cmdProp))
                    {
                        var cmdText = cmdProp.GetString();
                        if (!string.IsNullOrEmpty(cmdText))
                        {
                            var result = HASS.Agent.Platform.Linux.Commands.CommandAdapter.Execute(cmdText);
                            await ctx.Response.WriteAsJsonAsync(new { success = result, command = cmdText });
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error executing command");
                }
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync("invalid");
            });
        }
        
        private static void MapServiceEndpoints(WebApplication app)
        {
            app.MapGet("/service/status", async ctx =>
            {
                await ctx.Response.WriteAsJsonAsync(new { status = "running", managed = false });
            });
            
            app.MapPost("/service/restart", async ctx =>
            {
                // Schedule a restart
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    Environment.Exit(0); // Systemd will restart us
                });
                await ctx.Response.WriteAsync("restarting");
            });
        }
    }
}
