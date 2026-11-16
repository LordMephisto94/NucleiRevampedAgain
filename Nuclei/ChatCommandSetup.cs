using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using NuclearOption.ModdingApi;

namespace Nuclei;

/// <summary>
///     Registers in-game chat commands through the game's official ModdingApi. The game handles
///     parsing, autocomplete, /help and the permission check; built-ins (/kick, /adminkick, /help,
///     /ping, /clear) keep working and respect the roles Nuclei assigns.
/// </summary>
internal static class ChatCommandSetup
{
    internal static void RegisterAll()
    {
        Register("say", "Broadcast a server message to everyone",
            Sync(request =>
            {
                var message = string.Join(" ", request.Args);
                Messages.Broadcast(message);
                Plugin.Log.LogInfo($"{Names.Of(request.Sender)} used /say: {message}");
                return ChatCommandResult.Ok("Message sent.");
            }),
            [new ChatCommandArgument("message", ChatArgumentType.Text, false)],
            NucleiConfig.SayPermission.Value);

        Register("announce", "Announcement in the message feed with a sound",
            Sync(request =>
            {
                var result = Messages.Announce(string.Join(" ", request.Args));
                return result == "Announcement shown." ? ChatCommandResult.Ok(result) : ChatCommandResult.ClientError(result);
            }),
            [new ChatCommandArgument("message", ChatArgumentType.Text, false)],
            NucleiConfig.AnnouncePermission.Value);

        // Seconds is required and first: the game validates arguments by position, so an optional
        // number before free text would reject "/popup Hello".
        Register("popup", "Popup window for everyone. Seconds 0 = until OK. Optional title: Title | Message",
            Sync(request =>
            {
                if (!request.TryGetInt(0, out var seconds, out var error))
                    return ChatCommandResult.ClientError(error);
                if (seconds < 0)
                    return ChatCommandResult.ClientError("Seconds can't be negative (0 = until someone presses OK).");
                var text = string.Join(" ", request.Args.Skip(1));
                var bar = text.IndexOf('|');
                var result = bar >= 0
                    ? Messages.Popup(text.Substring(0, bar), text.Substring(bar + 1), seconds)
                    : Messages.Popup("", text, seconds);
                return result.StartsWith("Popup") ? ChatCommandResult.Ok(result) : ChatCommandResult.ClientError(result);
            }),
            [
                new ChatCommandArgument("seconds", ChatArgumentType.Int, false),
                new ChatCommandArgument("title|message", ChatArgumentType.Text, false)
            ],
            NucleiConfig.PopupPermission.Value);

        Register("ban", "Ban a player (writes to ban_list.txt)",
            Sync(request =>
            {
                if (!request.TryGetTargetSteamId(0, out var steamId, out var error))
                    return ChatCommandResult.ClientError(error);
                if (request.Sender.SteamId == steamId)
                    return ChatCommandResult.ClientError("You can't ban yourself.");
                if (RoleService.GetRole(steamId) >= RoleService.GetRole(request.Sender.SteamId) && !request.Sender.IsHost)
                    return ChatCommandResult.ClientError("You can't ban someone with an equal or higher role.");
                var reason = string.Join(" ", request.Args, 1, Math.Max(0, request.Args.Count - 1));
                return Ok(ServerActions.Ban(steamId, reason));
            }),
            [
                new ChatCommandArgument("player", ChatArgumentType.Player, false),
                new ChatCommandArgument("reason", ChatArgumentType.Text, true)
            ],
            NucleiConfig.BanPermission.Value);

        // Kicked players are offline, so this takes a Steam ID or a name from the kick list as text.
        Register("unkick", "Let a kicked player rejoin now (Steam ID or name; no argument lists kicks)",
            Sync(request =>
            {
                if (request.Args.Count == 0)
                    return ChatCommandResult.Ok(ServerActions.ListKicks());
                return ServerActions.TryResolveKicked(string.Join(" ", request.Args), out var steamId, out var error)
                    ? Ok(ServerActions.Unkick(steamId))
                    : ChatCommandResult.ClientError(error);
            }),
            [new ChatCommandArgument("player|steamid", ChatArgumentType.Text, true)],
            NucleiConfig.UnkickPermission.Value);

        Register("endmission", "End the current mission (default: in 10 seconds)",
            Sync(request =>
            {
                var seconds = 10f;
                if (request.Args.Count > 0 && !request.TryGetFloat(0, out seconds, out var error))
                    return ChatCommandResult.ClientError(error);
                return Ok(ServerActions.EndMission(seconds));
            }),
            [new ChatCommandArgument("seconds", ChatArgumentType.Float, true)],
            NucleiConfig.EndMissionPermission.Value);

        // /nextmission is registered later by RegisterNextMission, once the server's rotation is known.

        Register("setrole", "Set a player's role: player, moderator, admin or owner",
            Sync(request =>
            {
                if (!request.TryGetTargetSteamId(0, out var steamId, out var error))
                    return ChatCommandResult.ClientError(error);
                if (request.Args.Count < 2 || !RoleService.TryParseRole(request.Args[1], out var role))
                    return ChatCommandResult.ClientError("Role must be player, moderator, admin or owner.");
                RoleService.SetRole(steamId, role);
                return Ok($"{Names.Of(steamId)} is now {role}.");
            }),
            [
                new ChatCommandArgument("player", ChatArgumentType.Player, false),
                ChatCommandArgument.FromChoices("role", ["player", "moderator", "admin", "owner"], false)
            ],
            NucleiConfig.SetRolePermission.Value);
    }

