using System;
using NuclearOption.Chat;
using NuclearOption.DedicatedServer;
using NuclearOption.ModdingApi;
using NuclearOption.Networking;
using Steamworks;
using UnityEngine;

namespace Nuclei;

internal static class Placeholders
{
    internal const string PlayerName = "{player_name}";
    internal const string SteamId = "{steamid}";
    internal const string ServerName = "{server_name}";
    internal const string MissionName = "{mission_name}";
    internal const string PlayerCount = "{player_count}";

    internal const string Help =
        "Placeholders: {player_name}, {steamid}, {server_name}, {mission_name}, {player_count}.";

    internal static string Fill(string text, PlayerHandle? player = null)
    {
        if (player is { } p)
        {
            // Mentions render as the player's (censor-aware) name on each client.
            text = text.Replace(PlayerName, p.Mention);
            text = text.Replace(SteamId, p.SteamId.ToString());
        }

        text = text.Replace(ServerName, DedicatedServerManager.Instance?.Config?.ServerName ?? "the server");
        text = text.Replace(MissionName, ServerActions.CurrentMissionName());
        text = text.Replace(PlayerCount, PlayerHandle.PlayerCount.ToString());
        return text;
    }
}

internal static class Names
{
    /// <summary>Plain display name for logs and console output.</summary>
    internal static string Of(PlayerHandle player)
    {
        try
        {
            // Right after joining the game hasn't resolved the Steam name yet and reports a
            // placeholder, so fall back to the name the client sent during authentication.
            var resolved = player.GetInternalPlayer()?.GetPlayerName()?.SanitizedName;
            if (!string.IsNullOrWhiteSpace(resolved) && resolved != "Player")
                return resolved!;
            var owner = player.GetInternalPlayer()?.Owner;
            var reported = owner != null ? PlayerHelper.GetAuthData(owner)?.ClientReportedSteamName : null;
            if (!string.IsNullOrWhiteSpace(reported))
                return reported!;
        }
        catch
        {
            // fall through
        }

        return Of(player.SteamId);
    }

    internal static string Of(ulong steamId)
    {
        try
        {
            return ChatHelper.GetMentionName(new CSteamID(steamId));
        }
        catch
        {
            return steamId.ToString();
        }
    }
}

internal static class Messages
{
    private static float _lastMotD;

    internal static bool ServerActive => NuclearOption.Networking.NetworkManagerNuclearOption.i?.Server?.Active ?? false;

    internal static void SendWelcome(PlayerHandle player)
    {
        var template = NucleiConfig.WelcomeMessage.Value;
        if (string.IsNullOrWhiteSpace(template) || !ServerActive)
            return;
        ServerChat.SendToPlayer(player, Placeholders.Fill(template, player), false);
    }

    internal static void Broadcast(string message)
    {
        if (!ServerActive)
        {
            Plugin.Log.LogWarning("Can't send chat: no mission is running.");
            return;
        }

        ServerChat.SendToAll(Placeholders.Fill(message), false);
    }

    /// <summary>Called every frame from <see cref="ServerLoop" />.</summary>
    internal static void TickMotD()
    {
        var interval = NucleiConfig.MotDIntervalSeconds.Value;
        if (interval <= 0 || string.IsNullOrWhiteSpace(NucleiConfig.MessageOfTheDay.Value))
            return;
        if (Time.realtimeSinceStartup - _lastMotD < interval)
            return;
        _lastMotD = Time.realtimeSinceStartup;

        if (!ServerActive || PlayerHandle.PlayerCount == 0)
            return;

        try
        {
            ServerChat.SendToAll(Placeholders.Fill(NucleiConfig.MessageOfTheDay.Value), false);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Failed to send Message of the Day: {e.Message}");
        }
    }
}
