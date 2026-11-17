using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NuclearOption.DedicatedServer;
using NuclearOption.SavedMission;

namespace Nuclei;

/// <summary>
///     Index of the multiplayer missions the server can play, grouped by base mission so players can
///     pick "Escalation", then normal or co-op, then a team. Sources:
///     <list type="bullet">
///         <item>BuiltIn: the game's own missions ("Escalation", "Escalation Co-op as BDF", "11. Expedition").</item>
///         <item>Custom: folders in the server's MissionDirectory (MissionGroup.User), <c>Name/Name.json</c>.</item>
///         <item>Workshop: Workshop missions from the rotation, plus any the server already knows about.</item>
///     </list>
///     Built once the dedicated server is running; rebuild with <see cref="Build" /> after adding files.
/// </summary>
internal static class MissionCatalog
{
    internal const string Normal = "normal";
    internal const string Coop = "coop";
    internal const string WorkshopWord = "workshop";

    private static readonly Regex CoopSuffix = new(@"^(?<base>.+?)\s+Co-op\s+as\s+(?<team>\S+)$", RegexOptions.IgnoreCase);
    private static readonly Regex NumberPrefix = new(@"^\d+\.\s*");

    internal enum Source
    {
        BuiltIn,
        Custom,
        Workshop
    }

    /// <summary>A playable mission: the key the server loads plus a readable name.</summary>
    internal readonly struct Pick(MissionKeySaveable key, string display)
    {
        internal readonly MissionKeySaveable Key = key;
        internal readonly string Display = display;
    }

    internal sealed class Entry
    {
        internal string Token = "";
        internal Source Source;
        internal Pick? Normal;
        internal readonly SortedDictionary<string, Pick> CoopByTeam = new(StringComparer.OrdinalIgnoreCase);
    }

    private static readonly Dictionary<string, Entry> Entries = new(StringComparer.OrdinalIgnoreCase);

    internal static bool IsBuilt { get; private set; }
    internal static IEnumerable<Entry> All => Entries.Values.OrderBy(e => e.Source).ThenBy(e => e.Token);
    internal static string[] Teams => Entries.Values.SelectMany(e => e.CoopByTeam.Keys).Select(t => t.ToLowerInvariant()).Distinct().OrderBy(t => t).ToArray();

    internal static void Build()
    {
        Entries.Clear();

        // The server points the User group at DedicatedServerConfig.MissionDirectory a moment after it
        // starts running; do it here too so the first scan doesn't read the default folder.
        var missionDirectory = DedicatedServerManager.Instance?.Config?.MissionDirectory;
        if (!string.IsNullOrWhiteSpace(missionDirectory) && missionDirectory != MissionGroup.UserGroup.UserMissionDirectory)
            MissionGroup.UserGroup.SetDirectory(missionDirectory);

        AddGroup(MissionGroup.BuiltIn, Source.BuiltIn);
        AddGroup(MissionGroup.User, Source.Custom);
        AddGroup(MissionGroup.Workshop, Source.Workshop);
        AddRotationWorkshop();

        IsBuilt = true;
        var counts = string.Join(", ", Enum.GetValues(typeof(Source)).Cast<Source>()
            .Select(s => $"{Entries.Values.Count(e => e.Source == s)} {s.ToString().ToLowerInvariant()}"));
        Plugin.Log.LogInfo($"Mission catalog: {Entries.Count} multiplayer missions ({counts}). Custom mission folder: {MissionGroup.UserGroup.UserMissionDirectory}");
    }

