namespace HASS.Agent.Platform
{
    public interface ISettingsStore
    {
        string Get(string key, string defaultValue = "");
        void Set(string key, string value);
        bool Exists(string key);
    }
}
