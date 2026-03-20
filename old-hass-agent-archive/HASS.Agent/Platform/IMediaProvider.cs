using System.Threading.Tasks;

namespace HASS.Agent.Platform
{
    public interface IMediaProvider
    {
        Task<bool> InitializeAsync();
        Task StopAsync();
        int GetVolume();
        bool GetMuteState();
    }
}
