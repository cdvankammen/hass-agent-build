using Microsoft.AspNetCore.Builder;
using Serilog;
using HASS.Agent.Core;
using HASS.Agent.Headless.Services;

namespace HASS.Agent.Headless
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Configure logging
            ConfigureLogging();
            
            Log.Information("HASS.Agent Headless starting...");
            
            try
            {
                // Create services
                var configService = new ConfigurationService();
                var mqtt = CreateMqttManager();
                var platformService = new PlatformService(configService, mqtt);
                var commandsManager = new CommandsManager(mqtt);
                commandsManager.SetCommandsFile(Path.Combine(configService.ConfigPath, "commands.json"));
                
                // Initialize platform adapters
                platformService.Initialize();
                
                // Build and configure web app
                var builder = WebApplication.CreateBuilder(args);
                builder.Host.UseSerilog();
                
                var app = builder.Build();
                
                // Enable static files for web UI
                app.UseDefaultFiles();
                app.UseStaticFiles();
                
                // Map all API endpoints
                app.MapApiEndpoints(configService, platformService, mqtt, commandsManager);
                
                // Send startup notification
                platformService.Notify("HASS.Agent", "Headless started");
                
                // Get port from config or use default
                var port = configService.ReadConfiguredInt("LocalApiPort", 11111);
                var listenUrl = $"http://127.0.0.1:{port}";
                
                app.Lifetime.ApplicationStarted.Register(() => 
                    Log.Information("Headless started on {url}", listenUrl));
                
                app.Lifetime.ApplicationStopping.Register(() =>
                {
                    Log.Information("Headless shutting down...");
                    platformService.Dispose();
                });
                
                app.Run(listenUrl);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Fatal error starting headless service");
                throw;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
        
        private static void ConfigureLogging()
        {
            var envLogPath = Environment.GetEnvironmentVariable("HASS_AGENT_LOG_PATH");
            var defaultLogDir = OperatingSystem.IsLinux() ? "/var/log/hass-agent" : Directory.GetCurrentDirectory();
            
            try
            {
                if (OperatingSystem.IsLinux() && !Directory.Exists(defaultLogDir))
                {
                    Directory.CreateDirectory(defaultLogDir);
                }
            }
            catch { /* Ignore if we can't create log dir */ }
            
            var logPath = !string.IsNullOrEmpty(envLogPath) 
                ? envLogPath 
                : Path.Combine(defaultLogDir, "hass-headless.log");
            
            try
            {
                var logDir = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }
            }
            catch { /* Ignore if we can't create log dir */ }
            
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
                .CreateLogger();
        }
        
        private static IMqttManager CreateMqttManager()
        {
            var broker = Environment.GetEnvironmentVariable("HASS_AGENT_MQTT_BROKER");
            
            if (!string.IsNullOrEmpty(broker))
            {
                Log.Information("Using MQTT broker from environment: {broker}", broker);
                return new MqttNetManager();
            }
            
            // Check if config file has MQTT settings
            var appSettingsPath = Path.Combine(VariablesCore.ConfigPath, "appsettings.json");
            if (File.Exists(appSettingsPath))
            {
                try
                {
                    var json = File.ReadAllText(appSettingsPath);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    
                    if (doc.RootElement.TryGetProperty("MqttEnabled", out var enabled) &&
                        enabled.ValueKind == System.Text.Json.JsonValueKind.True &&
                        doc.RootElement.TryGetProperty("MqttAddress", out var addr) &&
                        !string.IsNullOrWhiteSpace(addr.GetString()))
                    {
                        Log.Information("Using MQTT broker from config: {addr}", addr.GetString());
                        return new MqttNetManager();
                    }
                }
                catch { }
            }
            
            Log.Information("MQTT not configured, using dummy manager");
            return new DummyMqttManager();
        }
    }
}
