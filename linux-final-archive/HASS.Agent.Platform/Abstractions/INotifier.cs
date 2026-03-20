namespace HASS.Agent.Platform
{
    public interface INotifier
    {
        void Notify(string title, string message);
    }
}
