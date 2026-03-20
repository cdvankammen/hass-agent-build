using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using HASS.Agent.Platform.Abstractions;
using Serilog;

namespace HASS.Agent.Platform.macOS.Input
{
    /// <summary>
    /// macOS input simulator using AppleScript and osascript
    /// For more advanced usage, CGEvent via native interop would be needed
    /// </summary>
    public class MacOSInputSimulator : IInputSimulator
    {
        // Map common key names to AppleScript key codes
        // Reference: https://eastmanreference.com/complete-list-of-applescript-key-codes
        private static readonly Dictionary<string, int> KeyCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            // Letters - lowercase
            ["a"] = 0, ["b"] = 11, ["c"] = 8, ["d"] = 2, ["e"] = 14, ["f"] = 3, ["g"] = 5,
            ["h"] = 4, ["i"] = 34, ["j"] = 38, ["k"] = 40, ["l"] = 37, ["m"] = 46, ["n"] = 45,
            ["o"] = 31, ["p"] = 35, ["q"] = 12, ["r"] = 15, ["s"] = 1, ["t"] = 17, ["u"] = 32,
            ["v"] = 9, ["w"] = 13, ["x"] = 7, ["y"] = 16, ["z"] = 6,
            
            // Numbers
            ["0"] = 29, ["1"] = 18, ["2"] = 19, ["3"] = 20, ["4"] = 21,
            ["5"] = 23, ["6"] = 22, ["7"] = 26, ["8"] = 28, ["9"] = 25,
            
            // Special keys
            ["return"] = 36, ["enter"] = 36,
            ["tab"] = 48,
            ["space"] = 49,
            ["delete"] = 51, ["backspace"] = 51,
            ["escape"] = 53, ["esc"] = 53,
            ["command"] = 55, ["cmd"] = 55,
            ["shift"] = 56,
            ["capslock"] = 57,
            ["option"] = 58, ["alt"] = 58,
            ["control"] = 59, ["ctrl"] = 59,
            ["rightshift"] = 60,
            ["rightoption"] = 61,
            ["rightcontrol"] = 62,
            ["function"] = 63, ["fn"] = 63,
            
            // Function keys
            ["f1"] = 122, ["f2"] = 123, ["f3"] = 99, ["f4"] = 118, ["f5"] = 96, ["f6"] = 97,
            ["f7"] = 98, ["f8"] = 100, ["f9"] = 101, ["f10"] = 109, ["f11"] = 103, ["f12"] = 111,
            
            // Arrow keys
            ["up"] = 126, ["down"] = 125, ["left"] = 123, ["right"] = 124,
            
            // Navigation
            ["home"] = 115, ["end"] = 119, ["pageup"] = 116, ["pagedown"] = 121,
            ["forwarddelete"] = 117
        };
        
        public bool IsAvailable()
        {
            // osascript is always available on macOS
            return OperatingSystem.IsMacOS();
        }
        
        public string GetRequirements()
        {
            var sb = new StringBuilder();
            sb.AppendLine("macOS Input Simulator Requirements:");
            sb.AppendLine();
            sb.AppendLine("This simulator uses AppleScript via osascript.");
            sb.AppendLine("For keyboard simulation, you need to grant accessibility permissions:");
            sb.AppendLine();
            sb.AppendLine("1. Open System Preferences → Security & Privacy → Privacy");
            sb.AppendLine("2. Select 'Accessibility' from the left panel");
            sb.AppendLine("3. Add your terminal app (Terminal.app or iTerm) or the HASS.Agent app");
            sb.AppendLine("4. Enable the checkbox next to the app");
            sb.AppendLine();
            sb.AppendLine("Note: You may need to restart the app after granting permissions.");
            return sb.ToString();
        }
        
