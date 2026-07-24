using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardBattle.Network;
using MyProjectF.Assets.Scripts.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 抽卡面板：日常卡池 / 宝箱池，单抽与十连并排展示。
/// </summary>
public class GachaPanelUI : MonoBehaviour
{
    public static GachaPanelUI Instance { get; private set; }

    private const int SingleDiamondCost = 160;
    private const int TenDiamondCost = 1600;
    private const int SingleChestGoldCost = 100;
    private const int TenChestGoldCost = 1000;

    private enum PoolKind { Daily, Chest }

    private GameObject _root;
    private Transform _mainArea;
    private Transform _stage;
    private Transform _multiGrid;
    private TMP_Text _diamondCount;
    private TMP_Text _ticketCount;
    private TMP_Text _goldCount;
    private GameObject _dailyWalletRow;
    private GameObject _chestWalletRow;
    private TMP_Text _resultLine;
    private TMP_Text _statusLine;
    private Image _cardThumb;
    private Image _flashImage;
    private RectTransform _thumbRt;
    private TMP_Text _singleBtnLabel;
    private TMP_Text _tenBtnLabel;
    private readonly List<Image> _navBgs = new();
    private readonly List<TMP_Text> _navLabels = new();
    private readonly List<GameObject> _navAccents = new();
    private Dictionary<string, Card> _cardByKey;
    private PoolKind _pool = PoolKind.Daily;
    private bool _busy;
    private bool _revealing;
    private bool _skipReveal;
    private bool? _queuedIsTen;
    private Coroutine _revealCo;

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Open()
    {
        EnsureUi();
        SelectPool(PoolKind.Daily);
        ResetStage();
        RefreshWalletChips();
        RefreshPullButtons();
        SetStatus("");
        _root.SetActive(true);
        _ = PrefetchAsync();
    }

    public void Close()
    {
        if (_revealCo != null)
        {
            StopCoroutine(_revealCo);
            _revealCo = null;
        }

        if (_root != null) _root.SetActive(false);
    }

    private void EnsureUi()
    {
        if (_root != null) return;

        _root = new GameObject("GachaPanel");
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5600;
        var scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        _root.AddComponent<GraphicRaycaster>();

        var panel = MakePanel(_root.transform, "Panel", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0f, 0f, 0f, 0f));

