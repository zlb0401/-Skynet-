using CardBattle.Network;
using UnityEngine;

/// <summary>
/// Auto-creates GameNetwork before the first scene loads.
/// </summary>
public static class SkynetBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureNetwork()
    {
        if (GameNetwork.Instance != null)
        {
            return;
        }

        var go = new GameObject("SkynetGameNetwork");
        go.AddComponent<GameNetwork>();
        Object.DontDestroyOnLoad(go);
    }
}
