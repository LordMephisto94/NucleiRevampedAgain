using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NuclearOption.SavedMission;

namespace Nuclei;

/// <summary>
///     Index of the game's built-in multiplayer missions, grouped by base mission so players can pick
///     "Escalation", then normal or co-op, then a team. Built once the server has loaded its missions.
///     Example names: "Escalation", "Escalation Co-op as BDF", "11. Expedition".
/// </summary>
internal static class MissionCatalog
{
    internal const string Normal = "normal";
    internal const string Coop = "coop";

    private static readonly Regex CoopSuffix = new(@"^(?<base>.+?)\s+Co-op\s+as\s+(?<team>\S+)$", RegexOptions.IgnoreCase);
    private static readonly Regex NumberPrefix = new(@"^\d+\.\s*");

    internal sealed class Entry
    {
        internal string Token = "";
        internal string? NormalName;
        internal readonly SortedDictionary<string, string> CoopByTeam = new(StringComparer.OrdinalIgnoreCase);
    }

    private static readonly Dictionary<string, Entry> Entries = new(StringComparer.OrdinalIgnoreCase);

    internal static bool IsBuilt { get; private set; }
    internal static IEnumerable<Entry> All => Entries.Values.OrderBy(e => e.Token);
    internal static string[] Teams => Entries.Values.SelectMany(e => e.CoopByTeam.Keys).Select(t => t.ToLowerInvariant()).Distinct().OrderBy(t => t).ToArray();

    internal static void Build()
    {
        Entries.Clear();
        foreach (var key in MissionGroup.BuiltIn.GetMissions())
        {
            var name = key.Name;
            if (string.IsNullOrWhiteSpace(name))
                continue;
            if (key.TryQuickLoad(out MissionQuickLoad quick) && quick.missionSettings?.PlayerModeEnum == PlayerMode.Singleplayer)
                continue;

            var coop = CoopSuffix.Match(name);
            var baseName = NumberPrefix.Replace(coop.Success ? coop.Groups["base"].Value : name, "");
            var token = ServerActions.ToChoice(baseName);
            if (!Entries.TryGetValue(token, out var entry))
                Entries[token] = entry = new Entry { Token = token };

            if (coop.Success)
                entry.CoopByTeam[coop.Groups["team"].Value] = name;
            else
                entry.NormalName = name;
        }

        IsBuilt = true;
        Plugin.Log.LogInfo($"Mission catalog: {Entries.Count} multiplayer missions ({string.Join(", ", All.Select(Describe))}).");
    }

    internal static string Describe(Entry e)
    {
        var parts = new List<string>();
        if (e.NormalName != null)
            parts.Add(Normal);
        if (e.CoopByTeam.Count > 0)
            parts.Add($"{Coop}:{string.Join("/", e.CoopByTeam.Keys.Select(t => t.ToLowerInvariant()))}");
        return $"{e.Token} [{string.Join(", ", parts)}]";
    }

    /// <summary>
    ///     Turns "mission [normal|coop] [team]" into an exact mission name. Also accepts a full mission
    ///     name such as "Escalation Co-op as BDF".
    /// </summary>
    internal static bool TryResolve(IReadOnlyList<string> args, out string missionName, out string error)
    {
        missionName = "";
        error = "";
        if (args.Count == 0)
        {
            error = "Name a mission.";
            return false;
        }

        if (!Entries.TryGetValue(args[0], out var entry))
        {
            // Full name typed out, e.g. from the console.
            var full = string.Join(" ", args);
            var exact = Entries.Values
                .SelectMany(e => e.CoopByTeam.Values.Append(e.NormalName))
                .FirstOrDefault(n => n != null && string.Equals(ServerActions.ToChoice(n), ServerActions.ToChoice(full), StringComparison.OrdinalIgnoreCase));
            if (exact != null)
            {
                missionName = exact;
                return true;
            }

            error = $"Unknown mission '{args[0]}'. Type /nextmission to list them.";
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

        mode ??= team != null || entry.NormalName == null ? Coop : Normal;

        if (mode == Normal)
        {
            if (entry.NormalName == null)
            {
                error = $"{entry.Token} only exists as co-op. Use {CoopOptions(entry)}.";
                return false;
            }

            missionName = entry.NormalName;
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
                missionName = entry.CoopByTeam.Values.First();
                return true;
            }

            error = $"Pick a team for {entry.Token} co-op: {CoopOptions(entry)}.";
            return false;
        }

        if (!entry.CoopByTeam.TryGetValue(team, out var coopName))
        {
            error = $"{entry.Token} co-op has no team '{team}'. Use {CoopOptions(entry)}.";
            return false;
        }

        missionName = coopName;
        return true;
    }

    private static string CoopOptions(Entry entry)
    {
        return string.Join(" or ", entry.CoopByTeam.Keys.Select(t => $"{Coop}_{t.ToLowerInvariant()}"));
    }

    internal static string ListText()
    {
        return Entries.Count == 0
            ? "No multiplayer missions found."
            : "Missions: " + string.Join("; ", All.Select(Describe));
    }
}
