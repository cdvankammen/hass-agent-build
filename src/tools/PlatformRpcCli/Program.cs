using System;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

if (args.Length == 0)
{
    Console.WriteLine("Usage: PlatformRpcCli <cmd> [--auth <auth>] [--socket <socket>]");
    return 1;
}

string cmd = args[0];
string auth = string.Empty;
string socket = Environment.GetEnvironmentVariable("HASS_AGENT_RPC_SOCKET") ?? "tcp://127.0.0.1:52222";
for (int i = 1; i < args.Length; i++)
{
    if (args[i] == "--auth" && i + 1 < args.Length) auth = args[++i];
    if (args[i] == "--socket" && i + 1 < args.Length) socket = args[++i];
}

var payload = new { cmd, auth };
var json = JsonSerializer.Serialize(payload);

try
{
    if (socket.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase))
    {
        var parts = socket.Substring("tcp://".Length).Split(':');
        var host = parts[0];
        var port = int.Parse(parts[1]);
        using var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        client.Connect(host, port);
        var data = Encoding.UTF8.GetBytes(json);
        client.Send(data);
        var buffer = new byte[8192];
        var read = client.Receive(buffer);
        Console.WriteLine(Encoding.UTF8.GetString(buffer, 0, read));
    }
    else
    {
        using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        client.Connect(new UnixDomainSocketEndPoint(socket));
        var data = Encoding.UTF8.GetBytes(json);
        client.Send(data);
        var buffer = new byte[8192];
        var read = client.Receive(buffer);
        Console.WriteLine(Encoding.UTF8.GetString(buffer, 0, read));
    }
}
catch (Exception ex)
{
    Console.WriteLine("Error: " + ex.Message);
    return 2;
}

return 0;