        var bgRt = MakePanel(panel, "Background", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0.06f, 0.08f, 0.11f, 1f));
        ApplyGachaBackground(bgRt.GetComponent<Image>());

        const float sidebarWidth = 240f;
        var sidebar = MakePanel(panel, "Sidebar", new Vector2(0f, 0f), new Vector2(0f, 1f),
            Vector2.zero, Vector2.zero, new Color(0.05f, 0.07f, 0.10f, 1f));
        sidebar.pivot = new Vector2(0f, 0.5f);
        sidebar.offsetMin = new Vector2(0f, 0f);
        sidebar.offsetMax = new Vector2(sidebarWidth, 0f);

        MakeSidebarBackButton(sidebar, Close);

        var navList = new GameObject("NavList", typeof(RectTransform));
        navList.transform.SetParent(sidebar, false);
        var navRt = navList.GetComponent<RectTransform>();
        navRt.anchorMin = new Vector2(0f, 1f);
        navRt.anchorMax = new Vector2(1f, 1f);
        navRt.pivot = new Vector2(0.5f, 1f);
        navRt.anchoredPosition = new Vector2(0f, -80f);
        navRt.sizeDelta = new Vector2(0f, 480f);
        var navVlg = navList.AddComponent<VerticalLayoutGroup>();
        navVlg.spacing = 8f;
        navVlg.padding = new RectOffset(8, 8, 8, 8);
        navVlg.childAlignment = TextAnchor.UpperCenter;
        navVlg.childControlWidth = true;
        navVlg.childControlHeight = true;
        navVlg.childForceExpandWidth = true;
        navVlg.childForceExpandHeight = false;

        MakeNavButton(navList.transform, "日常卡池", PoolKind.Daily);
        MakeNavButton(navList.transform, "宝箱池", PoolKind.Chest);

        _mainArea = MakePanel(panel, "MainArea", new Vector2(0f, 0f), new Vector2(1f, 1f),
            Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f));
        (_mainArea as RectTransform).offsetMin = new Vector2(sidebarWidth, 0f);
        (_mainArea as RectTransform).offsetMax = Vector2.zero;

        _dailyWalletRow = CreateWalletRow(_mainArea, "DailyWallet", out _diamondCount, out _ticketCount, true);
        _chestWalletRow = CreateWalletRow(_mainArea, "ChestWallet", out _goldCount, out _, false);
        _chestWalletRow.SetActive(false);

        _stage = MakePanel(_mainArea, "Stage", new Vector2(0f, 0f), new Vector2(1f, 1f),
            Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f));
        var stageRt = _stage as RectTransform;
        stageRt.offsetMin = new Vector2(12f, 168f);
        stageRt.offsetMax = new Vector2(-12f, -72f);

        // Blank-area click: skip reveal / clear stage
        var skipGo = new GameObject("SkipCatcher", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        skipGo.transform.SetParent(_stage, false);
        var skipRt = skipGo.GetComponent<RectTransform>();
        skipRt.anchorMin = Vector2.zero;
        skipRt.anchorMax = Vector2.one;
        skipRt.offsetMin = skipRt.offsetMax = Vector2.zero;
        var skipImg = skipGo.GetComponent<Image>();
        skipImg.color = new Color(1f, 1f, 1f, 0.001f);
        skipImg.raycastTarget = true;
        var skipBtn = skipGo.GetComponent<Button>();
        skipBtn.transition = Selectable.Transition.None;
        skipBtn.onClick.AddListener(OnStageClicked);

        _flashImage = MakePanel(_stage, "Flash", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            Color.white).GetComponent<Image>();
        _flashImage.raycastTarget = false;
        _flashImage.color = new Color(1f, 1f, 1f, 0f);

        var thumbGo = new GameObject("CardThumb", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        thumbGo.transform.SetParent(_stage, false);
        _thumbRt = thumbGo.GetComponent<RectTransform>();
        _thumbRt.anchorMin = _thumbRt.anchorMax = new Vector2(0.5f, 0.5f);
        _thumbRt.anchoredPosition = new Vector2(0f, 30f);
        _thumbRt.sizeDelta = new Vector2(280f, 340f);
        _cardThumb = thumbGo.GetComponent<Image>();
        _cardThumb.preserveAspect = false;
        _cardThumb.raycastTarget = false;
        thumbGo.SetActive(false);

        var multiGo = new GameObject("MultiGrid", typeof(RectTransform));
        multiGo.transform.SetParent(_stage, false);
        _multiGrid = multiGo.transform;
        var mrt = multiGo.GetComponent<RectTransform>();
        mrt.anchorMin = new Vector2(0.5f, 0.5f);
        mrt.anchorMax = new Vector2(0.5f, 0.5f);
        mrt.anchoredPosition = new Vector2(0f, 40f);
        mrt.sizeDelta = new Vector2(1400f, 520f);
        multiGo.SetActive(false);

        _resultLine = MakeText(_stage, "ResultLine", "", 26, new Vector2(0f, -250f), new Vector2(900f, 44f));
        _resultLine.alignment = TextAlignmentOptions.Center;
        _resultLine.color = new Color(1f, 0.92f, 0.62f, 1f);

        _statusLine = MakeText(_stage, "StatusLine", "", 22, new Vector2(0f, -290f), new Vector2(900f, 36f));
        _statusLine.alignment = TextAlignmentOptions.Center;
        _statusLine.color = new Color(0.75f, 0.80f, 0.88f, 1f);

        const float pullBtnY = 130f;
        var singleBtn = MakePullButton(_mainArea, "SingleBtn", new Vector2(-160f, pullBtnY), new Vector2(280f, 64f),
            () => _ = DoGacha(false));
        var singleRt = singleBtn.GetComponent<RectTransform>();
        singleRt.anchorMin = singleRt.anchorMax = new Vector2(0.5f, 0f);
        _singleBtnLabel = singleBtn.GetComponentInChildren<TMP_Text>();

        var tenBtn = MakePullButton(_mainArea, "TenBtn", new Vector2(160f, pullBtnY), new Vector2(280f, 64f),
            () => _ = DoGacha(true));
        var tenRt = tenBtn.GetComponent<RectTransform>();
        tenRt.anchorMin = tenRt.anchorMax = new Vector2(0.5f, 0f);
        _tenBtnLabel = tenBtn.GetComponentInChildren<TMP_Text>();

        _root.SetActive(false);
        BuildCardLookup();
    }

    private GameObject CreateWalletRow(Transform parent, string name, out TMP_Text primary, out TMP_Text secondary,
        bool daily)
    {
        var row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var wrt = row.GetComponent<RectTransform>();
        wrt.anchorMin = wrt.anchorMax = new Vector2(1f, 1f);
        wrt.pivot = new Vector2(1f, 1f);
        wrt.anchoredPosition = new Vector2(-24f, -16f);
        wrt.sizeDelta = new Vector2(320f, 56f);
        var whlg = row.AddComponent<HorizontalLayoutGroup>();
        whlg.spacing = 12f;
        whlg.childAlignment = TextAnchor.MiddleRight;
        whlg.childControlWidth = false;
        whlg.childControlHeight = false;
        whlg.childForceExpandWidth = false;
        whlg.childForceExpandHeight = false;

        if (daily)
        {
            primary = CreateWalletChip(row.transform, "DiamondChip", "UI/icon_diamond",
                new Color(0.45f, 0.75f, 1f, 1f));
            secondary = CreateWalletChip(row.transform, "TicketChip", "UI/icon_ticket",
                new Color(0.95f, 0.55f, 0.35f, 1f));
        }
        else
        {
            primary = CreateWalletChip(row.transform, "GoldChip", "UI/icon_gold",
                new Color(0.95f, 0.82f, 0.35f, 1f));
            secondary = null;
        }

        return row;
    }

    private void BuildCardLookup()
    {
        _cardByKey = new Dictionary<string, Card>();
        foreach (var c in Resources.LoadAll<Card>("Cards"))
        {
            if (c != null && !string.IsNullOrEmpty(c.cardName))
                _cardByKey[c.cardName] = c;
        }
    }

    private void SelectPool(PoolKind pool)
    {
        _pool = pool;
        for (var i = 0; i < _navBgs.Count; i++)
        {
            var selected = (i == 0 && pool == PoolKind.Daily) || (i == 1 && pool == PoolKind.Chest);
            _navBgs[i].color = selected
                ? new Color(0.22f, 0.26f, 0.32f, 1f)
                : new Color(0.10f, 0.12f, 0.16f, 0.6f);
            if (i < _navLabels.Count)
                _navLabels[i].color = selected
                    ? new Color(0.95f, 0.82f, 0.48f, 1f)
                    : new Color(0.72f, 0.76f, 0.82f, 1f);
            if (i < _navAccents.Count && _navAccents[i] != null)
                _navAccents[i].SetActive(selected);
        }

        if (_dailyWalletRow != null) _dailyWalletRow.SetActive(pool == PoolKind.Daily);
        if (_chestWalletRow != null) _chestWalletRow.SetActive(pool == PoolKind.Chest);
        ResetStage();
        RefreshPullButtons();
        SetResultLine("");
        SetStatus("");
    }

    private void ResetStage()
    {
        if (_cardThumb != null)
        {
            _cardThumb.sprite = null;
            _cardThumb.gameObject.SetActive(false);
        }

        if (_thumbRt != null) _thumbRt.localScale = Vector3.one;
        if (_flashImage != null) _flashImage.color = new Color(1f, 1f, 1f, 0f);
        ClearMultiGrid();
        if (_multiGrid != null) _multiGrid.gameObject.SetActive(false);
        if (_resultLine != null) _resultLine.text = "";
        if (_statusLine != null) _statusLine.text = "";
    }

    private void ClearMultiGrid()
    {
        if (_multiGrid == null) return;
        for (var i = _multiGrid.childCount - 1; i >= 0; i--)
            Object.Destroy(_multiGrid.GetChild(i).gameObject);
    }

    private async Task PrefetchAsync()
    {
        var net = GameNetwork.Instance;
        if (net == null || string.IsNullOrEmpty(net.Token)) return;
        try
        {
            var inv = await AuthClient.FetchInventoryAsync(net.AuthHost, net.AuthPort, net.Token);
            if (!inv.Ok) return;
            CardUpgradeCache.SetWallet(inv.Gold, inv.Dust, inv.Diamond, inv.Ticket);
            WalletHudUI.Instance?.SetBalances(inv.Gold, inv.Dust, inv.Diamond, inv.Ticket, true);
            RefreshWalletChips();
            RefreshPullButtons();
        }
        catch (System.Exception ex) { Debug.LogWarning("[Gacha] prefetch: " + ex.Message); }
    }

    private void RefreshWalletChips()
    {
        if (_diamondCount != null) _diamondCount.text = CardUpgradeCache.LastDiamond.ToString();
        if (_ticketCount != null) _ticketCount.text = CardUpgradeCache.LastTicket.ToString();
        if (_goldCount != null) _goldCount.text = CardUpgradeCache.LastGold.ToString();
        if (_diamondCount != null) ChineseFontBootstrap.ApplyChineseFont(_diamondCount);
        if (_ticketCount != null) ChineseFontBootstrap.ApplyChineseFont(_ticketCount);
        if (_goldCount != null) ChineseFontBootstrap.ApplyChineseFont(_goldCount);
    }

    private void RefreshPullButtons()
    {
        if (_singleBtnLabel == null || _tenBtnLabel == null) return;

        if (_pool == PoolKind.Chest)
        {
            _singleBtnLabel.text = $"单抽\n消耗{SingleChestGoldCost}金币";
            _tenBtnLabel.text = $"十连抽\n消耗{TenChestGoldCost}金币";
        }
        else
        {
            var ticket = CardUpgradeCache.LastTicket;
            if (ticket <= 0)
            {
                _singleBtnLabel.text = $"单抽\n消耗{SingleDiamondCost}钻石";
                _tenBtnLabel.text = $"十连抽\n消耗{TenDiamondCost}钻石";
            }
            else if (ticket < 10)
            {
                _singleBtnLabel.text = "单抽\n消耗1招募券";
                _tenBtnLabel.text = $"十连抽\n消耗{TenDiamondCost}钻石";
            }
            else
            {
                _singleBtnLabel.text = "单抽\n消耗1招募券";
                _tenBtnLabel.text = "十连抽\n消耗10招募券";
            }
        }

        ChineseFontBootstrap.ApplyChineseFont(_singleBtnLabel);
        ChineseFontBootstrap.ApplyChineseFont(_tenBtnLabel);
    }

    private (byte payType, byte count) ResolvePullParams(bool isTenPull)
    {
        if (_pool == PoolKind.Chest)
            return (2, (byte)(isTenPull ? 10 : 1));

        var ticket = CardUpgradeCache.LastTicket;
        if (isTenPull)
        {
            if (ticket >= 10) return (0, 10);
            return (1, 10);
        }

        if (ticket >= 1) return (0, 1);
        return (1, 1);
    }

    private void OnStageClicked()
    {
        if (_revealing)
        {
            _skipReveal = true;
            return;
        }

        // Idle click refreshes / clears previous result display.
        ResetStage();
        SetResultLine("");
        SetStatus("");
    }

    private async Task DoGacha(bool isTen)
    {
        // While revealing: skip to full result, then queue this new pull.
        if (_revealing)
        {
            _skipReveal = true;
            _queuedIsTen = isTen;
            return;
        }

        if (_busy) return;
        var net = GameNetwork.Instance;
        if (net == null || string.IsNullOrEmpty(net.Token))
        {
            SetStatus("请先登录");
            return;
        }

        var (payType, count) = ResolvePullParams(isTen);

        _busy = true;
        SetStatus("抽取中…");
        try
        {
            var result = await AuthClient.GachaAsync(net.AuthHost, net.AuthPort, net.Token, payType, count);
            if (!result.Ok)
            {
                SetStatus(Translate(result.Message));
                return;
            }

            // Rewards already applied server-side; sync wallet/owned regardless of animation.
            CardUpgradeCache.SetWallet(result.Gold, result.Dust, result.Diamond, result.Ticket);
            foreach (var item in result.Items)
            {
                if (item.IsNew && !string.IsNullOrEmpty(item.CardKey) && !IsSpecialKey(item.CardKey))
                    CardUpgradeCache.AddOwned(item.CardKey);
            }

            WalletHudUI.Instance?.SetBalances(result.Gold, result.Dust, result.Diamond, result.Ticket, true);
            RefreshWalletChips();
            RefreshPullButtons();

            if (_revealCo != null) StopCoroutine(_revealCo);
            _revealCo = StartCoroutine(PlayRevealSequence(result.Items, isTen));
        }
        catch (System.Exception ex) { SetStatus(ex.Message); }
        finally { _busy = false; }
    }

    private static bool IsSpecialKey(string key) =>
        key == "__GOLD__" || key == "__CHEST_1__" || key == "__CHEST_2__" || key == "__CHEST_3__";

    private IEnumerator PlayRevealSequence(GachaPullItem[] items, bool isTenPull)
    {
        _revealing = true;
        _skipReveal = false;

        if (items == null || items.Length == 0)
        {
            SetStatus("抽卡成功");
            _revealing = false;
            _revealCo = null;
            yield return FinishRevealAndMaybeQueue();
            yield break;
        }

        ClearMultiGrid();
        if (_cardThumb != null) _cardThumb.gameObject.SetActive(false);

        if (isTenPull && items.Length > 1)
        {
            if (_multiGrid != null) _multiGrid.gameObject.SetActive(true);
            var slots = new Image[items.Length];
            for (var i = 0; i < items.Length; i++)
                slots[i] = CreateMultiSlot(i, items.Length);

            var newCount = 0;
            var totalDust = 0;
            var goldSum = 0;
            var chestCount = 0;
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i].IsNew) newCount++;
                totalDust += items[i].DustGained;
                if (items[i].CardKey == "__GOLD__") goldSum += items[i].DustGained;
                else if (IsSpecialKey(items[i].CardKey)) chestCount++;
            }

            for (var i = 0; i < items.Length; i++)
            {
                if (_skipReveal)
                {
                    for (var j = i; j < items.Length; j++)
                        ShowSlotImmediate(slots[j], items[j]);
                    break;
                }

                ApplyItemVisual(slots[i], items[i], out _);
                SetResultLine(DescribeItem(items[i]));
                yield return AnimateSlotIn(slots[i]);
                if (!_skipReveal && i < items.Length - 1)
                    yield return WaitOrSkip(0.18f);
            }

            if (_pool == PoolKind.Chest)
                SetResultLine($"十连完成：金币奖励合计+{goldSum} · 宝箱×{chestCount}");
            else
                SetResultLine($"十连完成：新卡{newCount} · 粉尘+{totalDust}");
            SetStatus("");
        }
        else
        {
            if (_multiGrid != null) _multiGrid.gameObject.SetActive(false);
            yield return PlaySingleReveal(items[0]);
            SetStatus("抽卡成功");
        }

        _revealing = false;
        _revealCo = null;
        yield return FinishRevealAndMaybeQueue();
    }

    private IEnumerator FinishRevealAndMaybeQueue()
    {
        if (!_queuedIsTen.HasValue) yield break;
        var next = _queuedIsTen.Value;
        _queuedIsTen = null;
        // Small gap so skipped result is visible briefly.
        yield return new WaitForSeconds(0.15f);
        _ = DoGacha(next);
    }

    private IEnumerator WaitOrSkip(float sec)
    {
        var t = 0f;
        while (t < sec && !_skipReveal)
        {
            t += Time.deltaTime;
            yield return null;
        }
    }

    private void ShowSlotImmediate(Image img, GachaPullItem item)
    {
        if (img == null) return;
        ApplyItemVisual(img, item, out _);
        var c = img.color;
        c.a = 1f;
        img.color = c;
        img.rectTransform.localScale = Vector3.one;
    }

    private Image CreateMultiSlot(int index, int total)
    {
        const int cols = 5;
        var row = index / cols;
        var col = index % cols;
        var rows = (total + cols - 1) / cols;
        // Uniform portrait slots (same outer size for every card).
        const float slotW = 148f;
        const float slotH = 210f;
        const float gapX = 18f;
        const float gapY = 16f;
        var gridW = cols * slotW + (cols - 1) * gapX;
        var gridH = rows * slotH + (rows - 1) * gapY;
        var startX = -gridW * 0.5f + slotW * 0.5f;
        var startY = gridH * 0.5f - slotH * 0.5f;

        var frame = new GameObject("Slot_" + index, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        frame.transform.SetParent(_multiGrid, false);
        var frt = frame.GetComponent<RectTransform>();
        frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f);
        frt.sizeDelta = new Vector2(slotW, slotH);
        frt.anchoredPosition = new Vector2(startX + col * (slotW + gapX), startY - row * (slotH + gapY));
        var fimg = frame.GetComponent<Image>();
        fimg.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
        fimg.raycastTarget = false;

        var maskGo = new GameObject("Mask", typeof(RectTransform), typeof(RectMask2D));
        maskGo.transform.SetParent(frame.transform, false);
        var mrt = maskGo.GetComponent<RectTransform>();
        mrt.anchorMin = Vector2.zero;
        mrt.anchorMax = Vector2.one;
        mrt.offsetMin = new Vector2(4f, 4f);
        mrt.offsetMax = new Vector2(-4f, -4f);

        var artGo = new GameObject("Art", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        artGo.transform.SetParent(maskGo.transform, false);
        var artRt = artGo.GetComponent<RectTransform>();
        artRt.anchorMin = Vector2.zero;
        artRt.anchorMax = Vector2.one;
        artRt.offsetMin = artRt.offsetMax = Vector2.zero;
        var img = artGo.GetComponent<Image>();
        // Stretch to fill the fixed frame so every slot is the same visual size.
        img.preserveAspect = false;
        img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, 0f);
        return img;
    }

    private IEnumerator AnimateSlotIn(Image img)
    {
        if (img == null) yield break;
        if (_skipReveal)
        {
            var c0 = img.color; c0.a = 1f; img.color = c0;
            img.rectTransform.localScale = Vector3.one;
            yield break;
        }

        var rt = img.rectTransform;
        rt.localScale = Vector3.one * 0.4f;
        const float dur = 0.28f;
        var t = 0f;
        while (t < dur && !_skipReveal)
        {
            t += Time.deltaTime;
            var p = Mathf.Clamp01(t / dur);
            rt.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, p);
            var c = img.color;
            c.a = Mathf.Clamp01(p * 1.3f);
            img.color = c;
            yield return null;
        }

        rt.localScale = Vector3.one;
        var final = img.color;
        final.a = 1f;
        img.color = final;
    }

    private IEnumerator PlaySingleReveal(GachaPullItem item)
    {
        SetResultLine(DescribeItem(item));
        if (_cardThumb == null) yield break;

        _cardThumb.gameObject.SetActive(true);
        ApplyItemVisual(_cardThumb, item, out _);
        if (_skipReveal)
        {
            _cardThumb.color = Color.white;
            if (_thumbRt != null) _thumbRt.localScale = Vector3.one;
            yield break;
        }

        _cardThumb.color = new Color(1f, 1f, 1f, 0f);
        if (_thumbRt != null) _thumbRt.localScale = Vector3.one * 0.3f;
        if (_flashImage != null) _flashImage.color = new Color(1f, 1f, 1f, 0f);

        const float flashPeak = 0.55f;
        const float dur = 0.45f;
        var t = 0f;
        while (t < dur && !_skipReveal)
        {
            t += Time.deltaTime;
            var p = Mathf.Clamp01(t / dur);

            if (_thumbRt != null)
            {
                var scale = p < 0.65f
                    ? Mathf.Lerp(0.3f, 1.1f, p / 0.65f)
                    : Mathf.Lerp(1.1f, 1f, (p - 0.65f) / 0.35f);
                _thumbRt.localScale = Vector3.one * scale;
            }

            _cardThumb.color = new Color(1f, 1f, 1f, Mathf.Clamp01(p * 1.4f));

            if (_flashImage != null)
            {
                var flashA = p < 0.25f ? Mathf.Lerp(0f, flashPeak, p / 0.25f)
                    : p < 0.55f ? Mathf.Lerp(flashPeak, 0f, (p - 0.25f) / 0.3f)
                    : 0f;
                _flashImage.color = new Color(1f, 0.95f, 0.82f, flashA);
            }

            yield return null;
        }

        if (_thumbRt != null) _thumbRt.localScale = Vector3.one;
        _cardThumb.color = Color.white;
        if (_flashImage != null) _flashImage.color = new Color(1f, 1f, 1f, 0f);
    }

    private void ApplyItemVisual(Image img, GachaPullItem item, out string unused)
    {
        unused = null;
        if (img == null) return;

        if (item.CardKey == "__GOLD__")
        {
            var tex = Resources.Load<Texture2D>("UI/icon_gold");
            if (tex != null)
            {
                img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                img.color = new Color(1f, 1f, 1f, img.color.a);
            }
            else
            {
                img.sprite = null;
                img.color = new Color(0.95f, 0.82f, 0.35f, img.color.a);
            }
            return;
        }

        if (item.CardKey == "__CHEST_1__" || item.CardKey == "__CHEST_2__" || item.CardKey == "__CHEST_3__")
        {
            ushort id = item.CardKey == "__CHEST_1__" ? (ushort)1 : item.CardKey == "__CHEST_2__" ? (ushort)2 : (ushort)3;
            var sp = ItemCatalog.LoadIcon(id);
            if (sp != null)
            {
                img.sprite = sp;
                img.color = new Color(1f, 1f, 1f, img.color.a);
            }
            else
            {
                img.sprite = null;
                img.color = new Color(0.55f, 0.45f, 0.3f, img.color.a);
            }
            return;
        }

        var card = LookupCard(item.CardKey);
        if (card != null && card.cardSprite != null)
        {
            img.sprite = card.cardSprite;
            img.color = new Color(1f, 1f, 1f, img.color.a);
        }
        else
        {
            img.sprite = null;
            img.color = new Color(0.22f, 0.28f, 0.36f, img.color.a);
        }
    }

    private string DescribeItem(GachaPullItem item)
    {
        if (item.CardKey == "__GOLD__")
            return $"获得金币 +{item.DustGained}";
        if (item.CardKey == "__CHEST_1__") return "获得木宝箱 ×1";
        if (item.CardKey == "__CHEST_2__") return "获得铁宝箱 ×1";
        if (item.CardKey == "__CHEST_3__") return "获得金宝箱 ×1";

        var card = LookupCard(item.CardKey);
        var displayName = card != null ? card.GetDisplayName() : item.CardKey;
        return item.IsNew
            ? $"获得新卡：{displayName}"
            : $"已拥有，转化为 {item.DustGained} 粉尘";
    }

    private Card LookupCard(string key)
    {
        if (string.IsNullOrEmpty(key) || IsSpecialKey(key)) return null;
        if (_cardByKey != null && _cardByKey.TryGetValue(key, out var c)) return c;
        foreach (var card in Resources.LoadAll<Card>("Cards"))
        {
            if (card != null && card.cardName == key) return card;
        }

        return null;
    }

    private void SetResultLine(string msg)
    {
        if (_resultLine == null) return;
        _resultLine.text = msg ?? string.Empty;
        ChineseFontBootstrap.ApplyChineseFont(_resultLine);
    }

    private Coroutine _statusCo;

    private void SetStatus(string msg)
    {
        if (_statusLine == null) return;
        _statusLine.text = msg ?? string.Empty;
        ChineseFontBootstrap.ApplyChineseFont(_statusLine);
        if (_statusCo != null) StopCoroutine(_statusCo);
        if (!string.IsNullOrEmpty(msg))
            _statusCo = StartCoroutine(ClearStatusAfter(1.2f));
    }

    private IEnumerator ClearStatusAfter(float sec)
    {
        yield return new WaitForSeconds(sec);
        if (_statusLine != null) _statusLine.text = string.Empty;
        _statusCo = null;
    }

    private static string Translate(string msg) => msg switch
    {
        "not enough ticket" => "招募券不足",
        "not enough diamond" => "钻石不足",
        "not enough gold" => "金币不足",
        "invalid or expired token" => "请重新登录",
        "gacha success" => "抽卡成功",
        _ => msg
    };

    private static void ApplyGachaBackground(Image bgImg)
    {
        if (bgImg == null) return;
        var tex = Resources.Load<Texture2D>("UI/gacha_bg");
        if (tex != null)
        {
            bgImg.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            bgImg.color = Color.white;
        }
        else
        {
            bgImg.sprite = null;
            bgImg.color = new Color(0.06f, 0.08f, 0.11f, 1f);
        }
    }

    private static void MakeSidebarBackButton(Transform parent, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject("BackBtn", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(12f, -12f);
        rt.sizeDelta = new Vector2(56f, 56f);
        go.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.20f, 1f);
        go.GetComponent<Button>().onClick.AddListener(onClick);

        var label = MakeText(go.transform, "Arrow", "←", 32, Vector2.zero, new Vector2(56f, 56f));
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.88f, 0.84f, 0.76f, 1f);
        var lrt = label.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
    }

    private void MakeNavButton(Transform parent, string label, PoolKind pool)
    {
        var go = new GameObject("Nav_" + label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 52f;
        le.minHeight = 52f;
        var bg = go.GetComponent<Image>();
        bg.color = new Color(0.10f, 0.12f, 0.16f, 0.6f);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = bg;
        var captured = pool;
        btn.onClick.AddListener(() =>
        {
            if (_busy) return;
            SelectPool(captured);
        });

        var tmp = MakeText(go.transform, "Label", label, 22, Vector2.zero, new Vector2(180f, 48f));
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.72f, 0.76f, 0.82f, 1f);
        var trt = tmp.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        var accent = new GameObject("Accent", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        accent.transform.SetParent(go.transform, false);
        var art = accent.GetComponent<RectTransform>();
        art.anchorMin = new Vector2(0f, 0f);
        art.anchorMax = new Vector2(0f, 1f);
        art.pivot = new Vector2(0f, 0.5f);
        art.sizeDelta = new Vector2(4f, 0f);
        art.anchoredPosition = Vector2.zero;
        accent.GetComponent<Image>().color = new Color(0.82f, 0.68f, 0.32f, 1f);
        accent.SetActive(false);

        _navBgs.Add(bg);
        _navLabels.Add(tmp);
        _navAccents.Add(accent);
    }

    private static TMP_Text CreateWalletChip(Transform parent, string name, string iconPath, Color accent)
    {
        var chip = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        chip.transform.SetParent(parent, false);
        var rt = chip.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(132f, 56f);
        chip.GetComponent<Image>().color = new Color(0.08f, 0.10f, 0.14f, 0.75f);

        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGo.transform.SetParent(chip.transform, false);
        var iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0f, 0.5f);
        iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot = new Vector2(0f, 0.5f);
        iconRt.anchoredPosition = new Vector2(8f, 0f);
        iconRt.sizeDelta = new Vector2(40f, 40f);
        var iconImg = iconGo.GetComponent<Image>();
        iconImg.raycastTarget = false;
        var tex = Resources.Load<Texture2D>(iconPath);
        if (tex != null)
        {
            iconImg.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            iconImg.preserveAspect = true;
        }
        else
        {
            iconImg.color = accent;
        }

        var count = MakeText(chip.transform, "Count", "0", 24, Vector2.zero, new Vector2(80f, 40f));
        var cRt = count.rectTransform;
        cRt.anchorMin = new Vector2(0f, 0f);
        cRt.anchorMax = new Vector2(1f, 1f);
        cRt.offsetMin = new Vector2(52f, 4f);
        cRt.offsetMax = new Vector2(-6f, -4f);
        count.alignment = TextAlignmentOptions.Left;
        count.fontStyle = FontStyles.Bold;
        count.color = Color.white;
        ChineseFontBootstrap.ApplyChineseFont(count);
        return count;
    }

    private static GameObject MakePullButton(Transform parent, string name, Vector2 pos, Vector2 size,
        UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var bg = go.GetComponent<Image>();
        bg.color = new Color(0.14f, 0.18f, 0.24f, 1f);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = bg;
        var colors = btn.colors;
        colors.normalColor = bg.color;
        colors.highlightedColor = new Color(0.20f, 0.26f, 0.34f, 1f);
        colors.pressedColor = new Color(0.10f, 0.14f, 0.20f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(onClick);

        var label = MakeText(go.transform, "Label", "", 22, Vector2.zero, size);
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.92f, 0.88f, 0.78f, 1f);
        label.enableWordWrapping = true;
        var lrt = label.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(8f, 4f);
        lrt.offsetMax = new Vector2(-8f, -4f);
        return go;
    }

    private static RectTransform MakePanel(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pos,
        Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        go.GetComponent<Image>().color = color;
        return rt;
    }

    private static TMP_Text MakeText(Transform parent, string name, string text, float size, Vector2 pos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        ChineseFontBootstrap.ApplyChineseFont(tmp);
        return tmp;
    }
}
