using System;

namespace HASS.Agent.Core
{
    public class ConfiguredSensor
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int UpdateInterval { get; set; }
        public string Query { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Counter { get; set; } = string.Empty;
        public string Instance { get; set; } = string.Empty;
        public string WindowName { get; set; } = string.Empty;
    }

    public static class SensorConverter
    {
        public static SensorModel ToSensorModel(ConfiguredSensor cfg)
        {
            return new SensorModel
            {
                Id = cfg.Id.ToString(),
                Name = cfg.Name,
                State = string.Empty,
                Type = cfg.Type,
                UpdateInterval = cfg.UpdateInterval,
                Query = cfg.Query,
                Scope = cfg.Scope,
                Category = cfg.Category,
                Counter = cfg.Counter,
                Instance = cfg.Instance,
                WindowName = cfg.WindowName
            };
        }

        public static ConfiguredSensor FromSensorModel(SensorModel model)
        {
            return new ConfiguredSensor
            {
                Id = Guid.TryParse(model.Id, out var g) ? g : Guid.NewGuid(),
                Name = model.Name,
                Type = model.Type ?? "Generic",
                UpdateInterval = model.UpdateInterval,
                Query = model.Query,
                Scope = model.Scope,
                Category = model.Category,
                Counter = model.Counter,
                Instance = model.Instance,
                WindowName = model.WindowName
            };
        }
    }
}
