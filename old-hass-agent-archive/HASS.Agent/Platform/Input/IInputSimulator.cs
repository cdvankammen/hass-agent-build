namespace HASS.Agent.Platform.Input
{
    public interface IInputSimulator
    {
        void SendKey(string key); // single key like "Return" or "ctrl+alt+t"
        void SendText(string text);
        void SendKeySequence(string sequence); // sequence like "ctrl+alt+t, type:hello"
    }
}
