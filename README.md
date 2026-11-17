# NucleiRevamped

> A fork of [Nuclei](https://github.com/MaxWasUnavailable/Nuclei) by
> [MaxWasUnavailable](https://github.com/MaxWasUnavailable). Version 2.0 is a rewrite for the
> official Nuclear Option dedicated server.

A server-side BepInEx 5 plugin for the **official Nuclear Option dedicated server** (Steam app
`3930080`, `NuclearOptionServer.x86_64` / `.exe`). Players don't need to install anything.

The game's server already handles hosting, mission rotation, time limits, passwords, bans and
vote-kicks through `DedicatedServerConfig.json`. NucleiRevamped adds what the game leaves out:

* **Staff roles.** Owner, Admin and Moderator are assigned from Steam IDs in the config. They use the
  game's own permission system, so built-in commands like `/adminkick` work for your staff.
* **Chat commands:** `/say`, `/announce`, `/popup`, `/ban`, `/unkick`, `/endmission`, `/nextmission` and `/setrole`. They're registered
  through the game's ModdingApi, so they show up in `/help` and autocomplete.
* **Welcome message and Message of the Day**, with placeholders.
* **Console commands** for server panels (AMP, Pterodactyl, a terminal): `status`, `say`, `announce`, `popup`, `missions`, `workshopmission`, `refreshmissions`, `kick`,
  `unkick`, `kicks`, `clearkicks`, `ban`, `setrole`, `endmission`, `nextmission`, `reload` and `help`.
* **Chat logging** to the server console.

## Installing on AMP

1. Create an instance from CubeCoders' **Nuclear Option** template.
2. In *Configuration → Updates*, enable **Install BepInEx**, then run **Update**.
3. In *Configuration → Nuclear Option*, enable **Modded Server**.
4. Upload `NucleiRevamped.dll` to `nuclear-option/3930080/BepInEx/plugins/`.
5. Start the server once, stop it, and edit `BepInEx/config/NucleiRevamped.cfg`. At minimum, set
   `Owner` to your 64-bit Steam ID.
6. Type `help` in AMP's console to see the console commands.

## Installing elsewhere

Install [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) (the `linux_x64` or `win_x64` build)
into the server folder, put `NucleiRevamped.dll` in `BepInEx/plugins/`, and start the server. On
Linux, use BepInEx's `run_bepinex.sh` or set the Doorstop environment variables yourself.

## Chat commands

| Command | Default role | What it does |
|---|---|---|
| `/say <message>` | Admin | Broadcasts a server message |
| `/announce <message>` | Admin | Server announcement in the message feed, with a sound |
| `/popup <seconds> <title\|message>` | Admin | Centred popup window for everyone; `0` seconds = until someone presses OK |
| `/ban <player> [reason]` | Moderator | Adds the player to `ban_list.txt` and kicks them |
| `/endmission [seconds]` | Admin | Ends the current mission (10 s by default) |
| `/nextmission [mission] [normal\|coop_bdf\|coop_pala] [minutes]` | Admin | Queues the next mission, optionally co-op as a team and with a time limit; with no arguments, lists missions |
| `/workshopmission <id> [minutes]` | Admin | Queues any Steam Workshop mission by its item ID (the server downloads it) |
| `/missions [builtin\|custom\|workshop]` | Everyone | Lists the missions the server can play |
| `/refreshmissions` | Admin | Re-scans custom mission files after you add some |
| `/unkick [player\|steamid]` | Moderator | Lets a kicked player rejoin now; with no argument, lists kicked players |
| `/setrole <player> <role>` | Owner | Sets a role: `player`, `moderator`, `admin` or `owner` |

`/nextmission` autocompletes every multiplayer mission the server has: built-in missions, custom
missions and Workshop missions from your rotation.

**Custom missions:** put each mission in its own folder inside the `MissionDirectory` from
`DedicatedServerConfig.json`, as `<MissionDirectory>/<Mission Name>/<Mission Name>.json`. That's the
same layout the in-game editor saves to, so you can copy a folder from your own game's Missions folder.
On AMP that's `nuclear-option/3930080/Missions/`. Run `/refreshmissions` (or `refreshmissions` in the
console) to pick up new files without a restart; players may need to rejoin to see them in
autocomplete. A custom mission with the same name as a built-in one is listed as `custom_<name>`.

**Workshop missions:** `/workshopmission 3271890874 90` queues Workshop item 3271890874 for 90
minutes. The item ID is the number in the Workshop page's URL.

Mission names use underscores (`Terminal_Control`) because the game doesn't allow spaces in choices. Examples:
`/nextmission Escalation normal 120` and `/nextmission Terminal_Control coop_pala 90`. Without a
duration, missions from your rotation keep their configured `MaxTime`, and other missions use the
current mission's limit. The command only queues the mission; `/endmission` switches immediately.

`/popup` uses the game's mission dialog box. There is one per server, so the first player to press OK
closes it for everyone, and scripted PvE mission dialogs wait until it closes. That's why it closes
itself after `<seconds>` (`PopupSeconds` in the config sets the console default). Example:
`/popup 15 Server Notice | Restarting in 5 minutes`.

Role tags (`ShowRoleTags`) are sent to everyone, but each player only sees them if **Server Tags** is
enabled in their own game settings.

Kicks: the game's `/adminkick` and vote kicks block the player until the mission changes. Nuclei's console
`kick` does the same. A kick from the scoreboard's right-click menu blocks the player until the server
restarts. `unkick` / `/unkick` lifts both kinds, `kicks` lists them, and `clearkicks` empties both lists.

You can change each command's required role in the `[Commands]` section. The game's built-in
`/help`, `/kick` (vote), `/adminkick`, `/ping` and `/clear` keep working.

## Placeholders

`{player_name}`, `{steamid}`, `{server_name}`, `{mission_name}`, `{player_count}`

## Building

Requires the .NET SDK (8 or newer) and a copy of the game's `Managed` folder.

```bash
dotnet build Nuclei/Nuclei.csproj -c Release \
  -p:NuclearOptionManaged="/path/to/Nuclear Option/NuclearOption_Data/Managed"
```

Set `NUCLEAR_OPTION_PLUGINS` to a `BepInEx/plugins` folder to copy the DLL there after each build.

## License

LGPL v3.0, carried forward from the original project.
