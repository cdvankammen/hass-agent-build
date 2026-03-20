using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using HASS.Agent.Core;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// ensure config path exists and copy samples if missing
System.IO.Directory.CreateDirectory(VariablesCore.ConfigPath);
var commandsFile = System.IO.Path.Combine(VariablesCore.ConfigPath, "commands.json");
var sensorsFile = System.IO.Path.Combine(VariablesCore.ConfigPath, "sensors.json");
if (!System.IO.File.Exists(commandsFile) && System.IO.File.Exists(System.IO.Path.Combine(AppContext.BaseDirectory, "commands.json")))
    System.IO.File.Copy(System.IO.Path.Combine(AppContext.BaseDirectory, "commands.json"), commandsFile);
if (!System.IO.File.Exists(sensorsFile) && System.IO.File.Exists(System.IO.Path.Combine(AppContext.BaseDirectory, "sensors.json")))
    System.IO.File.Copy(System.IO.Path.Combine(AppContext.BaseDirectory, "sensors.json"), sensorsFile);

app.MapGet("/commands", async ctx =>
{
    var list = await HASS.Agent.Core.StoredEntities.LoadCommandsAsync(commandsFile);
    ctx.Response.ContentType = "application/json";
    await ctx.Response.WriteAsJsonAsync(list);
});

app.MapGet("/info", async ctx =>
{
    await ctx.Response.WriteAsJsonAsync(new { config = VariablesCore.ConfigPath });
});

app.MapGet("/sensors", async ctx =>
{
    var mgr = new HASS.Agent.Core.SensorsManager(sensorsFile);
    var list = await mgr.GetSensorsAsync();
    await ctx.Response.WriteAsJsonAsync(list);
});

IMqttManager mqtt;
var broker = Environment.GetEnvironmentVariable("HASS_AGENT_MQTT_BROKER");
if (!string.IsNullOrEmpty(broker)) mqtt = new HASS.Agent.Core.MqttNetManager();
else mqtt = new HASS.Agent.Core.DummyMqttManager();

var commandsManager = new HASS.Agent.Core.CommandsManager(mqtt);
commandsManager.SetCommandsFile(commandsFile);

// attempt discovery publish
_ = Task.Run(async () => await HASS.Agent.Core.DiscoveryPublisher.PublishAllAsync(mqtt));

app.MapPost("/discovery", async ctx =>
{
    await HASS.Agent.Core.DiscoveryPublisher.PublishAllAsync(mqtt);
    await ctx.Response.WriteAsync("ok");
});

app.MapPost("/import/legacy", async ctx =>
{
    // accept optional JSON body containing legacy commands array or import instructions
    string body;
    using (var sr = new System.IO.StreamReader(ctx.Request.Body)) body = await sr.ReadToEndAsync();

    var legacyCommands = new System.Collections.Generic.List<HASS.Agent.Core.ConfiguredCommand>();
    var legacySensors = new System.Collections.Generic.List<HASS.Agent.Core.ConfiguredSensor>();

    string? importFilePath = null;

    if (!string.IsNullOrWhiteSpace(body))
    {
        try
        {
            // try to parse as an array of commands
            legacyCommands = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.List<HASS.Agent.Core.ConfiguredCommand>>(body) ?? new System.Collections.Generic.List<HASS.Agent.Core.ConfiguredCommand>();
            // if body contains an object with filePath or zipBase64, parse that
            var obj = Newtonsoft.Json.Linq.JObject.Parse(body);
            if (obj["filePath"] != null) importFilePath = obj["filePath"]?.ToString();
            if (obj["zipBase64"] != null)
            {
                // decode zip to temp and extract commands.json/sensors.json
                var zipb = obj["zipBase64"]?.ToString();
                var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hass_import_") + System.Guid.NewGuid().ToString();
                System.IO.Directory.CreateDirectory(tmp);
                var zipPath = System.IO.Path.Combine(tmp, "import.zip");
                if (!string.IsNullOrEmpty(zipb))
                {
                    System.IO.File.WriteAllBytes(zipPath, System.Convert.FromBase64String(zipb));
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tmp);
                    var cpath = System.IO.Path.Combine(tmp, "commands.json");
                    var spath = System.IO.Path.Combine(tmp, "sensors.json");
                    if (System.IO.File.Exists(cpath)) legacyCommands = await HASS.Agent.Core.LegacyStoredImport.LoadConfiguredCommandsAsync(cpath);
                    if (System.IO.File.Exists(spath)) legacySensors = await HASS.Agent.Core.LegacyStoredImport.LoadConfiguredSensorsAsync(spath);
                }
            }
        }
        catch
        {
            // ignore, will try filePath fallback
        }
    }

    // fallback: attempt to find legacy files in app base directory or in provided filePath
    var baseDir = AppContext.BaseDirectory;
    var candidates = new[] { System.IO.Path.Combine(baseDir, "commands.json"), System.IO.Path.Combine(baseDir, "../HASS.Agent/commands.json"), System.IO.Path.Combine(baseDir, "../HASS.Agent/Settings/commands.json") };
    var sensorCandidates = new[] { System.IO.Path.Combine(baseDir, "sensors.json"), System.IO.Path.Combine(baseDir, "../HASS.Agent/sensors.json"), System.IO.Path.Combine(baseDir, "../HASS.Agent/Settings/sensors.json") };
    if (!string.IsNullOrWhiteSpace(importFilePath))
    {
        if (System.IO.Directory.Exists(importFilePath))
        {
            var dirC = System.IO.Path.Combine(importFilePath, "commands.json");
            var dirS = System.IO.Path.Combine(importFilePath, "sensors.json");
              candidates = new[] { dirC };
              sensorCandidates = new[] { dirS };
        }
        else
        {
            candidates = new[] { importFilePath };
            sensorCandidates = new[] { importFilePath };
        }
    }
    foreach (var c in candidates)
    {
        if (legacyCommands.Count == 0 && System.IO.File.Exists(c))
        {
            legacyCommands = await HASS.Agent.Core.LegacyStoredImport.LoadConfiguredCommandsAsync(c);
        }
    }

    // sensors
    foreach (var c in sensorCandidates)
    {
        if (legacySensors.Count == 0 && System.IO.File.Exists(c))
        {
            legacySensors = await HASS.Agent.Core.LegacyStoredImport.LoadConfiguredSensorsAsync(c);
        }
    }

    // convert and persist
    var summary = new System.Collections.Generic.Dictionary<string, object>();
    var importedCommands = 0;
    var importedSensors = 0;
    var warnings = new System.Collections.Generic.List<string>();

    if (legacyCommands.Count > 0)
    {
        var mapped = HASS.Agent.Core.LegacyCommandMapper.MapConfiguredCommands(legacyCommands);
        await HASS.Agent.Core.StoredEntities.SaveCommandsAsync(commandsFile, mapped);
        importedCommands = mapped.Count;
    }

    if (legacySensors.Count > 0)
    {
        var list = new System.Collections.Generic.List<HASS.Agent.Core.SensorModel>();
        foreach (var ls in legacySensors)
        {
            var sm = HASS.Agent.Core.SensorConverter.ToSensorModel(ls);
            list.Add(sm);
        }
        await HASS.Agent.Core.StoredEntities.SaveSensorsAsync(sensorsFile, list);
        importedSensors = list.Count;
    }

    summary["commands"] = importedCommands;
    summary["sensors"] = importedSensors;
    summary["warnings"] = warnings;
    await ctx.Response.WriteAsJsonAsync(summary);
});

