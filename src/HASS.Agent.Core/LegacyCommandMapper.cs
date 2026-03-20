using System.Collections.Generic;

namespace HASS.Agent.Core
{
    public static class LegacyCommandMapper
    {
        public static List<CommandModel> MapConfiguredCommands(List<ConfiguredCommand> legacy)
        {
            var result = new List<CommandModel>();
            if (legacy == null) return result;

            foreach (var c in legacy)
            {
                var cm = new CommandModel
                {
                    Id = c.Id.ToString(),
                    Name = c.Name,
                    EntityType = c.EntityType ?? "unknown",
                    State = "OFF",
                    Command = c.Command ?? string.Empty,
                    KeyCode = c.KeyCode ?? string.Empty,
                    Keys = c.Keys ?? new System.Collections.Generic.List<string>(),
                    RunAsLowIntegrity = c.RunAsLowIntegrity
                };

                // map some known types to additional metadata hints
                if (!string.IsNullOrWhiteSpace(c.Type))
                {
                    switch (c.Type)
                    {
                        case TypeConstants.KeyCommand:
                            if (string.IsNullOrWhiteSpace(cm.EntityType) || cm.EntityType == "unknown") cm.EntityType = "key";
                            if (!string.IsNullOrWhiteSpace(c.KeyCode)) cm.KeyCode = c.KeyCode;
                            if (c.Keys != null && c.Keys.Count > 0) cm.Keys = new System.Collections.Generic.List<string>(c.Keys);
                            break;
                        case TypeConstants.MultipleKeysCommand:
                            if (string.IsNullOrWhiteSpace(cm.EntityType) || cm.EntityType == "unknown") cm.EntityType = "multiplekeys";
                            if (c.Keys != null) cm.Keys = new System.Collections.Generic.List<string>(c.Keys);
                            break;
                        case TypeConstants.PowershellCommand:
                            if (string.IsNullOrWhiteSpace(cm.EntityType) || cm.EntityType == "unknown") cm.EntityType = "powershell";
                            if (!string.IsNullOrWhiteSpace(c.Command) || !string.IsNullOrWhiteSpace(c.Args))
                            {
                                cm.Command = ArgumentHelper.CombineCommandAndArgs(c.Command ?? string.Empty, c.Args ?? string.Empty);
                            }
                            break;
                        case TypeConstants.CustomCommand:
                            if (string.IsNullOrWhiteSpace(cm.EntityType) || cm.EntityType == "unknown") cm.EntityType = "custom";
                            if (!string.IsNullOrWhiteSpace(c.Command)) cm.Command = c.Command;
                            break;
                        case TypeConstants.SetVolumeCommand:
                            if (string.IsNullOrWhiteSpace(cm.EntityType) || cm.EntityType == "unknown") cm.EntityType = "setvolume";
                            if (!string.IsNullOrWhiteSpace(c.Command)) cm.Command = c.Command;
                            break;
                        case TypeConstants.WebViewCommand:
                        case TypeConstants.LaunchUrlCommand:
                            if (string.IsNullOrWhiteSpace(cm.EntityType) || cm.EntityType == "unknown") cm.EntityType = "launchurl";
                            if (!string.IsNullOrWhiteSpace(c.Command)) cm.Command = c.Command;
                            break;
                        case TypeConstants.InternalCommand:
                            if (string.IsNullOrWhiteSpace(cm.EntityType) || cm.EntityType == "unknown") cm.EntityType = "internal";
                            if (!string.IsNullOrWhiteSpace(c.Command)) cm.Command = c.Command;
                            break;
                        case TypeConstants.MonitorSleepCommand:
                        case TypeConstants.MonitorWakeCommand:
                            if (string.IsNullOrWhiteSpace(cm.EntityType) || cm.EntityType == "unknown") cm.EntityType = "monitor";
                            break;
                        default:
                            if (string.IsNullOrWhiteSpace(cm.EntityType) || cm.EntityType == "unknown") cm.EntityType = c.Type;
                            break;
                    }
                }

                result.Add(cm);
            }

            return result;
        }
    }
}
