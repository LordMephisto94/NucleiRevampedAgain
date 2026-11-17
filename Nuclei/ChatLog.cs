using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirage;
using NuclearOption.Chat;
using NuclearOption.Networking;
using UnityEngine;

namespace Nuclei;

/// <summary>
///     Logs player chat to the server console. The game has no chat event in its ModdingApi, so this
///     hooks the server-side chat handler. Its generated name ends in a hash that changes between game
///     versions, so the method is looked up by prefix instead of hard-coding it.
/// </summary>
internal static class ChatLog
{
    internal static void TryApply(Harmony harmony)
    {
        var target = typeof(ChatManager)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name.StartsWith("UserCode_CmdSendChatMessage_", StringComparison.Ordinal));
        if (target == null)
        {
            Plugin.Log.LogWarning("Chat logging disabled: ChatManager chat handler not found in this game version.");
            return;
        }

        try
        {
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(ChatLog), nameof(Postfix)));
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Chat logging disabled: {e.Message}");
        }
    }

    private static void Postfix(string message, bool allChat, INetworkPlayer sender)
    {
        try
        {
            if (!PlayerHelper.TryGetPlayer(sender, out Player player))
                return;
            var scope = allChat ? "All" : "Team";
            // One stable line per message so panels can match it, e.g. AMP's Console.UserChatRegex.
            Debug.Log($"[Chat] [{scope}] {Names.Of(player.SteamID)} ({player.SteamID}): {message}");
        }
        catch
        {
            // Never break chat because of logging.
        }
    }
}