    /// <summary>
    ///     Registers /nextmission with every built-in multiplayer mission as autocomplete choices, plus
    ///     normal/coop and team choices. Called from <see cref="ServerLoop" /> once the dedicated server
    ///     is running, because missions aren't indexed before that. The game forbids spaces in choices,
    ///     so "Terminal Control" is offered as "Terminal_Control".
    /// </summary>
    internal static void TryRegisterNextMission()
    {
        if (MissionCatalog.IsBuilt || !NuclearOption.DedicatedServer.DedicatedServerManager.IsRunning)
            return;
        try
        {
            MissionCatalog.Build();
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Couldn't index missions, /nextmission falls back to free text: {e.Message}");
        }

        // The game validates arguments strictly by position, so mode and team share one slot
        // (normal, coop_bdf, coop_pala) to let the duration follow without filler arguments.
        var missions = MissionCatalog.All.Select(e => e.Token).ToArray();
        var variants = new[] { MissionCatalog.Normal }
            .Concat(MissionCatalog.Teams.Select(t => $"{MissionCatalog.Coop}_{t}"))
            .ToArray();
        ChatCommandArgument[] arguments = missions.Length > 0
            ?
            [
                ChatCommandArgument.FromChoices("mission", missions, true),
                ChatCommandArgument.FromChoices("mode", variants, true),
                new ChatCommandArgument("minutes", ChatArgumentType.Int, true)
            ]
            : [new ChatCommandArgument("mission", ChatArgumentType.Text, true)];

        Register("nextmission", "Choose the next mission: mission [normal|coop_team] [minutes]",
            async request =>
            {
                if (request.Args.Count == 0)
                    return ChatCommandResult.Ok(ServerActions.ListMissions());
                return ChatCommandResult.Ok(await ServerActions.SetNextMission(request.Args));
            },
            arguments,
            NucleiConfig.NextMissionPermission.Value);
    }

    private static ChatCommandResult Ok(string message)
    {
        Plugin.Log.LogInfo(message);
        return ChatCommandResult.Ok(message);
    }

    private static ChatCommandAsyncCallback Sync(ChatCommandCallback callback)
    {
        return request => UniTask.FromResult(callback(request));
    }

    private static void Register(string name, string description, ChatCommandAsyncCallback callback,
        ChatCommandArgument[] arguments, Role permission)
    {
        try
        {
            if (!ChatCommands.RegisterServerCommand(name, description, callback, arguments, RoleService.ToPermission(permission), []))
                Plugin.Log.LogWarning($"Chat command /{name} was not registered: the name is already taken.");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Failed to register chat command /{name}: {e.Message}");
        }
    }
}
