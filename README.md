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
* **Chat commands:** `/say`, `/ban`, `/unkick`, `/endmission`, `/nextmission` and `/setrole`. They're registered
  through the game's ModdingApi, so they show up in `/help` and autocomplete.
* **Welcome message and Message of the Day**, with placeholders.
* **Console commands** for server panels (AMP, Pterodactyl, a terminal): `status`, `say`, `kick`,
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
| `/ban <player> [reason]` | Moderator | Adds the player to `ban_list.txt` and kicks them |
| `/endmission [seconds]` | Admin | Ends the current mission (10 s by default) |
| `/nextmission [mission] [normal\|coop_bdf\|coop_pala] [minutes]` | Admin | Queues the next mission, optionally co-op as a team and with a time limit; with no arguments, lists missions |
| `/unkick [player\|steamid]` | Moderator | Lets a kicked player rejoin now; with no argument, lists kicked players |
| `/setrole <player> <role>` | Owner | Sets a role: `player`, `moderator`, `admin` or `owner` |

`/nextmission` autocompletes every built-in multiplayer mission. Mission names use underscores
(`Terminal_Control`) because the game doesn't allow spaces in choices. Examples:
`/nextmission Escalation normal 120` and `/nextmission Terminal_Control coop_pala 90`. Without a
duration, missions from your rotation keep their configured `MaxTime`, and other missions use the
current mission's limit. The command only queues the mission; `/endmission` switches immediately.

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