app.MapPost("/command", async ctx =>
{
    try
    {
        string body;
        using (var sr = new System.IO.StreamReader(ctx.Request.Body))
        {
            body = await sr.ReadToEndAsync();
        }

        HASS.Agent.Core.CommandModel? cmd = null;

        if (!string.IsNullOrWhiteSpace(body))
        {
            var trimmed = body.Trim();
            Console.WriteLine("POST /command body: " + trimmed);
            if (trimmed.StartsWith("{"))
            {
                try
                {
                    cmd = System.Text.Json.JsonSerializer.Deserialize<HASS.Agent.Core.CommandModel>(trimmed);
                }
                catch (System.Exception ex)
                {
                    Console.WriteLine("JSON deserialize error: " + ex.Message + " -- body: " + trimmed);
                    cmd = null;
                }
            }

            if (cmd == null)
            {
                // try simple formats: id=1, id:1, or just the id
                string? id = null;
                if (trimmed.Contains("id=")) id = trimmed.Split('=')[1].Trim();
                else if (trimmed.Contains("id:")) id = trimmed.Split(':')[1].Trim();
                else id = trimmed;

                if (!string.IsNullOrWhiteSpace(id))
                {
                    cmd = new HASS.Agent.Core.CommandModel { Id = id, Name = id, EntityType = "unknown", State = "ON" };
                }
                else
                {
                    // log body for debugging
                    Console.WriteLine("POST /command received unparseable body: " + trimmed);
                }
            }
        }

        if (cmd != null)
        {
            await commandsManager.ExecuteCommandAsync(cmd);

            // return updated commands list so GUI can refresh immediately
            var updated = await HASS.Agent.Core.StoredEntities.LoadCommandsAsync(commandsFile);
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsJsonAsync(updated);
            return;
        }

        ctx.Response.StatusCode = 400;
        await ctx.Response.WriteAsync("invalid");
    }
    catch
    {
        ctx.Response.StatusCode = 500;
        await ctx.Response.WriteAsync("error");
    }
});

app.MapGet("/service/status", async ctx =>
{
    await ctx.Response.WriteAsync(JsonSerializer.Serialize(new { exit = 0, output = "not-managed" }));
});

app.MapPost("/service/start", async ctx => { await ctx.Response.WriteAsync(JsonSerializer.Serialize(new { exit = 0, output = "ok" })); });
app.MapPost("/service/stop", async ctx => { await ctx.Response.WriteAsync(JsonSerializer.Serialize(new { exit = 0, output = "ok" })); });

// background sensor updater: reads sensors.json, updates simulated values, and publishes via MQTT
_ = Task.Run(async () =>
{
    var rnd = new Random();
    var mgr = new HASS.Agent.Core.SensorsManager(sensorsFile);
    while (true)
    {
        var sensors = await mgr.GetSensorsAsync();
        foreach (var s in sensors)
        {
            if (s.Id == "cpu") s.State = rnd.Next(0, 100) + "%";
            if (s.Id == "mem") s.State = rnd.Next(0, 100) + "%";

            await mqtt.PublishAsync($"hassagent/sensor/{s.Id}/state", System.Text.Json.JsonSerializer.Serialize(s));
        }

        System.IO.File.WriteAllText(sensorsFile, System.Text.Json.JsonSerializer.Serialize(sensors, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        await Task.Delay(TimeSpan.FromSeconds(30));
    }
});

app.Run("http://127.0.0.1:11111");
