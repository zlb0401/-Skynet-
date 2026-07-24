using MyProjectF.Assets.Scripts.Cards;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RewardSceneController : MonoBehaviour
{
    [Header("Setup")]
    public RewardPool pool;
    public Transform cardParent;
    public RewardCardView cardPrefab;
    public Button continueButton;
    public TMP_Text headerText;

    private readonly List<RewardCardView> spawned = new();
    private bool choiceMade;
    private bool continueArmed;

    [SerializeField] private RectTransform selectedAnchor;
    [SerializeField] private float disappearDuration = 0.25f;
    [SerializeField] private float moveDuration = 0.35f;
    [SerializeField] private float zoomScale = 1.15f;

    private void Start()
    {
        Time.timeScale = 1f;
        continueArmed = false;

        try { WalletHudUI.Instance?.SetVisible(false); } catch { /* ignore */ }

        if (headerText)
        {
            headerText.text = "选择一张卡";
            try { ChineseFontBootstrap.ApplyChineseFont(headerText); } catch { /* ignore */ }
        }

        if (cardParent)
        {
            cardParent.gameObject.SetActive(true);
            cardParent.localScale = Vector3.one;
        }

        HideContinueButton();
        SpawnCards();

        try
        {
            StageRewardClient.ClaimIfNeeded("battle1_clear");
        }
        catch (System.Exception ex)
        {
            Logger.LogWarning("[Reward] claim currency failed: " + ex.Message);
        }
    }

    private void HideContinueButton()
    {
        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Build or repair a bottom-center Chinese「继续」button, then show it.
    /// Never throws — selection flow must always be able to proceed.
    /// </summary>
    private void ShowContinueButton()
    {
        try
        {
            EnsureRuntimeContinueButton();
            if (continueButton == null)
            {
                Logger.LogError("[Reward] continueButton still null after EnsureRuntime.");
                return;
            }

            // Activate first so layout/canvas updates even if styling fails.
            continueButton.gameObject.SetActive(true);
            continueButton.interactable = true;

            var rt = continueButton.transform as RectTransform;
            if (rt != null)
            {
                rt.localScale = Vector3.one;
                // Bottom-center, always on screen for 16:9 and similar.
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, 120f);
                rt.sizeDelta = new Vector2(280f, 88f);
            }

            try { StyleContinueButton(); } catch (System.Exception ex)
            {
                Logger.LogWarning("[Reward] style continue failed: " + ex.Message);
            }

            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(GoNext);
            continueButton.transform.SetAsLastSibling();
            continueArmed = true;
            Logger.Log("[Reward] continue button shown.");
        }
        catch (System.Exception ex)
        {
            Logger.LogError("[Reward] ShowContinueButton failed: " + ex);
        }
    }

    private void EnsureRuntimeContinueButton()
    {
        if (continueButton == null)
        {
            continueButton = GameObject.Find("ContinueButton")?.GetComponent<Button>();
        }

        if (continueButton != null)
        {
            return;
        }

        var canvas = GetComponentInParent<Canvas>() ?? FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            var canvasGo = new GameObject("RewardCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }

        var go = new GameObject("ContinueButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(canvas.transform, false);
        continueButton = go.GetComponent<Button>();
        var img = go.GetComponent<Image>();
        img.color = new Color(0.12f, 0.16f, 0.22f, 0.95f);
        continueButton.targetGraphic = img;
    }

    private void StyleContinueButton()
    {
        // Hide leftover EN texts/sprites labels on the scene button.
        foreach (var t in continueButton.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t.gameObject.name != "ContinueLabel")
            {
                t.gameObject.SetActive(false);
            }
        }

        foreach (var t in continueButton.GetComponentsInChildren<Text>(true))
        {
            t.gameObject.SetActive(false);
        }

        var img = continueButton.GetComponent<Image>();
        if (img != null)
        {
            img.color = new Color(0.12f, 0.16f, 0.22f, 0.95f);
            img.raycastTarget = true;
            // Drop English plate sprite so only solid + Chinese text show.
            img.sprite = null;
            img.type = Image.Type.Simple;
        }

        var labelGo = continueButton.transform.Find("ContinueLabel");
        TMP_Text label;
        if (labelGo == null)
        {
            var go = new GameObject("ContinueLabel", typeof(RectTransform));
            labelGo = go.transform;
            labelGo.SetParent(continueButton.transform, false);
            var lrt = go.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            label = go.AddComponent<TextMeshProUGUI>();
        }
        else
        {
            label = labelGo.GetComponent<TMP_Text>() ?? labelGo.gameObject.AddComponent<TextMeshProUGUI>();
            labelGo.gameObject.SetActive(true);
        }

        label.text = "继续";
        label.fontSize = 42;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        try { ChineseFontBootstrap.ApplyChineseFont(label); } catch { /* ignore */ }
    }

    private void SpawnCards()
    {
        ClearSpawned();

        if (pool == null || cardPrefab == null || cardParent == null)
        {
            Logger.LogError("[Reward] Missing pool/cardPrefab/cardParent — cannot spawn cards.");
            if (headerText)
            {
                headerText.text = "奖励配置缺失，请返回主菜单";
            }

            ShowContinueButton();
            return;
        }

        if (pool.candidates == null || pool.candidates.Count == 0)
        {
            pool.PopulateFromDatabase();
        }

        int seed = System.Environment.TickCount;
        var defs = pool.RollCardChoices(3, seed);
        if (defs == null || defs.Count == 0)
        {
            Logger.LogError("[Reward] RollCardChoices returned empty. candidates=" + (pool.candidates?.Count ?? 0));
            if (headerText)
            {
                headerText.text = "暂无可选卡牌";
            }

            ShowContinueButton();
            return;
        }

        foreach (var d in defs)
        {
            try
            {
                var card = Instantiate(cardPrefab, cardParent);
                card.Setup(d, OnCardChosen);
                spawned.Add(card);
            }
            catch (System.Exception ex)
            {
                Logger.LogError("[Reward] spawn card failed: " + ex);
            }
        }

        var parentRT = cardParent as RectTransform;
        if (parentRT != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRT);
        }

        Canvas.ForceUpdateCanvases();
        Logger.Log($"[Reward] spawned={spawned.Count} candidates={pool.candidates?.Count}");
    }

    private void ClearSpawned()
    {
        foreach (var c in spawned)
        {
            if (c)
            {
                Destroy(c.gameObject);
            }
        }

        spawned.Clear();
        choiceMade = false;
    }

    private void OnCardChosen(RewardCardView chosen)
    {
        if (choiceMade)
        {
            return;
        }

        choiceMade = true;

        foreach (var c in spawned)
        {
            c.Interactable(false);
        }

        foreach (var c in spawned)
        {
            if (c != chosen && c != null)
            {
                StartCoroutine(FadeAndDestroy(c.gameObject, disappearDuration));
            }
        }

        StartCoroutine(AnimateChosenToCenter(chosen));

        string cardName = chosen?.def?.cardData ? chosen.def.cardData.GetDisplayName() : null;
        try
        {
            ApplyCard(chosen?.def?.cardData ? chosen.def.cardData.cardName : null);
        }
        catch (System.Exception ex)
        {
            Logger.LogWarning("[Reward] ApplyCard failed: " + ex.Message);
        }

        if (headerText)
        {
            headerText.text = string.IsNullOrEmpty(cardName) ? "已加入卡组" : $"{cardName} 已加入卡组";
            try { ChineseFontBootstrap.ApplyChineseFont(headerText); } catch { /* ignore */ }
        }

        // Always arm continue after choice (do not wait on animation).
        ShowContinueButton();
    }

    private System.Collections.IEnumerator FadeAndDestroy(GameObject go, float duration)
    {
        if (!go)
        {
            yield break;
        }

        var cg = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
        float t = 0f;
        float start = cg.alpha;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(start, 0f, t / duration);
            yield return null;
        }

        cg.alpha = 0f;
        Destroy(go);
    }

    private System.Collections.IEnumerator AnimateChosenToCenter(RewardCardView chosen)
    {
        if (chosen == null)
        {
            yield break;
        }

        var h = cardParent.GetComponent<HorizontalLayoutGroup>();
        var g = cardParent.GetComponent<GridLayoutGroup>();
        bool hadH = h && h.enabled;
        bool hadG = g && g.enabled;
        if (h)
        {
            h.enabled = false;
        }

        if (g)
        {
            g.enabled = false;
        }

        var rt = chosen.transform as RectTransform;
        var cg = chosen.GetComponent<CanvasGroup>() ?? chosen.gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;

        RectTransform rootCanvasRt = null;
        var canvas = GetComponentInParent<Canvas>() ?? FindAnyObjectByType<Canvas>();
        if (canvas)
        {
            rootCanvasRt = canvas.transform as RectTransform;
        }

        Vector3 startPos = rt.position;
        Vector3 startScale = rt.localScale;
        Vector3 targetPos;
        if (selectedAnchor != null)
        {
            rt.SetParent(selectedAnchor.parent, worldPositionStays: true);
            targetPos = selectedAnchor.position;
        }
        else if (rootCanvasRt != null)
        {
            rt.SetParent(rootCanvasRt, worldPositionStays: true);
            targetPos = rootCanvasRt.TransformPoint(rootCanvasRt.rect.center);
        }
        else
        {
            targetPos = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        }

        float t = 0f;
        while (t < moveDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / moveDuration));
            rt.position = Vector3.Lerp(startPos, targetPos, k);
            rt.localScale = Vector3.Lerp(startScale, Vector3.one * zoomScale, k);
            yield return null;
        }

        rt.position = targetPos;
        rt.localScale = Vector3.one * zoomScale;

        if (h && hadH)
        {
            h.enabled = true;
        }

        if (g && hadG)
        {
            g.enabled = true;
        }

        // Re-assert continue on top after parenting moves.
        if (continueArmed)
        {
            ShowContinueButton();
        }
    }

    private void ApplyCard(string cardName)
    {
        if (string.IsNullOrEmpty(cardName))
        {
            Logger.LogWarning("[Reward] Invalid cardName.");
            return;
        }

        var deck = PlayerDeck.Instance ?? FindAnyObjectByType<PlayerDeck>();
        if (deck == null)
        {
            Logger.LogError("[Reward] PlayerDeck not found.");
            return;
        }

        deck.AddCardToDeck(cardName);
    }

    private void GoNext()
    {
        Logger.Log("[Reward] GoNext -> BattleBoss1");
        var sf = SceneFlowManager.Instance ?? FindAnyObjectByType<SceneFlowManager>();
        if (sf != null)
        {
            sf.LoadNextAfterBattle();
            return;
        }

        Logger.LogWarning("[Reward] SceneFlowManager not found, loading BattleBoss1 directly.");
        UnityEngine.SceneManagement.SceneManager.LoadScene(SceneType.BattleBoss1.ToString());
    }
}
