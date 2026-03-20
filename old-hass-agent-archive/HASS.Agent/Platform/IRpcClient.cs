using System.Threading.Tasks;

namespace HASS.Agent.Platform
{
    public interface IRpcClient
    {
        Task<bool> PingAsync();
        Task<bool> ShutdownServiceAsync(string auth);
    }
}