        public bool SendKey(string key)
        {
            try
            {
                if (!IsAvailable()) return false;
                
                var normalizedKey = key.ToLowerInvariant().Trim();
                
                if (KeyCodes.TryGetValue(normalizedKey, out var keyCode))
                {
                    return ExecuteKeyCode(keyCode);
                }
                
                // Try typing as character if single char
                if (normalizedKey.Length == 1)
                {
                    return SendText(normalizedKey);
                }
                
                Log.Warning("[INPUT] Unknown key: {key}", key);
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[INPUT] Error sending key {key}", key);
                return false;
            }
        }
        
        public bool SendKeyCombination(string combination)
        {
            try
            {
                if (!IsAvailable()) return false;
                
                // Parse combination like "cmd+c" or "ctrl+alt+delete"
                var parts = combination.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                
                if (parts.Length == 0) return false;
                
                var modifiers = new List<string>();
                string? mainKey = null;
                
                foreach (var part in parts)
                {
                    var normalized = part.ToLowerInvariant();
                    if (normalized is "cmd" or "command")
                    {
                        modifiers.Add("command down");
                    }
                    else if (normalized is "ctrl" or "control")
                    {
                        modifiers.Add("control down");
                    }
                    else if (normalized is "alt" or "option")
                    {
                        modifiers.Add("option down");
                    }
                    else if (normalized is "shift")
                    {
                        modifiers.Add("shift down");
                    }
                    else
                    {
                        mainKey = normalized;
                    }
                }
                
                if (mainKey == null) return false;
                
                var modifierStr = modifiers.Count > 0 ? string.Join(", ", modifiers) : "";
                
                if (KeyCodes.TryGetValue(mainKey, out var keyCode))
                {
                    return ExecuteKeyCodeWithModifiers(keyCode, modifierStr);
                }
                
                // Single character
                if (mainKey.Length == 1)
                {
                    return ExecuteKeystrokeWithModifiers(mainKey, modifierStr);
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[INPUT] Error sending key combination {combo}", combination);
                return false;
            }
        }
        
        public bool SendText(string text, int delayMs = 10)
        {
            try
            {
                if (!IsAvailable()) return false;
                if (string.IsNullOrEmpty(text)) return true;
                
                // Use AppleScript keystroke command
                var escapedText = EscapeForAppleScript(text);
                var script = $"tell application \"System Events\" to keystroke \"{escapedText}\"";
                
                return ExecuteAppleScript(script);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[INPUT] Error sending text");
                return false;
            }
        }
        
        public bool SendKeySequence(string sequence)
        {
            try
            {
                if (!IsAvailable()) return false;
                if (string.IsNullOrWhiteSpace(sequence)) return true;
                
                // Parse Windows SendKeys-like syntax
                int i = 0;
                var success = true;
                
                while (i < sequence.Length && success)
                {
                    // Handle modifiers
                    var useCommand = false;
                    var useControl = false;
                    var useOption = false;
                    var useShift = false;
                    
                    while (i < sequence.Length)
                    {
                        if (sequence[i] == '^') { useControl = true; i++; }
                        else if (sequence[i] == '%') { useOption = true; i++; }
                        else if (sequence[i] == '+') { useShift = true; i++; }
                        else break;
                    }
                    
                    if (i >= sequence.Length) break;
                    
                    char c = sequence[i];
                    
                    // Handle special key in braces {KEY}
                    if (c == '{')
                    {
                        var endBrace = sequence.IndexOf('}', i);
                        if (endBrace > i)
                        {
                            var specialKey = sequence.Substring(i + 1, endBrace - i - 1).ToLowerInvariant();
                            var modStr = BuildModifierString(useCommand, useControl, useOption, useShift);
                            
                            if (KeyCodes.TryGetValue(specialKey, out var keyCode))
                            {
                                success = ExecuteKeyCodeWithModifiers(keyCode, modStr);
                            }
                            else
                            {
                                Log.Warning("[INPUT] Unknown special key: {key}", specialKey);
                            }
                            
                            i = endBrace + 1;
                            continue;
                        }
                    }
                    
                    // Regular character
                    var charModStr = BuildModifierString(useCommand, useControl, useOption, useShift);
                    success = ExecuteKeystrokeWithModifiers(c.ToString(), charModStr);
                    i++;
                }
                
                return success;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[INPUT] Error processing key sequence {seq}", sequence);
                return false;
            }
        }
        
        public bool SendMultipleKeys(IEnumerable<string> keys)
        {
            var success = true;
            foreach (var key in keys)
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    if (key.Contains('+'))
                    {
                        success = SendKeyCombination(key) && success;
                    }
                    else
                    {
                        success = SendKey(key) && success;
                    }
                }
            }
            return success;
        }
        
        public bool MoveMouse(int x, int y)
        {
            try
            {
                // Use cliclick or AppleScript with Objective-C bridge
                // For now, try using AppleScript with do shell script and cliclick
                if (CommandExists("cliclick"))
                {
                    return ExecuteCommand("cliclick", $"m:{x},{y}");
                }
                
                Log.Warning("[INPUT] cliclick not installed. Install with: brew install cliclick");
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[INPUT] Error moving mouse");
                return false;
            }
        }
        
        public bool ClickMouse(int button = 1)
        {
            try
            {
                if (CommandExists("cliclick"))
                {
                    var clickType = button switch
                    {
                        1 => "c:.",  // left click at current position
                        2 => "m:.",  // middle click
                        3 => "rc:.", // right click
                        _ => "c:."
                    };
                    return ExecuteCommand("cliclick", clickType);
                }
                
                Log.Warning("[INPUT] cliclick not installed. Install with: brew install cliclick");
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[INPUT] Error clicking mouse");
                return false;
            }
        }
        
        private bool ExecuteKeyCode(int keyCode)
        {
            var script = $"tell application \"System Events\" to key code {keyCode}";
            return ExecuteAppleScript(script);
        }
        
        private bool ExecuteKeyCodeWithModifiers(int keyCode, string modifiers)
        {
            string script;
            if (string.IsNullOrEmpty(modifiers))
            {
                script = $"tell application \"System Events\" to key code {keyCode}";
            }
            else
            {
                script = $"tell application \"System Events\" to key code {keyCode} using {{{modifiers}}}";
            }
            return ExecuteAppleScript(script);
        }
        
        private bool ExecuteKeystrokeWithModifiers(string keystroke, string modifiers)
        {
            var escaped = EscapeForAppleScript(keystroke);
            string script;
            if (string.IsNullOrEmpty(modifiers))
            {
                script = $"tell application \"System Events\" to keystroke \"{escaped}\"";
            }
            else
            {
                script = $"tell application \"System Events\" to keystroke \"{escaped}\" using {{{modifiers}}}";
            }
            return ExecuteAppleScript(script);
        }
        
        private string BuildModifierString(bool command, bool control, bool option, bool shift)
        {
            var mods = new List<string>();
            if (command) mods.Add("command down");
            if (control) mods.Add("control down");
            if (option) mods.Add("option down");
            if (shift) mods.Add("shift down");
            return string.Join(", ", mods);
        }
        
        private bool ExecuteAppleScript(string script)
        {
            try
            {
                var psi = new ProcessStartInfo("osascript")
                {
                    Arguments = $"-e '{script}'",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                using var p = Process.Start(psi);
                if (p == null) return false;
                
                p.WaitForExit(5000);
                
                if (p.ExitCode != 0)
                {
                    var error = p.StandardError.ReadToEnd();
                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        Log.Warning("[INPUT] AppleScript error: {error}", error.Trim());
                    }
                }
                
                return p.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[INPUT] Error executing AppleScript");
                return false;
            }
        }
        
        private bool ExecuteCommand(string command, string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo(command)
                {
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                using var p = Process.Start(psi);
                if (p == null) return false;
                
                p.WaitForExit(5000);
                return p.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[INPUT] Error executing command");
                return false;
            }
        }
        
        private static bool CommandExists(string command)
        {
            try
            {
                var psi = new ProcessStartInfo("which")
                {
                    Arguments = command,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                if (p == null) return false;
                p.WaitForExit(2000);
                return p.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
        
        private static string EscapeForAppleScript(string text)
        {
            return text
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
    }
}
