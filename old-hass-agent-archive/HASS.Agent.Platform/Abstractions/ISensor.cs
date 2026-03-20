using System.Collections.Generic;

namespace HASS.Agent.Platform.Abstractions
{
    /// <summary>
    /// Interface for platform-specific sensors that report data to Home Assistant.
    /// All sensors must implement this interface for proper type safety.
    /// </summary>
    public interface ISensor
    {
        /// <summary>
        /// Unique identifier for this sensor (used in MQTT topics and discovery)
        /// </summary>
        string Id { get; }
        
        /// <summary>
        /// Human-readable name for this sensor
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Gets the current state of the sensor as a dictionary.
        /// The dictionary should always contain a "state" key with the primary value.
        /// Additional keys provide attributes for Home Assistant.
        /// </summary>
        /// <returns>Dictionary containing sensor state and attributes</returns>
        Dictionary<string, object> GetState();
    }
}
