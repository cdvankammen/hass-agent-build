using System;
using System.Collections.Generic;

namespace HASS.Agent.Core
{
    public class ConfiguredCommand
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string Args { get; set; } = string.Empty;
        public string KeyCode { get; set; } = string.Empty;
        public System.Collections.Generic.List<string> Keys { get; set; } = new System.Collections.Generic.List<string>();
        public bool RunAsLowIntegrity { get; set; }
    }

    public static class CommandConverter
    {
        public static CommandModel ToCommandModel(ConfiguredCommand cfg)
        {
            return new CommandModel
            {
                Id = cfg.Id.ToString(),
                Name = cfg.Name,
                EntityType = cfg.EntityType,
                State = "OFF",
                Command = cfg.Command,
                Args = cfg.Args,
                KeyCode = cfg.KeyCode,
                Keys = cfg.Keys,
                RunAsLowIntegrity = cfg.RunAsLowIntegrity
            };
        }

        public static ConfiguredCommand FromCommandModel(CommandModel model)
        {
            return new ConfiguredCommand
            {
                Id = Guid.TryParse(model.Id, out var g) ? g : Guid.NewGuid(),
                Name = model.Name,
                EntityType = model.EntityType,
                Command = model.Command ?? string.Empty,
                Args = model.Args ?? string.Empty,
                Type = "Custom",
                KeyCode = model.KeyCode ?? string.Empty,
                Keys = model.Keys ?? new System.Collections.Generic.List<string>(),
                RunAsLowIntegrity = model.RunAsLowIntegrity
            };
        }
    }
}