    private static void AddGroup(MissionGroup group, Source source)
    {
        IEnumerable<MissionKey> keys;
        try
        {
            keys = group.GetMissions().ToList();
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Couldn't list {source} missions: {e.Message}");
            return;
        }

        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key.Name))
                continue;
            try
            {
                if (key.TryQuickLoad(out MissionQuickLoad quick) && quick.missionSettings?.PlayerModeEnum == PlayerMode.Singleplayer)
                    continue;
                Add(key.Name, MissionKeySaveable.FromKey(key), source);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Skipping {source} mission '{key.Name}': {e.Message}");
            }
        }
    }

    /// <summary>Workshop missions listed in DedicatedServerConfig.json, named after the downloaded mission if possible.</summary>
    private static void AddRotationWorkshop()
    {
        var rotation = DedicatedServerManager.Instance?.Config?.MissionRotation ?? [];
        foreach (var option in rotation.Where(o => string.Equals(o.Key.Group, "Workshop", StringComparison.OrdinalIgnoreCase)))
        {
            if (Entries.Values.Any(e => e.Normal is { } n && n.Key.Equals(option.Key)))
                continue;
            var name = $"{WorkshopWord} {option.Key.Name}";
            try
            {
                if (option.Key.TryGetKey(out MissionKey key) && !string.IsNullOrWhiteSpace(key.Name))
                    name = key.Name;
            }
            catch
            {
                // Not downloaded yet; keep the ID-based name.
            }

            Add(name, option.Key, Source.Workshop);
        }
    }

    private static void Add(string name, MissionKeySaveable key, Source source)
    {
        var coop = CoopSuffix.Match(name);
        var baseName = NumberPrefix.Replace(coop.Success ? coop.Groups["base"].Value : name, "");
        var token = ServerActions.ToChoice(baseName);

        // A custom mission called "Escalation" must not replace the built-in one.
        if (Entries.TryGetValue(token, out var existing) && existing.Source != source)
            token = $"{source.ToString().ToLowerInvariant()}_{token}";
        if (!Entries.TryGetValue(token, out var entry))
            Entries[token] = entry = new Entry { Token = token, Source = source };

        var pick = new Pick(key, name);
        if (coop.Success)
            entry.CoopByTeam[coop.Groups["team"].Value] = pick;
        else
            entry.Normal = pick;
    }

    internal static string Describe(Entry e)
    {
        var parts = new List<string>();
        if (e.Normal != null)
            parts.Add(Normal);
        if (e.CoopByTeam.Count > 0)
            parts.Add($"{Coop}:{string.Join("/", e.CoopByTeam.Keys.Select(t => t.ToLowerInvariant()))}");
        return $"{e.Token} [{string.Join(", ", parts)}]";
    }

    /// <summary>
    ///     Turns "mission [normal|coop] [team]" into a mission. Also accepts a full mission name such as
    ///     "Escalation Co-op as BDF", and "workshop &lt;id&gt;" for any Workshop mission.
    /// </summary>
    internal static bool TryResolve(IReadOnlyList<string> args, out Pick pick, out string error)
    {
        pick = default;
        error = "";
        if (args.Count == 0)
        {
            error = "Name a mission.";
            return false;
        }

        if (args[0].Equals(WorkshopWord, StringComparison.OrdinalIgnoreCase))
        {
            if (args.Count < 2 || !ulong.TryParse(args[1], out var workshopId))
            {
                error = "Usage: workshop <Workshop item ID>";
                return false;
            }

            pick = new Pick(new MissionKeySaveable { Group = "Workshop", Name = workshopId.ToString() }, $"Workshop mission {workshopId}");
            return true;
        }

        if (!Entries.TryGetValue(args[0], out var entry))
        {
            // Full name typed out, e.g. from the console.
            var full = ServerActions.ToChoice(string.Join(" ", args));
            var exact = Entries.Values
                .SelectMany(e => e.CoopByTeam.Values.Select(p => (Pick?)p).Append(e.Normal))
                .FirstOrDefault(p => p is { } x && string.Equals(ServerActions.ToChoice(x.Display), full, StringComparison.OrdinalIgnoreCase));
            if (exact is { } found)
            {
                pick = found;
                return true;
            }

            error = $"Unknown mission '{args[0]}'. Type /missions to list them.";
            return false;
        }

        string? mode = null, team = null;
        foreach (var arg in args.Skip(1))
        {
            if (arg.Equals(Normal, StringComparison.OrdinalIgnoreCase) || arg.Equals(Coop, StringComparison.OrdinalIgnoreCase))
                mode = arg.ToLowerInvariant();
            else
                team = arg;
        }

        mode ??= team != null || entry.Normal == null ? Coop : Normal;

        if (mode == Normal)
        {
            if (entry.Normal is not { } normal)
            {
                error = $"{entry.Token} only exists as co-op. Use {CoopOptions(entry)}.";
                return false;
            }

            pick = normal;
            return true;
        }

        if (entry.CoopByTeam.Count == 0)
        {
            error = $"{entry.Token} has no co-op version.";
            return false;
        }

        if (team == null)
        {
            if (entry.CoopByTeam.Count == 1)
            {
                pick = entry.CoopByTeam.Values.First();
                return true;
            }

            error = $"Pick a team for {entry.Token} co-op: {CoopOptions(entry)}.";
            return false;
        }

        if (!entry.CoopByTeam.TryGetValue(team, out pick))
        {
            error = $"{entry.Token} co-op has no team '{team}'. Use {CoopOptions(entry)}.";
            return false;
        }

        return true;
    }

    private static string CoopOptions(Entry entry)
    {
        return string.Join(" or ", entry.CoopByTeam.Keys.Select(t => $"{Coop}_{t.ToLowerInvariant()}"));
    }

    /// <summary>Mission list grouped by source; <paramref name="filter" /> is builtin, custom or workshop.</summary>
    internal static string ListText(string? filter = null)
    {
        var sources = Enum.GetValues(typeof(Source)).Cast<Source>()
            .Where(s => string.IsNullOrEmpty(filter) || s.ToString().Equals(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (sources.Count == 0)
            return "Filter must be builtin, custom or workshop.";

        var lines = new List<string>();
        foreach (var source in sources)
        {
            var entries = All.Where(e => e.Source == source).ToList();
            var label = source switch
            {
                Source.BuiltIn => "Built-in",
                Source.Custom => "Custom",
                _ => "Workshop"
            };
            lines.Add(entries.Count == 0
                ? $"{label}: none"
                : $"{label}: {string.Join("; ", entries.Select(Describe))}");
        }

        return string.Join("\n", lines);
    }
}
