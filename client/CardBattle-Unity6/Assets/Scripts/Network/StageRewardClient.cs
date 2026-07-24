using System;
using System.Threading.Tasks;
using CardBattle.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Claims stage clear rewards from C++ Auth (gold/dust). Idempotent per run.
/// </summary>
public static class StageRewardClient
{
    private static string _runId;
    private static readonly System.Collections.Generic.HashSet<string> ClaimedLocal = new();

    public static string CurrentRunId
    {
        get
        {
            if (string.IsNullOrEmpty(_runId))
            {
                BeginNewRun();
            }

            return _runId;
        }
    }

    public static void BeginNewRun()
    {
        _runId = Guid.NewGuid().ToString("N").Substring(0, 16);
        ClaimedLocal.Clear();
        Debug.Log("[StageReward] new run " + _runId);
    }

    public static void ClaimIfNeeded(string stageKey)
    {
        _ = ClaimAsync(stageKey);
    }

    public static async Task ClaimAsync(string stageKey)
    {
        if (string.IsNullOrEmpty(stageKey) || ClaimedLocal.Contains(stageKey))
        {
            return;
        }

        var net = GameNetwork.Instance;
        if (net == null || string.IsNullOrEmpty(net.Token))
        {
            Debug.Log("[StageReward] skip (not logged in / no token): " + stageKey);
            return;
        }

        try
        {
            var result = await AuthClient.ClaimRewardAsync(net.AuthHost, net.AuthPort, net.Token, stageKey, CurrentRunId);
            ClaimedLocal.Add(stageKey);
            if (!result.Ok)
            {
                Debug.LogWarning("[StageReward] failed: " + result.Message);
                return;
            }

            CardUpgradeCache.SetWallet(result.Gold, result.Dust, result.Diamond, result.Ticket);
            WalletHudUI.Instance?.SetBalances(result.Gold, result.Dust, result.Diamond, result.Ticket, false);

            var parts = new System.Collections.Generic.List<string>();
            if (result.GoldDelta > 0) parts.Add($"+{result.GoldDelta} 金币");
            if (result.DustDelta > 0) parts.Add($"+{result.DustDelta} 粉尘");
            if (result.DiamondDelta > 0) parts.Add($"+{result.DiamondDelta} 钻石");
            if (result.TicketDelta > 0) parts.Add($"+{result.TicketDelta} 招募券");
            if (parts.Count > 0)
            {
                ShowToast("通关奖励 " + string.Join("  ", parts));
            }

            Debug.Log($"[StageReward] {stageKey} +{result.GoldDelta}g +{result.DustDelta}d +{result.DiamondDelta}💎 +{result.TicketDelta}券");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[StageReward] " + ex.Message);
        }
    }

    private static void ShowToast(string msg)
    {
        var existing = GameObject.Find("StageRewardToast");
        if (existing != null)
        {
            UnityEngine.Object.Destroy(existing);
        }

        var go = new GameObject("StageRewardToast");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 6000;
        go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        go.AddComponent<GraphicRaycaster>();

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var rt = textGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.75f);
        rt.anchorMax = new Vector2(0.5f, 0.75f);
        rt.sizeDelta = new Vector2(700f, 60f);
        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = msg;
        text.fontSize = 30;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 0.92f, 0.55f, 1f);
        ChineseFontBootstrap.ApplyChineseFont(text);
        UnityEngine.Object.Destroy(go, 3.5f);
    }
}
