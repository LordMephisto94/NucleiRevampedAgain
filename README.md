# NucleiRevamped

> **Notice:** This project is a modified fork of [Nuclei](https://github.com/MaxWasUnavailable/Nuclei?utm_source=gemini), originally authored by [MaxWasUnavailable](https://github.com/MaxWasUnavailable?utm_source=gemini). All core architecture, base features, and the vast majority of the foundational codebase are the work of MaxWasUnavailable. Versions 2.0.0 and later, which introduce native Linux dedicated server support and compatibility with *Nuclear Option* 0.34.1, were vibe-coded by Battle8111 with AI assistance via Cline & Claude Opus 5. 

A powerful, adaptable dedicated server backend for *Nuclear Option*. Originally created by MaxWasUnavailable, this fork is "cleanly" vibe-coded by Battle8111 for native BepInEx Linux execution. Compiled as pure managed .NET Framework 4.8 code, the plugin interfaces seamlessly with the `NuclearOptionServer.x86_64` executable to expand server administration capabilities without requiring client-side downloads.

## Core Capabilities

* **Native Linux Execution:** Optimized for headless Linux hosts and panel environments (like Pterodactyl), utilizing custom POSIX signal handling to prevent Unity engine shutdown hangs.
* **Role-Based Chat Commands:** Features a modular command system (`/kick`, `/ban`, `/say`, `/stop`) gated behind configurable permission tiers (Everyone, Moderator, Admin, Owner).
* **Intelligent Moderation:** Implements dual-track anti-griefing monitors with independent thresholds and actions for player team kills versus friendly AI destruction.
* **Enhanced Vote-Kicking:** Drives the base game's built-in vote-kick interface while adding vital server-side protections, including staff exemptions, minimum population requirements, and chat-based voting.
* **Non-Destructive Configuration:** Overrides official `DedicatedServerConfig.json` parameters (such as mission rotations and time limits) dynamically in memory, leaving your panel's base configuration files untouched.
* **Dynamic Broadcasting:** Supports customizable, color-coded welcome messages and Messages of the Day using dynamic variable placeholders like `{server_name}` and `{player_name_censored}`.

## License & Attribution

This repository operates under the **GNU Lesser General Public License v3.0 (LGPL v3.0)**, carrying forward the open-source requirements of the original project.

You may freely use, modify, and distribute this software, provided that all modified or derivative works are also open-sourced under the LGPL v3.0 (or GPL v3.0) and these original copyright and attribution notices are preserved.
