using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using HASS.Agent.Platform.Abstractions;
using Serilog;

namespace HASS.Agent.Platform.Linux.Input
{
    /// <summary>
    /// Linux input simulator using xdotool (X11) or ydotool (Wayland)
    /// </summary>
    public class LinuxInputSimulator : IInputSimulator
    {
        private readonly bool _useYdotool;
        private bool? _xdotoolAvailable;
        private bool? _ydotoolAvailable;
        
        // Map Windows SendKeys syntax to xdotool key names
        private static readonly Dictionary<string, string> SendKeysToXdotool = new(StringComparer.OrdinalIgnoreCase)
        {
            // Special keys
            ["{ENTER}"] = "Return",
            ["{TAB}"] = "Tab",
            ["{ESC}"] = "Escape",
            ["{ESCAPE}"] = "Escape",
            ["{BACKSPACE}"] = "BackSpace",
            ["{BS}"] = "BackSpace",
            ["{DELETE}"] = "Delete",
            ["{DEL}"] = "Delete",
            ["{INSERT}"] = "Insert",
            ["{INS}"] = "Insert",
            ["{HOME}"] = "Home",
            ["{END}"] = "End",
            ["{PGUP}"] = "Page_Up",
            ["{PGDN}"] = "Page_Down",
            ["{UP}"] = "Up",
            ["{DOWN}"] = "Down",
            ["{LEFT}"] = "Left",
            ["{RIGHT}"] = "Right",
            ["{SPACE}"] = "space",
            ["{PRTSC}"] = "Print",
            ["{SCROLLLOCK}"] = "Scroll_Lock",
            ["{PAUSE}"] = "Pause",
            ["{NUMLOCK}"] = "Num_Lock",
            ["{CAPSLOCK}"] = "Caps_Lock",
            ["{BREAK}"] = "Break",
            
            // Function keys
            ["{F1}"] = "F1", ["{F2}"] = "F2", ["{F3}"] = "F3", ["{F4}"] = "F4",
            ["{F5}"] = "F5", ["{F6}"] = "F6", ["{F7}"] = "F7", ["{F8}"] = "F8",
            ["{F9}"] = "F9", ["{F10}"] = "F10", ["{F11}"] = "F11", ["{F12}"] = "F12",
            
            // Modifier mappings
            ["+"] = "shift", // Shift
            ["^"] = "ctrl",  // Control
            ["%"] = "alt",   // Alt
            
            // Additional common keys
            ["ENTER"] = "Return",
            ["RETURN"] = "Return",
            ["TAB"] = "Tab",
            ["ESCAPE"] = "Escape",
            ["SPACE"] = "space",
            ["BACKSPACE"] = "BackSpace",
        };
        
        public LinuxInputSimulator(bool preferYdotool = false)
        {
            _useYdotool = preferYdotool && IsYdotoolAvailable();
        }
        
        private bool IsXdotoolAvailable()
        {
            if (_xdotoolAvailable.HasValue) return _xdotoolAvailable.Value;
            _xdotoolAvailable = CommandExists("xdotool");
            return _xdotoolAvailable.Value;
        }
        
        private bool IsYdotoolAvailable()
        {
            if (_ydotoolAvailable.HasValue) return _ydotoolAvailable.Value;
            _ydotoolAvailable = CommandExists("ydotool");
            return _ydotoolAvailable.Value;
        }
        
