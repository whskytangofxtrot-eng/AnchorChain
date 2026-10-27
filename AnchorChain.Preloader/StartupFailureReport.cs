using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BepInEx.Logging;

namespace AnchorChain.Preloader
{
    // Listen only while AnchorChain initializes plugins. Never expose exception
    // text or local file paths in the player-facing notice.
    public sealed class StartupFailureReport : ILogListener
    {
        private readonly Dictionary<string, string> failures = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Regex Failed = new Regex(@"^Error loading plugin ([^\r\n]+?) \(([^\r\n()]+)\):", RegexOptions.CultureInvariant);
        private static readonly Regex Loaded = new Regex(@"^Loaded plugin ([^\r\n]+?) \(([^\r\n()]+)\)$", RegexOptions.CultureInvariant);

        public void LogEvent(object sender, LogEventArgs eventArgs)
        {
            if (eventArgs.Source == null || eventArgs.Source.SourceName != "AnchorChain") return;
            RecordMessage(eventArgs.Data as string);
        }

        public void RecordMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            var failed = Failed.Match(message);
            lock (failures)
            {
                if (failed.Success)
                {
                    string name = failed.Groups[1].Value.Trim();
                    if (name.Length > 120) name = name.Substring(0, 117) + "...";
                    failures[failed.Groups[2].Value] = name;
                    return;
                }
                var loaded = Loaded.Match(message);
                if (loaded.Success) failures.Remove(loaded.Groups[2].Value);
            }
        }

        public string[] GetFailedModNames()
        {
            lock (failures) return failures.Values.Distinct().OrderBy(name => name, StringComparer.Ordinal).ToArray();
        }

        public static string BuildNotice(string[] names)
        {
            string subject = names.Length == 0 ? "An enabled code mod failed to start." :
                "These code mods failed to start:\n" + string.Join("\n", names.Take(8).Select(name => "- " + name)) +
                (names.Length > 8 ? "\n- Additional failures are listed in the log." : "");
            return subject + "\n\nAnchorChain stopped retrying so you could reach the menu. The affected mods may have partly started and are not working correctly in this session.\n\nOpen Mods, uncheck the affected mods, then restart Sea Power before loading a campaign or battle.\n\nDetails: BepInEx/LogOutput.log";
        }

        public void Dispose() { }
    }
}
