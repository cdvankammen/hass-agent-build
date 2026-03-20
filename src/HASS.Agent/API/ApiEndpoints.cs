using System.Text.Json;
using Grapevine;
using HASS.Agent.Enums;
using HASS.Agent.Extensions;
using HASS.Agent.Managers;
using HASS.Agent.Media;
using HASS.Agent.Models.HomeAssistant;
using HASS.Agent.MQTT;
using Serilog;
using HttpMethod = System.Net.Http.HttpMethod;

namespace HASS.Agent.API
{
    /// <summary>
    /// Endpoints for the local API
    /// </summary>
    public class ApiEndpoints : ApiDeserialization
    {
        /// <summary>
        /// Info routes, provides device info on /info
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static async Task DeviceInfoRoute(IHttpContext context)
        {
            context.Response.ContentType = "application/json";
            await context.Response.SendResponseAsync(JsonSerializer.Serialize(new
            {
                serial_number = Variables.SerialNumber,
                device = Variables.DeviceConfig,
                apis = new
                {
                    notifications = Variables.AppSettings.NotificationsEnabled,
                    media_player = Variables.AppSettings.MediaPlayerEnabled
                }
            }, MqttManager.JsonSerializerOptions));
        }

        /// <summary>
        /// Returns the list of configured commands
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static async Task ListCommandsRoute(IHttpContext context)
        {
            try
            {
                context.Response.ContentType = "application/json";

                var list = Variables.Commands?.Select(c => new
                {
                    id = c.Id,
                    name = c.Name,
                    entityType = c.EntityType.ToString(),
                    state = c.State
                }) ?? Enumerable.Empty<object>();

                await context.Response.SendResponseAsync(JsonSerializer.Serialize(list, MqttManager.JsonSerializerOptions));
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "[LOCALAPI] Error while listing commands: {ex}", ex.Message);
                await context.Response.SendResponseAsync("error");
            }
        }

        /// <summary>
        /// Execute a command by name or id
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static async Task ExecuteCommandRoute(IHttpContext context)
        {
            try
            {
                var body = await DeserializeAsync<Dictionary<string, string>>(context.Request.InputStream, context.CancellationToken);
                if (body == null || (!body.ContainsKey("name") && !body.ContainsKey("id")))
                {
                    context.Response.StatusCode = 400;
                    await context.Response.SendResponseAsync("missing name or id");
                    return;
                }

                var name = body.ContainsKey("name") ? body["name"] : string.Empty;
                var id = body.ContainsKey("id") ? body["id"] : string.Empty;

                if (!string.IsNullOrEmpty(id))
                {
                    var command = Variables.Commands?.FirstOrDefault(x => x.Id == id);
                    if (command == null)
                    {
                        context.Response.StatusCode = 404;
                        await context.Response.SendResponseAsync("command not found");
                        return;
                    }

                    command.TurnOn();
                    await context.Response.SendResponseAsync("ok");
                    return;
                }

                if (!string.IsNullOrEmpty(name))
                {
                    CommandsManager.ExecuteCommandByName(name);
                    await context.Response.SendResponseAsync("ok");
                    return;
                }

                context.Response.StatusCode = 400;
                await context.Response.SendResponseAsync("invalid request");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "[LOCALAPI] Error while executing command: {ex}", ex.Message);
                context.Response.StatusCode = 500;
                await context.Response.SendResponseAsync("error");
            }
        }

        public static async Task ServiceStatusRoute(IHttpContext context)
        {
            try
            {
                var (exit, output) = await ExecuteServiceCtlAsync("status");
                context.Response.ContentType = "application/json";
                await context.Response.SendResponseAsync(JsonSerializer.Serialize(new { exit, output }));
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "[LOCALAPI] Error while getting service status: {ex}", ex.Message);
                context.Response.StatusCode = 500;
                await context.Response.SendResponseAsync("error");
            }
        }

        public static async Task ServiceStartRoute(IHttpContext context)
        {
            try
            {
                var (exit, output) = await ExecuteServiceCtlAsync("start");
                context.Response.ContentType = "application/json";
                await context.Response.SendResponseAsync(JsonSerializer.Serialize(new { exit, output }));
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "[LOCALAPI] Error while starting service: {ex}", ex.Message);
                context.Response.StatusCode = 500;
                await context.Response.SendResponseAsync("error");
            }
        }

        public static async Task ServiceStopRoute(IHttpContext context)
        {
            try
            {
                var (exit, output) = await ExecuteServiceCtlAsync("stop");
                context.Response.ContentType = "application/json";
                await context.Response.SendResponseAsync(JsonSerializer.Serialize(new { exit, output }));
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "[LOCALAPI] Error while stopping service: {ex}", ex.Message);
                context.Response.StatusCode = 500;
                await context.Response.SendResponseAsync("error");
            }
        }

        private static async Task<(int exitCode, string output)> ExecuteServiceCtlAsync(string action)
        {
            // try --user first, then system
            string[] argsUser = { "--user", action, "hass-agent" };
            string[] argsSystem = { action, "hass-agent" };

            async Task<(int, string)> run(string[] args)
            {
                try
                {
                    var psi = new ProcessStartInfo("systemctl") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
                    psi.ArgumentList.Clear();
                    foreach (var a in args) psi.ArgumentList.Add(a);
                    using var p = Process.Start(psi);
                    if (p == null) return (127, "failed to start process");
                    var outt = await p.StandardOutput.ReadToEndAsync();
                    var err = await p.StandardError.ReadToEndAsync();
                    p.WaitForExit(5000);
                    var combined = (outt + "\n" + err).Trim();
                    return (p.ExitCode, combined);
                }
                catch (Exception ex)
                {
                    return (127, ex.Message);
                }
            }

            var res = await run(argsUser);
            if (res.Item1 == 0 || !string.IsNullOrEmpty(res.Item2)) return res;
            return await run(argsSystem);
        }
        
        /// <summary>
        /// Notification route, handles all incoming notifications on '/notify'
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static async Task NotifyRoute(IHttpContext context)
        {
            try
            {
                var notification = await DeserializeAsync<Notification>(context.Request.InputStream, context.CancellationToken);
                _ = Task.Run(() => NotificationManager.ShowNotification(notification));
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "[LOCALAPI] Error while processing incoming notification: {ex}", ex.Message);
            }
            finally
            {
                await context.Response.SendResponseAsync("notification processed");
            }
        }

        /// <summary>
        /// Media route, handles all incoming requests on '/media'
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static async Task MediaRoute(IHttpContext context)
        {
            try
            {
                if (context.Request.HttpMethod != HttpMethod.Get) return;
                if (!context.Request.QueryString.HasKeys()) return;

                var apiMediaRequest = context.Request.QueryString.ParseApiMediaRequest();

                switch (apiMediaRequest.RequestType)
                {
                    case MediaRequestType.Unknown:
                        // unable to parse, drop
                        return;

                    case MediaRequestType.Request:
                        // HA's waiting for info, have the mediamanager return it
                        await context.Response.SendResponseAsync(MediaManager.ProcessRequest(apiMediaRequest.Request));
                        break;

                    case MediaRequestType.Command:
                        // have HA wait for us to complete
                        MediaManager.ProcessCommand(apiMediaRequest.Command);
                        break;

                    case MediaRequestType.PlayMedia:
                        // media might take a while, process async
                        _ = Task.Run(() => MediaManager.ProcessMedia(apiMediaRequest.MediaUri));
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "[LOCALAPI] Error while processing incoming media request: {ex}", ex.Message);
            }
        }
    }
}
