using System;
using System.Text;

namespace HASS.Agent.Core
{
    public static class ArgumentHelper
    {
        // Simple helper to quote args that contain spaces or special characters
        public static string CombineCommandAndArgs(string command, string args)
        {
            if (string.IsNullOrWhiteSpace(command)) return args ?? string.Empty;
            if (string.IsNullOrWhiteSpace(args)) return command;

            // If args contains whitespace, quote the full args string to preserve inner quoting
            if (args.IndexOfAny(new[] { ' ', '\t', '\n' }) >= 0)
            {
                return command + " " + QuoteFull(args);
            }

            // Otherwise split tokens and quote individually
            var tokens = SplitArgs(args);
            for (int i = 0; i < tokens.Length; i++) tokens[i] = QuoteToken(tokens[i]);
            return command + " " + string.Join(" ", tokens);
        }

        private static string[] SplitArgs(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return Array.Empty<string>();
            var list = new System.Collections.Generic.List<string>();
            var sb = new StringBuilder();
            bool inDouble = false, inSingle = false;
            foreach (var ch in s)
            {
                if (ch == '"' && !inSingle) { inDouble = !inDouble; sb.Append(ch); continue; }
                if (ch == '\'' && !inDouble) { inSingle = !inSingle; sb.Append(ch); continue; }
                if (char.IsWhiteSpace(ch) && !inDouble && !inSingle)
                {
                    if (sb.Length > 0) { list.Add(sb.ToString()); sb.Clear(); }
                }
                else sb.Append(ch);
            }
            if (sb.Length > 0) list.Add(sb.ToString());
            return list.ToArray();
        }

        private static string QuoteToken(string t)
        {
            if (string.IsNullOrEmpty(t)) return t;
            // If already quoted, return as-is
            if ((t.StartsWith("\"") && t.EndsWith("\"")) || (t.StartsWith("'") && t.EndsWith("'"))) return t;
            if (t.IndexOfAny(new[] { ' ', '\"', '\'' }) >= 0)
            {
                return "\"" + t.Replace("\"", "\\\"") + "\"";
            }
            return t;
        }

        private static string QuoteFull(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return "\"" + s.Replace("\"", "\\\"") + "\"";
        }
    }
}
