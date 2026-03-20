using System.Collections.Generic;
using System.Threading.Tasks;

namespace HASS.Agent.Platform.Abstractions
{
    /// <summary>
    /// Cross-platform keyboard and input simulation interface
    /// </summary>
    public interface IInputSimulator
    {
        /// <summary>
        /// Send a single key press (e.g., "Return", "Escape", "F1")
        /// </summary>
        /// <param name="key">Key name or code</param>
        /// <returns>True if successful</returns>
        bool SendKey(string key);
        
        /// <summary>
        /// Send a key combination (e.g., "ctrl+alt+t", "super+d")
        /// </summary>
        /// <param name="combination">Key combination with + separator</param>
        /// <returns>True if successful</returns>
        bool SendKeyCombination(string combination);
        
        /// <summary>
        /// Type text character by character
        /// </summary>
        /// <param name="text">Text to type</param>
        /// <param name="delayMs">Delay between keystrokes in milliseconds</param>
        /// <returns>True if successful</returns>
        bool SendText(string text, int delayMs = 10);
        
        /// <summary>
        /// Execute a sequence of key commands (SendKeys-like syntax)
        /// Supports: {ENTER}, {TAB}, {ESC}, {BACKSPACE}, {DELETE}, +{key} (Shift), ^{key} (Ctrl), %{key} (Alt)
        /// </summary>
        /// <param name="sequence">Key sequence in SendKeys format</param>
        /// <returns>True if successful</returns>
        bool SendKeySequence(string sequence);
        
        /// <summary>
        /// Execute multiple key commands from a list
        /// </summary>
        /// <param name="keys">List of key commands</param>
        /// <returns>True if all successful</returns>
        bool SendMultipleKeys(IEnumerable<string> keys);
        
        /// <summary>
        /// Move mouse to position
        /// </summary>
        bool MoveMouse(int x, int y);
        
        /// <summary>
        /// Click mouse button
        /// </summary>
        /// <param name="button">1=left, 2=middle, 3=right</param>
        bool ClickMouse(int button = 1);
        
        /// <summary>
        /// Check if the input simulator is available (required tools installed)
        /// </summary>
        bool IsAvailable();
        
        /// <summary>
        /// Get platform-specific information about requirements
        /// </summary>
        string GetRequirements();
    }
}