        private static bool CommandExists(string command)
        {
            try
            {
                var psi = new ProcessStartInfo("which")
                {
                    Arguments = command,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
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
        
        public bool IsAvailable()
        {
            return IsXdotoolAvailable() || IsYdotoolAvailable();
        }
        
        public string GetRequirements()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Linux Input Simulator Requirements:");
            sb.AppendLine();
            sb.AppendLine("For X11:");
            sb.AppendLine("  sudo apt install xdotool");
            sb.AppendLine();
            sb.AppendLine("For Wayland:");
            sb.AppendLine("  sudo apt install ydotool");
            sb.AppendLine("  sudo systemctl enable --now ydotool");
            sb.AppendLine();
            sb.AppendLine("Current status:");
            sb.AppendLine($"  xdotool: {(IsXdotoolAvailable() ? "✓ Available" : "✗ Not installed")}");
            sb.AppendLine($"  ydotool: {(IsYdotoolAvailable() ? "✓ Available" : "✗ Not installed")}");
            return sb.ToString();
        }
        
        public bool SendKey(string key)
        {
            try
            {
                if (!IsAvailable())
                {
                    Log.Warning("[INPUT] No input simulator available. Install xdotool or ydotool.");
                    return false;
                }
                
                // Normalize key name
                var normalizedKey = NormalizeKeyName(key);
                
                Log.Debug("[INPUT] Sending key: {key} -> {normalized}", key, normalizedKey);
                
                return ExecuteXdotool($"key {normalizedKey}");
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
                
                // Parse combination like "ctrl+alt+t" or "super+d"
                var parts = combination.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var normalizedParts = new List<string>();
                
                foreach (var part in parts)
                {
                    normalizedParts.Add(NormalizeModifierOrKey(part));
                }
                
                var xdotoolCombo = string.Join("+", normalizedParts);
                
                Log.Debug("[INPUT] Sending key combination: {combo} -> {xdotool}", combination, xdotoolCombo);
                
                return ExecuteXdotool($"key {xdotoolCombo}");
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
                
                // Escape special characters for shell
                var escapedText = EscapeForShell(text);
                
                Log.Debug("[INPUT] Typing text: {length} characters", text.Length);
                
                return ExecuteXdotool($"type --delay {delayMs} -- {escapedText}");
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
                
                Log.Debug("[INPUT] Processing key sequence: {seq}", sequence);
                
                // Parse Windows SendKeys-like syntax
                // Examples: "^a" (Ctrl+A), "+{TAB}" (Shift+Tab), "Hello{ENTER}"
                
                int i = 0;
                var success = true;
                
                while (i < sequence.Length && success)
                {
                    char c = sequence[i];
                    
                    // Handle modifiers
                    var modifiers = new List<string>();
                    while (i < sequence.Length && (sequence[i] == '+' || sequence[i] == '^' || sequence[i] == '%'))
                    {
                        modifiers.Add(sequence[i] switch
                        {
                            '+' => "shift",
                            '^' => "ctrl",
                            '%' => "alt",
                            _ => ""
                        });
                        i++;
                    }
                    
                    if (i >= sequence.Length) break;
                    
                    c = sequence[i];
                    
                    // Handle special key in braces {KEY}
                    if (c == '{')
                    {
                        var endBrace = sequence.IndexOf('}', i);
                        if (endBrace > i)
                        {
                            var specialKey = sequence.Substring(i, endBrace - i + 1);
                            var xdotoolKey = MapSendKeysToXdotool(specialKey);
                            
                            if (modifiers.Count > 0)
                            {
                                var combo = string.Join("+", modifiers) + "+" + xdotoolKey;
                                success = ExecuteXdotool($"key {combo}");
                            }
                            else
                            {
                                success = ExecuteXdotool($"key {xdotoolKey}");
                            }
                            
                            i = endBrace + 1;
                            continue;
                        }
                    }
                    
                    // Handle regular character
                    if (modifiers.Count > 0)
                    {
                        var combo = string.Join("+", modifiers) + "+" + c;
                        success = ExecuteXdotool($"key {combo}");
                    }
                    else
                    {
                        // Type single character
                        success = ExecuteXdotool($"type -- \"{c}\"");
                    }
                    
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
            try
            {
                if (!IsAvailable()) return false;
                
                var success = true;
                foreach (var key in keys)
                {
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        // Determine if it's a combination or single key
                        if (key.Contains('+'))
                        {
                            success = SendKeyCombination(key) && success;
                        }
                        else if (key.Contains('{') || key.StartsWith("^") || key.StartsWith("+") || key.StartsWith("%"))
                        {
                            success = SendKeySequence(key) && success;
                        }
                        else
                        {
                            success = SendKey(key) && success;
                        }
                    }
                }
                
                return success;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[INPUT] Error sending multiple keys");
                return false;
            }
        }
        
        public bool MoveMouse(int x, int y)
        {
            try
            {
                if (!IsAvailable()) return false;
                return ExecuteXdotool($"mousemove {x} {y}");
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
                if (!IsAvailable()) return false;
                return ExecuteXdotool($"click {button}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[INPUT] Error clicking mouse");
                return false;
            }
        }
        
        private bool ExecuteXdotool(string arguments)
        {
            try
            {
                var tool = _useYdotool && IsYdotoolAvailable() ? "ydotool" : "xdotool";
                
                var psi = new ProcessStartInfo(tool)
                {
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                // Set DISPLAY if not set (needed for xdotool)
                if (!_useYdotool && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")))
                {
                    psi.Environment["DISPLAY"] = ":0";
                }
                
                using var p = Process.Start(psi);
                if (p == null) return false;
                
                p.WaitForExit(5000);
                
                if (p.ExitCode != 0)
                {
                    var error = p.StandardError.ReadToEnd();
                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        Log.Warning("[INPUT] {tool} error: {error}", tool, error.Trim());
                    }
                }
                
                return p.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[INPUT] Error executing input command");
                return false;
            }
        }
        
        private string NormalizeKeyName(string key)
        {
            // Check if it's a SendKeys special key
            var mapped = MapSendKeysToXdotool(key);
            if (mapped != key) return mapped;
            
            // Common normalizations
            return key.ToLowerInvariant() switch
            {
                "enter" or "return" => "Return",
                "esc" or "escape" => "Escape",
                "space" => "space",
                "tab" => "Tab",
                "backspace" or "back" => "BackSpace",
                "delete" or "del" => "Delete",
                "insert" or "ins" => "Insert",
                "home" => "Home",
                "end" => "End",
                "pageup" or "pgup" => "Page_Up",
                "pagedown" or "pgdn" => "Page_Down",
                "up" or "uparrow" => "Up",
                "down" or "downarrow" => "Down",
                "left" or "leftarrow" => "Left",
                "right" or "rightarrow" => "Right",
                "print" or "printscreen" or "prtsc" => "Print",
                "super" or "win" or "meta" => "super",
                _ => key
            };
        }
        
        private string NormalizeModifierOrKey(string part)
        {
            return part.ToLowerInvariant() switch
            {
                "ctrl" or "control" => "ctrl",
                "alt" => "alt",
                "shift" => "shift",
                "super" or "win" or "meta" or "windows" => "super",
                _ => NormalizeKeyName(part)
            };
        }
        
        private string MapSendKeysToXdotool(string sendKey)
        {
            if (SendKeysToXdotool.TryGetValue(sendKey, out var mapped))
            {
                return mapped;
            }
            
            // Try without braces
            var keyOnly = sendKey.Trim('{', '}').ToUpperInvariant();
            if (SendKeysToXdotool.TryGetValue($"{{{keyOnly}}}", out mapped))
            {
                return mapped;
            }
            
            return sendKey;
        }
        
        private static string EscapeForShell(string text)
        {
            // Use single quotes and escape any single quotes in the text
            return "'" + text.Replace("'", "'\\''") + "'";
        }
    }
}
