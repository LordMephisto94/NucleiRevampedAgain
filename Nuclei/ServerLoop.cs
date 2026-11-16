using UnityEngine;

namespace Nuclei;

/// <summary>
///     Persistent object that runs Nuclei's per-frame work on the main thread. It lives on its own
///     GameObject so scene loads can't destroy it.
/// </summary>
internal class ServerLoop : MonoBehaviour
{
    internal static void Create()
    {
        var go = new GameObject("NucleiServerLoop") { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(go);
        go.AddComponent<ServerLoop>();
    }

    private void Update()
    {
        ChatCommandSetup.TryRegisterNextMission();
        ConsoleCommands.Drain();
        Messages.TickMotD();
    }
}
