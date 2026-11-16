using System;
using System.Linq;
using NuclearOption.ModdingApi;

namespace Nuclei;

/// <summary>
///     Staff roles. Values map directly onto the game's <see cref="PermissionLevel" /> byte
///     (Player 0, Moderator 50, Admin 100, Host 255). Host can't be given to players, so Owner uses 254,
///     the same custom level the game's own example mod uses.
/// </summary>
public enum Role : byte
{
    Player = 0,
    Moderator = 50,
    Admin = 100,
    Owner = 254
}

/// <summary>
///     Assigns permission levels from the config lists when players join.
/// </summary>
internal static class RoleService
{
    internal static void Init()
    {
        PlayerHandle.PlayerJoined += OnPlayerJoined;
    }

    internal static PermissionLevel ToPermission(Role role)
    {
        return (PermissionLevel)(byte)role;
    }

    internal static Role GetRole(ulong steamId)
    {
        if (NucleiConfig.ParseIds(NucleiConfig.Owner.Value).Contains(steamId))
            return Role.Owner;
        if (NucleiConfig.ParseIds(NucleiConfig.Admins.Value).Contains(steamId))
            return Role.Admin;
        if (NucleiConfig.ParseIds(NucleiConfig.Moderators.Value).Contains(steamId))
            return Role.Moderator;
        return Role.Player;
    }

    internal static bool TryParseRole(string text, out Role role)
    {
        if (string.Equals(text, "mod", StringComparison.OrdinalIgnoreCase))
        {
            role = Role.Moderator;
            return true;
        }

        return Enum.TryParse(text, true, out role) && Enum.IsDefined(typeof(Role), role);
    }

    private static void OnPlayerJoined(PlayerHandle player)
    {
        try
        {
            Apply(player);
            Messages.SendWelcome(player);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Error handling join of {player.SteamId}: {e}");
        }
    }

    /// <summary>Pushes the configured role (and optional tag) onto a connected player.</summary>
    internal static void Apply(PlayerHandle player)
    {
        if (player.IsHost)
            return;

        var role = GetRole(player.SteamId);
        if (player.PermissionLevel != ToPermission(role))
        {
            if (player.SetPermissionLevel(ToPermission(role)))
                Plugin.Log.LogInfo($"{Names.Of(player)} ({player.SteamId}) is now {role}.");
            else
                Plugin.Log.LogWarning($"Failed to set role {role} for {player.SteamId}.");
        }

        if (NucleiConfig.ShowRoleTags.Value)
            player.SetServerTag(role switch
            {
                Role.Owner => NucleiConfig.OwnerTag.Value,
                Role.Admin => NucleiConfig.AdminTag.Value,
                Role.Moderator => NucleiConfig.ModeratorTag.Value,
                _ => ""
            });
    }

    /// <summary>Saves a role change to the config and applies it right away if the player is online.</summary>
    internal static void SetRole(ulong steamId, Role role)
    {
        var owner = NucleiConfig.ParseIds(NucleiConfig.Owner.Value);
        var admins = NucleiConfig.ParseIds(NucleiConfig.Admins.Value);
        var mods = NucleiConfig.ParseIds(NucleiConfig.Moderators.Value);
        // Only one owner: promoting someone to Owner demotes the previous owner to Player.
        var affected = role == Role.Owner ? owner.Append(steamId).Distinct().ToList() : [steamId];
        owner.Remove(steamId);
        admins.Remove(steamId);
        mods.Remove(steamId);

        switch (role)
        {
            case Role.Owner:
                owner = [steamId];
                break;
            case Role.Admin:
                admins.Add(steamId);
                break;
            case Role.Moderator:
                mods.Add(steamId);
                break;
        }

        NucleiConfig.Owner.Value = NucleiConfig.JoinIds(owner.Take(1));
        NucleiConfig.Admins.Value = NucleiConfig.JoinIds(admins);
        NucleiConfig.Moderators.Value = NucleiConfig.JoinIds(mods);

        foreach (var id in affected)
            if (PlayerHandle.TryGet(id, out var handle))
                Apply(handle);
    }
}
