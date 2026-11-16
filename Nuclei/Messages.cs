using System;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
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

    /// <summary>
    ///     Shows text as the large on-screen mission message (the banner used for objectives), with the
    ///     mission message sound. Uses the RPC directly: MissionMessages.ShowMessage also draws it locally,
    ///     which needs a HUD the headless server doesn't have.
    /// </summary>
    internal static string Announce(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "Usage: announce <message>";
        if (!ServerActive || MissionMessages.i == null)
            return "Announcements need a running mission (nobody is playing yet).";

        var text = Placeholders.Fill(message);
        MissionMessages.i.RpcShowMessage(text, true, null);
        Plugin.Log.LogInfo($"Announced: {text}");
        return "Announcement shown.";
    }

    private static MethodInfo? _dialogueButton;

    /// <summary>
    ///     Shows the mission dialog box (a centred window with a title, text and an OK button) to all
    ///     players. The game keeps one shared dialog queue, so the first player to press OK closes it for
    ///     everyone, and scripted mission dialogs wait behind it. To keep that short, the popup is closed
    ///     after <paramref name="seconds" /> (0 = stays until someone presses OK).
    /// </summary>
    internal static string Popup(string title, string body, int seconds)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "Usage: popup [seconds] [title |] <message>";
        var messages = MissionMessages.i;
        if (!ServerActive || messages == null)
            return "Popups need a running mission (nobody is playing yet).";

        title = Placeholders.Fill(string.IsNullOrWhiteSpace(title) ? "Server" : title.Trim());
        body = Placeholders.Fill(body.Trim());
        messages.ShowDialogue(title, body, "OK", null, () => { });
        var id = messages.dialogueQueue.Last().id;
        var queued = messages.dialogueQueue.Count > 1;
        Plugin.Log.LogInfo($"Popup '{title}': {body} ({(seconds > 0 ? $"closes after {seconds}s" : "until OK")})");

        if (seconds > 0)
            UniTask.Void(async () =>
            {
                await UniTask.Delay(seconds * 1000, ignoreTimeScale: true);
                CloseDialogue(messages, id);
            });

        return queued ? "Popup queued behind another dialog; it shows when that one closes." : "Popup shown.";
    }

    private static void CloseDialogue(MissionMessages messages, int id)
    {
        // The mission may have changed (old MissionMessages destroyed) before the timer fires.
        if (messages == null || MissionMessages.i != messages)
            return;
        try
        {
            // Same server handler a player's OK click runs. Its generated name ends in a version hash.
            _dialogueButton ??= typeof(MissionMessages)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name.StartsWith("UserCode_CmdDialogueButton_", StringComparison.Ordinal));
            if (_dialogueButton == null)
            {
                Plugin.Log.LogWarning("Can't auto-close popups in this game version; players must press OK.");
                return;
            }

            _dialogueButton.Invoke(messages, [id]);
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Failed to close popup: {e.InnerException?.Message ?? e.Message}");
        }
    }

    /// <summary>Parses "[seconds] [title |] message" for the popup commands.</summary>
    internal static string PopupFromArgs(string text)
    {
        text = text.Trim();
        var seconds = NucleiConfig.PopupSeconds.Value;
        var firstSpace = text.IndexOf(' ');
        if (firstSpace > 0 && int.TryParse(text.Substring(0, firstSpace), out var s) && s >= 0)
        {
            seconds = s;
            text = text.Substring(firstSpace + 1);
        }

        var bar = text.IndexOf('|');
        return bar >= 0
            ? Popup(text.Substring(0, bar), text.Substring(bar + 1), seconds)
            : Popup("", text, seconds);
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
