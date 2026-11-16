using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;

namespace Nuclei;

/// <summary>
///     BepInEx config (BepInEx/config/NucleiRevamped.cfg). Server settings such as name, ports,
///     password and mission rotation live in the game's own DedicatedServerConfig.json instead.
/// </summary>
internal static class NucleiConfig
{
    private const string RolesSection = "Roles";
    private const string MessagesSection = "Messages";
    private const string CommandsSection = "Commands";
    private const string ConsoleSection = "Console";

    internal static ConfigEntry<string> Owner = null!;
    internal static ConfigEntry<string> Admins = null!;
    internal static ConfigEntry<string> Moderators = null!;
    internal static ConfigEntry<bool> ShowRoleTags = null!;
    internal static ConfigEntry<string> OwnerTag = null!;
    internal static ConfigEntry<string> AdminTag = null!;
    internal static ConfigEntry<string> ModeratorTag = null!;

    internal static ConfigEntry<string> WelcomeMessage = null!;
    internal static ConfigEntry<string> MessageOfTheDay = null!;
    internal static ConfigEntry<int> MotDIntervalSeconds = null!;

    internal static ConfigEntry<Role> SayPermission = null!;
    internal static ConfigEntry<Role> BanPermission = null!;
    internal static ConfigEntry<Role> UnkickPermission = null!;
    internal static ConfigEntry<Role> EndMissionPermission = null!;
    internal static ConfigEntry<Role> NextMissionPermission = null!;
    internal static ConfigEntry<Role> SetRolePermission = null!;

    internal static ConfigEntry<bool> EnableConsoleCommands = null!;
    internal static ConfigEntry<bool> LogChat = null!;

    internal static void Bind(ConfigFile config)
    {
        Owner = config.Bind(RolesSection, "Owner", "",
            "Steam ID (64-bit) of the server owner. Gets every permission.");
        Admins = config.Bind(RolesSection, "Admins", "",
            "Steam IDs of admins, separated by semicolons.");
        Moderators = config.Bind(RolesSection, "Moderators", "",
            "Steam IDs of moderators, separated by semicolons.");
        ShowRoleTags = config.Bind(RolesSection, "ShowRoleTags", true,
            "Show a tag next to the names of staff members. Players only see tags if Server Tags is enabled in their game settings.");
        OwnerTag = config.Bind(RolesSection, "OwnerTag", "OWNER", "Tag shown for the owner when ShowRoleTags is on.");
        AdminTag = config.Bind(RolesSection, "AdminTag", "ADMIN", "Tag shown for admins when ShowRoleTags is on.");
        ModeratorTag = config.Bind(RolesSection, "ModeratorTag", "MOD", "Tag shown for moderators when ShowRoleTags is on.");

        WelcomeMessage = config.Bind(MessagesSection, "WelcomeMessage",
            $"Welcome to {Placeholders.ServerName}, {Placeholders.PlayerName}!",
            $"Sent privately to each player who joins. Leave empty to disable. {Placeholders.Help}");
        MessageOfTheDay = config.Bind(MessagesSection, "MessageOfTheDay",
            "This server is running NucleiRevamped. Type /help for commands.",
            $"Broadcast to everyone every MotDIntervalSeconds. Leave empty to disable. {Placeholders.Help}");
        MotDIntervalSeconds = config.Bind(MessagesSection, "MotDIntervalSeconds", 900,
            "Seconds between Message of the Day broadcasts. 0 disables it.");

        SayPermission = BindPermission(config, "say", Role.Admin);
        BanPermission = BindPermission(config, "ban", Role.Moderator);
        UnkickPermission = BindPermission(config, "unkick", Role.Moderator);
        EndMissionPermission = BindPermission(config, "endmission", Role.Admin);
        NextMissionPermission = BindPermission(config, "nextmission", Role.Admin);
        SetRolePermission = BindPermission(config, "setrole", Role.Owner);

        EnableConsoleCommands = config.Bind(ConsoleSection, "EnableConsoleCommands", true,
            "Read admin commands from the server's standard input (the AMP / panel console). Type 'help' there for a list.");
        LogChat = config.Bind(ConsoleSection, "LogChat", true,
            "Write every chat message to the server log as '[Chat] Name (steamid): message'.");
    }

    private static ConfigEntry<Role> BindPermission(ConfigFile config, string command, Role defaultRole)
    {
        return config.Bind(CommandsSection, $"{command}Permission", defaultRole,
            $"Lowest role allowed to use /{command}. Changes need a server restart.");
    }

    internal static List<ulong> ParseIds(string value)
    {
        var ids = new List<ulong>();
        foreach (var part in value.Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (ulong.TryParse(part.Trim(), out var id))
                ids.Add(id);
            else
                Plugin.Log.LogWarning($"Ignoring invalid Steam ID '{part}' in config.");
        }

        return ids;
    }

    internal static string JoinIds(IEnumerable<ulong> ids)
    {
        return string.Join(";", ids.Distinct());
    }
}
