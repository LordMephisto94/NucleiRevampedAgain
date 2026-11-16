using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using NuclearOption.DedicatedServer;
using NuclearOption.DedicatedServer.Commands;
using NuclearOption.ModdingApi;
using NuclearOption.Networking;
using Steamworks;

namespace Nuclei;

/// <summary>
///     Admin actions shared by chat commands and console commands. Each returns a short
///     human-readable result. Must be called on the Unity main thread.
/// </summary>
internal static class ServerActions
{
    private static DedicatedServerManager? Manager => DedicatedServerManager.Instance;

    internal static List<PlayerHandle> GetPlayers()
    {
        var list = new List<PlayerHandle>();
        PlayerHandle.GetAll(list);
        return list;
    }

    /// <summary>Resolves a Steam ID, a mention, or a (partial) player name to a Steam ID.</summary>
    internal static bool TryResolvePlayer(string text, out ulong steamId, out string error)
    {
        error = "";
        if (PlayerHandle.TryParseSteamId(text, out steamId))
            return true;
        if (PlayerHandle.TryFromArgument(text, out var handle))
        {
            steamId = handle.SteamId;
            return true;
        }

        var matches = GetPlayers()
            .Where(p => Names.Of(p).IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();
        if (matches.Count == 1)
        {
            steamId = matches[0].SteamId;
            return true;
        }

        error = matches.Count == 0 ? $"No player matches '{text}'." : $"'{text}' matches {matches.Count} players, be more specific.";
        return false;
    }

    private static NuclearOption.Networking.Authentication.NetworkAuthenticatorNuclearOption Auth =>
        NetworkManagerNuclearOption.i.Authenticator;

    /// <summary>
    ///     Kicks for the rest of the current mission, same as the game's /adminkick. The game's own
    ///     direct kick (scoreboard, remote "kick-player") instead blocks the player until the server
    ///     restarts, which on a dedicated server can be days.
    /// </summary>
    internal static string Kick(ulong steamId)
    {
        if (!PlayerHandle.TryGet(steamId, out var handle))
            return $"{steamId} is not connected.";
        if (handle.IsHost)
            return "Can't kick the host.";

        var name = Names.Of(handle);
        var mention = handle.Mention;
        Auth.OnMissionKick(new CSteamID(steamId));
        if (Messages.ServerActive)
            ServerChat.SendToAll($"{mention} was kicked by an admin.", false);
        return $"Kicked {name} ({steamId}). They can rejoin when the mission changes, or after 'unkick'.";
    }

    /// <summary>Lets a kicked player back in (both the mission and the until-restart kick lists).</summary>
    internal static string Unkick(ulong steamId)
    {
        var id = new CSteamID(steamId);
        var found = Auth.KickList.Contains(id) | Auth.MissionKickList.Contains(id);
        Auth.KickList.Remove(id);
        Auth.MissionKickList.Remove(id);
        return found
            ? $"Unkicked {Names.Of(steamId)} ({steamId}); they can rejoin now."
            : $"{steamId} is not on a kick list. (Bans are in ban_list.txt.)";
    }

    internal static string ListKicks()
    {
        var lines = Auth.KickList.GetItems().Select(e => $"  {Names.Of(e.Item1.m_SteamID)} | {e.Item1.m_SteamID} | until restart")
            .Concat(Auth.MissionKickList.GetItems().Select(e => $"  {Names.Of(e.Item1.m_SteamID)} | {e.Item1.m_SteamID} | this mission"))
            .ToList();
        return lines.Count == 0 ? "Nobody is kicked." : "Kicked players:\n" + string.Join("\n", lines);
    }

    internal static string ClearKicks()
    {
        Auth.KickList.Clear();
        Auth.MissionKickList.Clear();
        return "Kick lists cleared; everyone except banned players can rejoin.";
    }

    /// <summary>Resolves a Steam ID, or the name of a player on one of the kick lists.</summary>
    internal static bool TryResolveKicked(string text, out ulong steamId, out string error)
    {
        error = "";
        if (PlayerHandle.TryParseSteamId(text, out steamId))
            return true;
        var matches = Auth.KickList.GetItems().Concat(Auth.MissionKickList.GetItems())
            .Select(e => e.Item1.m_SteamID)
            .Distinct()
            .Where(id => Names.Of(id).IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();
        if (matches.Count == 1)
        {
            steamId = matches[0];
            return true;
        }

        error = matches.Count == 0
            ? $"No kicked player matches '{text}'. Use their Steam ID (see 'kicks')."
            : $"'{text}' matches {matches.Count} kicked players; use the Steam ID.";
        return false;
    }

    internal static string Ban(ulong steamId, string reason)
    {
        if (!DedicatedServerManager.IsRunning)
            return "Bans need the dedicated server to be running.";
        if (RoleService.GetRole(steamId) == Role.Owner)
            return "Can't ban the owner.";

        var name = Names.Of(steamId);
        reason = string.IsNullOrWhiteSpace(reason) ? "Banned by admin" : reason.Replace("\n", " ");
        // Adds the ID to the first file in BanListPaths (ban_list.txt) and disconnects the player.
        NetworkManagerNuclearOption.i.Authenticator.BanPlayer(new CSteamID(steamId), reason, null);
        BanServerCommands.KickPlayer(new CSteamID(steamId));
        return $"Banned {name} ({steamId}): {reason}";
    }

    internal static string EndMission(float secondsLeft)
    {
        if (Manager == null || !DedicatedServerManager.IsRunning)
            return "No dedicated server is running.";
        Manager.SetTimeRemaining(Math.Max(0, secondsLeft));
        if (secondsLeft >= 10)
            Messages.Broadcast($"The mission will end in {secondsLeft:0} seconds.");
        return $"Mission will end in {secondsLeft:0} seconds.";
    }

    private const float DefaultMaxTime = 7200f;
    internal const int MaxMinutes = 24 * 60;

    /// <summary>
    ///     Queues a mission to play after the current one. Accepts
    ///     "mission [normal|coop_team|coop team] [minutes]" (see <see cref="MissionCatalog" />)
    ///     or a full mission name.
    /// </summary>
    internal static async UniTask<string> SetNextMission(IReadOnlyList<string> rawArgs)
    {
        var manager = Manager;
        if (manager == null || !DedicatedServerManager.IsRunning)
            return "No dedicated server is running.";

        // Trailing whole number = duration in minutes; "coop_pala" = "coop pala".
        var args = rawArgs.SelectMany(a => a.StartsWith(MissionCatalog.Coop + "_", StringComparison.OrdinalIgnoreCase)
            ? new[] { MissionCatalog.Coop, a.Substring(MissionCatalog.Coop.Length + 1) }
            : new[] { a }).ToList();
        int? minutes = null;
        if (args.Count > 1 && int.TryParse(args[args.Count - 1], out var parsed))
        {
            if (parsed < 1 || parsed > MaxMinutes)
                return $"Duration must be between 1 and {MaxMinutes} minutes.";
            minutes = parsed;
            args.RemoveAt(args.Count - 1);
        }

        if (!MissionCatalog.TryResolve(args, out var missionName, out var error))
            return error;

        // Without a duration, rotation entries keep their configured time limit and anything else
        // uses the current mission's.
        var rotation = manager.Config.MissionRotation ?? [];
        var match = rotation.FirstOrDefault(m => string.Equals(m.Key.Name, missionName, StringComparison.OrdinalIgnoreCase));
        var option = match.Key.Name != null
            ? match
            : new MissionOptions
            {
                Key = new MissionKeySaveable { Group = "BuiltIn", Name = missionName },
                MaxTime = manager.CurrentMissionOption.MaxTime > 0 ? manager.CurrentMissionOption.MaxTime : DefaultMaxTime
            };
        if (minutes != null)
            option.MaxTime = minutes.Value * 60f;

        var ok = await manager.SetNextMissionAsync(option);
        return ok
            ? $"Next mission set to {missionName} ({option.MaxTime / 60:0} min). Use /endmission to switch now."
            : $"Couldn't load mission '{missionName}'.";
    }

    /// <summary>Mission name as a single chat token: "Terminal Control" becomes "Terminal_Control".</summary>
    internal static string ToChoice(string? missionName)
    {
        return string.Join("_", (missionName ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    internal static string ListMissions()
    {
        var rotation = Manager?.Config?.MissionRotation;
        var rotationText = rotation == null || rotation.Length == 0
            ? "No mission rotation configured."
            : "Rotation: " + string.Join(", ", rotation.Select(m => m.Key.Name));
        return rotationText + "\n" + MissionCatalog.ListText() +
               "\nUsage: nextmission <mission> [normal|coop_<team>] [minutes]";
    }

    /// <summary>
    ///     Name of the mission the server is on. Between matches the server idles on an empty scene
    ///     (MissionManager reports a placeholder), so ask the server manager instead.
    /// </summary>
    internal static string CurrentMissionName()
    {
        var manager = Manager;
        if (manager == null || !DedicatedServerManager.IsRunning)
            return "none";
        var name = manager.CurrentMissionOption.Key.Name;
        return string.IsNullOrEmpty(name) ? manager.NextMissionOption.Key.Name ?? "none" : name;
    }

    internal static string Status()
    {
        var players = GetPlayers();
        var manager = Manager;
        var next = manager != null && DedicatedServerManager.IsRunning ? manager.NextMissionOption.Key.Name : null;
        var lines = new List<string>
        {
            $"Mission: {CurrentMissionName()} | Next: {next ?? "?"} | Players: {players.Count}"
        };
        lines.AddRange(players.Select(p =>
            $"  {Names.Of(p)} | {p.SteamId} | {p.FactionName} | {RoleService.GetRole(p.SteamId)}"));
        return string.Join("\n", lines);
    }
}
