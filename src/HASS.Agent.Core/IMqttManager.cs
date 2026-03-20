using System.Threading.Tasks;

namespace HASS.Agent.Core
{
    public interface IMqttManager
    {
        bool IsConnected();
        Task PublishAsync(string topic, string payload);
        Task PublishAsync(string topic, string payload, bool retain);
    }
}
