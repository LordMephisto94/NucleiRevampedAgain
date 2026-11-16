using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nuclei;

/// <summary>
///     Reads admin commands from standard input so panel consoles (AMP, Pterodactyl, a plain
///     terminal) can manage the server. Lines are read on a background thread and executed on the
///     Unity main thread by <see cref="ServerLoop" />.
/// </summary>
internal static class ConsoleCommands
{
    private static readonly ConcurrentQueue<string> Pending = new();
    private static Thread? _reader;

    private const string HelpText =
        "Nuclei console commands:\n" +
        "  status                         mission and player list\n" +
        "  say <message>                  broadcast a chat message\n" +
        "  kick <player>                  kick until the mission changes\n" +
        "  unkick <player|steamid>        let a kicked player rejoin now\n" +
        "  kicks                          list kicked players\n" +
        "  clearkicks                     let all kicked players rejoin\n" +
        "  ban <player> [reason]          ban by name or Steam ID\n" +
        "  setrole <player> <role>        player | moderator | admin | owner\n" +
        "  endmission [seconds]           end the current mission (default 10)\n" +
        "  nextmission [mission] [normal|coop_<team>] [minutes]\n" +
        "                                 set the next mission, or list missions\n" +
        "  reload                         reload NucleiRevamped.cfg\n" +
        "  help                           this list";

    internal static void Start()
    {
        _reader = new Thread(ReadLoop) { IsBackground = true, Name = "Nuclei stdin" };
        _reader.Start();
        Plugin.Log.LogInfo("Console commands enabled. Type 'help' in the server console.");
    }

    private static void ReadLoop()
    {
        try
        {
            using var stdin = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
            while (stdin.ReadLine() is { } line)
                if (!string.IsNullOrWhiteSpace(line))
                    Pending.Enqueue(line.Trim());
            Plugin.Log.LogInfo("Standard input closed; console commands stopped.");
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Console command reader stopped: {e.Message}");
        }
    }

    /// <summary>Runs queued commands. Called every frame on the main thread.</summary>
    internal static void Drain()
    {
        while (Pending.TryDequeue(out var line))
        {
            try
            {
                Execute(line);
            }
            catch (Exception e)
            {
                Reply($"Command failed: {e.Message}");
            }
        }
    }

    private static void Reply(string text)
    {
        Plugin.Log.LogInfo(text.Replace("\n", "\n[Nuclei] "));
    }

    private static void Execute(string line)
    {
        var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].TrimStart('/').ToLowerInvariant();
        var args = parts.Skip(1).ToArray();
        var rest = string.Join(" ", args);

        switch (command)
        {
            case "help":
                Reply(HelpText);
                break;
            case "status":
            case "players":
                Reply(ServerActions.Status());
                break;
            case "say":
                if (rest.Length == 0) { Reply("Usage: say <message>"); break; }
                Messages.Broadcast(rest);
                Reply($"Said: {rest}");
                break;
            case "kick":
                if (args.Length == 0) { Reply("Usage: kick <player>"); break; }
                Reply(ServerActions.TryResolvePlayer(args[0], out var kickId, out var kickError)
                    ? ServerActions.Kick(kickId)
                    : kickError);
                break;
            case "unkick":
                if (args.Length == 0) { Reply("Usage: unkick <player|steamid>"); break; }
                Reply(ServerActions.TryResolveKicked(rest, out var unkickId, out var unkickError)
                    ? ServerActions.Unkick(unkickId)
                    : unkickError);
                break;
            case "kicks":
                Reply(ServerActions.ListKicks());
                break;
            case "clearkicks":
                Reply(ServerActions.ClearKicks());
                break;
            case "ban":
                if (args.Length == 0) { Reply("Usage: ban <player> [reason]"); break; }
                Reply(ServerActions.TryResolvePlayer(args[0], out var banId, out var banError)
                    ? ServerActions.Ban(banId, string.Join(" ", args.Skip(1)))
                    : banError);
                break;
            case "setrole":
                if (args.Length < 2 || !RoleService.TryParseRole(args[1], out var role))
                {
                    Reply("Usage: setrole <player> <player|moderator|admin|owner>");
                    break;
                }

                if (!ServerActions.TryResolvePlayer(args[0], out var roleId, out var roleError))
                {
                    Reply(roleError);
                    break;
                }

                RoleService.SetRole(roleId, role);
                Reply($"{Names.Of(roleId)} ({roleId}) is now {role}.");
                break;
            case "endmission":
                var seconds = 10f;
                if (args.Length > 0 && !float.TryParse(args[0], out seconds))
                {
                    Reply("Usage: endmission [seconds]");
                    break;
                }

                Reply(ServerActions.EndMission(seconds));
                break;
            case "nextmission":
                if (rest.Length == 0)
                    Reply(ServerActions.ListMissions());
                else
                    UniTask.Void(async () => Reply(await ServerActions.SetNextMission(args)));
                break;
            case "reload":
                Plugin.Instance?.Config.Reload();
                foreach (var player in ServerActions.GetPlayers())
                    RoleService.Apply(player);
                Reply("Config reloaded and roles re-applied.");
                break;
            default:
                Reply($"Unknown command '{command}'. Type 'help'.");
                break;
        }
    }
}
