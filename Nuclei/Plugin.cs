using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nuclei;

/// <summary>
///     NucleiRevamped: admin tools for the official Nuclear Option dedicated server.
///     The game itself hosts the server, rotates missions and handles bans/vote-kicks
///     (DedicatedServerConfig.json); this plugin adds staff roles, welcome/MotD messages,
///     extra chat commands and console commands for server panels such as AMP.
/// </summary>
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static readonly NucleiLog Log = new();
    internal static Plugin? Instance { get; private set; }

    private void Awake()
    {
        Instance = this;

        // The official server build runs headless; a normal game client has a graphics device.
        var headless = Application.isBatchMode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
        if (!headless)
        {
            Log.LogInfo("Not a dedicated server; NucleiRevamped stays inactive.");
            return;
        }

        try
        {
            NucleiConfig.Bind(Config);
            RoleService.Init();
            ChatCommandSetup.RegisterAll();
            ServerLoop.Create();

            if (NucleiConfig.EnableConsoleCommands.Value)
                ConsoleCommands.Start();
            if (NucleiConfig.LogChat.Value)
                ChatLog.TryApply(new Harmony(MyPluginInfo.PLUGIN_GUID));

            Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded.");
        }
        catch (Exception e)
        {
            Log.LogError($"{MyPluginInfo.PLUGIN_NAME} failed to start: {e}");
        }
    }
}

/// <summary>
///     Logs through Unity's log rather than BepInEx's. The server prints the Unity log to stdout
///     ("-logFile -"), so messages reach panel consoles like AMP even though BepInEx's own console
///     output is off by default; BepInEx still copies them into BepInEx/LogOutput.log.
/// </summary>
internal sealed class NucleiLog
{
    private const string Prefix = "[Nuclei] ";

    internal void LogInfo(string message) => Debug.Log(Prefix + message);
    internal void LogWarning(string message) => Debug.LogWarning(Prefix + message);
    internal void LogError(string message) => Debug.LogError(Prefix + message);
}
