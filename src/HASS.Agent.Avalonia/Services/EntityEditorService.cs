using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using HASS.Agent.Core;

namespace HASS.Agent.Avalonia.Services;

public static class EntityEditorService
{
    public static Task<CommandModel?> ShowCommandEditorAsync(Window? owner, CommandModel initial)
    {
        if (owner is null)
        {
            return Task.FromResult<CommandModel?>(null);
        }

        var dialog = CreateWindow($"{(string.IsNullOrWhiteSpace(initial.Id) ? "Add" : "Edit")} Command", 700, 760);
        var body = new StackPanel { Margin = new Thickness(16), Spacing = 12 };
        var errorText = new TextBlock { Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap };
        body.Children.Add(errorText);

        var idBox = AddTextField(body, "ID", initial.Id, "Unique command identifier");
        var nameBox = AddTextField(body, "Name", initial.Name, "Friendly name");
        var entityTypeBox = AddTextField(body, "Entity Type", initial.EntityType, "custom, key, shell, url, volume...");
        var stateBox = AddTextField(body, "State", initial.State);
        var commandBox = AddTextField(body, "Command", initial.Command, "Command payload or shell text");
        var argsBox = AddTextField(body, "Args", initial.Args, "Optional arguments");
        var keyCodeBox = AddTextField(body, "Key Code", initial.KeyCode, "Optional keyboard key code");
        var keysBox = AddTextField(body, "Keys", initial.Keys.Count > 0 ? string.Join(", ", initial.Keys) : string.Empty, "Comma-separated key list");
        var lowIntegrityBox = AddCheckField(body, "Run as low integrity", initial.RunAsLowIntegrity);

        var buttonRow = CreateButtonRow();
        var saveButton = new Button { Content = "Save", IsDefault = true };
        var cancelButton = new Button { Content = "Cancel", IsCancel = true };

        saveButton.Click += (_, _) =>
        {
            var id = idBox.Text?.Trim() ?? string.Empty;
            var name = nameBox.Text?.Trim() ?? string.Empty;
            var entityType = entityTypeBox.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(id))
            {
                errorText.Text = "ID is required.";
                return;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                errorText.Text = "Name is required.";
                return;
            }

            if (string.IsNullOrWhiteSpace(entityType))
            {
                errorText.Text = "Entity type is required.";
                return;
            }

            errorText.Text = string.Empty;
            dialog.Close(new CommandModel
            {
                Id = id,
                Name = name,
                EntityType = entityType,
                State = stateBox.Text?.Trim() ?? string.Empty,
                Command = commandBox.Text?.Trim() ?? string.Empty,
                Args = argsBox.Text?.Trim() ?? string.Empty,
                KeyCode = keyCodeBox.Text?.Trim() ?? string.Empty,
                Keys = (keysBox.Text ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList(),
                RunAsLowIntegrity = lowIntegrityBox.IsChecked ?? false
            });
        };

        cancelButton.Click += (_, _) => dialog.Close(null);

        buttonRow.Children.Add(saveButton);
        buttonRow.Children.Add(cancelButton);
        body.Children.Add(buttonRow);

        dialog.Content = new ScrollViewer { Content = body };
        return dialog.ShowDialog<CommandModel?>(owner);
    }

    public static Task<SensorModel?> ShowSensorEditorAsync(Window? owner, SensorModel initial)
    {
        if (owner is null)
        {
            return Task.FromResult<SensorModel?>(null);
        }

        var dialog = CreateWindow($"{(string.IsNullOrWhiteSpace(initial.Id) ? "Add" : "Edit")} Sensor", 680, 760);
        var body = new StackPanel { Margin = new Thickness(16), Spacing = 12 };
        var errorText = new TextBlock { Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap };
        body.Children.Add(errorText);

        var idBox = AddTextField(body, "ID", initial.Id, "Unique sensor identifier");
        var nameBox = AddTextField(body, "Name", initial.Name, "Friendly name");
        var typeBox = AddTextField(body, "Type", initial.Type, "file, platform, custom...");
        var stateBox = AddTextField(body, "State", initial.State);
        var updateIntervalBox = AddNumericField(body, "Update Interval", initial.UpdateInterval, 0, 86400);
        var queryBox = AddTextField(body, "Query", initial.Query, "Sensor query or command");
        var scopeBox = AddTextField(body, "Scope", initial.Scope);
        var categoryBox = AddTextField(body, "Category", initial.Category);
        var counterBox = AddTextField(body, "Counter", initial.Counter);
        var instanceBox = AddTextField(body, "Instance", initial.Instance);
        var windowNameBox = AddTextField(body, "Window Name", initial.WindowName);

        var buttonRow = CreateButtonRow();
        var saveButton = new Button { Content = "Save", IsDefault = true };
        var cancelButton = new Button { Content = "Cancel", IsCancel = true };

        saveButton.Click += (_, _) =>
        {
            var id = idBox.Text?.Trim() ?? string.Empty;
            var name = nameBox.Text?.Trim() ?? string.Empty;
            var type = typeBox.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(id))
            {
                errorText.Text = "ID is required.";
                return;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                errorText.Text = "Name is required.";
                return;
            }

            if (string.IsNullOrWhiteSpace(type))
            {
                errorText.Text = "Type is required.";
                return;
            }

            errorText.Text = string.Empty;
            dialog.Close(new SensorModel
            {
                Id = id,
                Name = name,
                State = stateBox.Text?.Trim() ?? string.Empty,
                Type = type,
                UpdateInterval = (int)(updateIntervalBox.Value ?? 0),
                Query = queryBox.Text?.Trim() ?? string.Empty,
                Scope = scopeBox.Text?.Trim() ?? string.Empty,
                Category = categoryBox.Text?.Trim() ?? string.Empty,
                Counter = counterBox.Text?.Trim() ?? string.Empty,
                Instance = instanceBox.Text?.Trim() ?? string.Empty,
                WindowName = windowNameBox.Text?.Trim() ?? string.Empty
            });
        };

        cancelButton.Click += (_, _) => dialog.Close(null);

        buttonRow.Children.Add(saveButton);
        buttonRow.Children.Add(cancelButton);
        body.Children.Add(buttonRow);

        dialog.Content = new ScrollViewer { Content = body };
        return dialog.ShowDialog<SensorModel?>(owner);
    }

    private static Window CreateWindow(string title, double width, double height)
    {
        return new Window
        {
            Title = title,
            Width = width,
            Height = height,
            MinWidth = 520,
            MinHeight = 420,
            CanResize = true,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
    }

    private static StackPanel CreateButtonRow()
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
    }

    private static TextBox AddTextField(StackPanel panel, string label, string value, string watermark = "")
    {
        var box = new TextBox
        {
            Text = value ?? string.Empty,
            Watermark = string.IsNullOrWhiteSpace(watermark) ? null : watermark
        };

        panel.Children.Add(new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = label, FontWeight = FontWeight.SemiBold },
                box
            }
        });

        return box;
    }

    private static NumericUpDown AddNumericField(StackPanel panel, string label, int value, int minimum, int maximum)
    {
        var box = new NumericUpDown
        {
            Value = value,
            Minimum = minimum,
            Maximum = maximum
        };

        panel.Children.Add(new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = label, FontWeight = FontWeight.SemiBold },
                box
            }
        });

        return box;
    }

    private static CheckBox AddCheckField(StackPanel panel, string label, bool value)
    {
        var box = new CheckBox
        {
            Content = label,
            IsChecked = value
        };

        panel.Children.Add(box);
        return box;
    }
}
