using System.Collections.Generic;
using System.Threading.Tasks;
using CardBattle.Network;
using MyProjectF.Assets.Scripts.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bag / inventory: choose open count → progress → result popup.
/// </summary>
public class BagPanelUI : MonoBehaviour
{
    public static BagPanelUI Instance { get; private set; }

    private GameObject _root;
    private Transform _grid;
    private TMP_Text _status;
    private GameObject _openRoot;
    private TMP_Text _openTitle;
    private TMP_Text _openCountLabel;
    private GameObject _progressRoot;
    private Image _progressFill;
    private TMP_Text _progressLabel;
    private GameObject _resultRoot;
    private Transform _resultList;
    private ushort _pendingItemId;
    private uint _pendingMax;
    private int _openCount = 1;
    private bool _busy;
    private Dictionary<string, Card> _cardByKey;

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Open()
    {
        EnsureUi();
        HideOpenDialog();
        HideProgress();
        HideResult();
        _root.SetActive(true);
        _ = RefreshAsync();
    }

    public void Close()
    {
        HideOpenDialog();
        HideProgress();
        HideResult();
        if (_root != null) _root.SetActive(false);
    }

    private void EnsureUi()
    {
        if (_root != null) return;

        _root = new GameObject("BagPanel");
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5500;
        var scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        _root.AddComponent<GraphicRaycaster>();

        var dimGo = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        dimGo.transform.SetParent(_root.transform, false);
        var dimRt = dimGo.GetComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = dimRt.offsetMax = Vector2.zero;
        dimGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        var dimBtn = dimGo.GetComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(Close);

        var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelGo.transform.SetParent(_root.transform, false);
        var prt = panelGo.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(720f, 620f);
        panelGo.GetComponent<Image>().color = new Color(0.10f, 0.13f, 0.18f, 0.97f);

        var title = MakeText(panelGo.transform, "Title", "背包", 40, new Vector2(0f, 250f), new Vector2(640f, 48f));
        title.alignment = TextAlignmentOptions.Center;

        var gridGo = new GameObject("Grid", typeof(RectTransform));
        gridGo.transform.SetParent(panelGo.transform, false);
        var grt = gridGo.GetComponent<RectTransform>();
        grt.anchorMin = grt.anchorMax = new Vector2(0.5f, 0.5f);
        grt.anchoredPosition = new Vector2(0f, 10f);
        grt.sizeDelta = new Vector2(640f, 400f);
        var grid = gridGo.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(140f, 160f);
        grid.spacing = new Vector2(16f, 16f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;
        grid.childAlignment = TextAnchor.UpperLeft;
        _grid = gridGo.transform;

        _status = MakeText(panelGo.transform, "Status", "", 20, new Vector2(0f, -240f), new Vector2(640f, 36f));
        _status.alignment = TextAlignmentOptions.Center;
        _status.color = new Color(1f, 0.85f, 0.45f);

        MakeButton(panelGo.transform, "Close", "关闭", new Vector2(0f, -280f), new Vector2(200f, 56f), Close);

        EnsureOpenDialog();
        EnsureProgressUi();
        EnsureResultUi();
        _root.SetActive(false);
    }

    private void EnsureOpenDialog()
    {
        if (_openRoot != null) return;

        _openRoot = new GameObject("OpenDialog", typeof(RectTransform));
        _openRoot.transform.SetParent(_root.transform, false);
        var ort = _openRoot.GetComponent<RectTransform>();
        ort.anchorMin = Vector2.zero;
        ort.anchorMax = Vector2.one;
        ort.offsetMin = ort.offsetMax = Vector2.zero;

        var dim = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        dim.transform.SetParent(_openRoot.transform, false);
        var drt = dim.GetComponent<RectTransform>();
        drt.anchorMin = Vector2.zero;
        drt.anchorMax = Vector2.one;
        drt.offsetMin = drt.offsetMax = Vector2.zero;
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        dim.GetComponent<Button>().transition = Selectable.Transition.None;
        dim.GetComponent<Button>().onClick.AddListener(HideOpenDialog);

        var box = new GameObject("Box", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        box.transform.SetParent(_openRoot.transform, false);
        var brt = box.GetComponent<RectTransform>();
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.sizeDelta = new Vector2(440f, 260f);
        box.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.20f, 0.98f);

        _openTitle = MakeText(box.transform, "Title", "打开宝箱", 28, new Vector2(0f, 80f), new Vector2(400f, 40f));
        _openTitle.alignment = TextAlignmentOptions.Center;

        // −  count  +  ≫(max)
        MakeButton(box.transform, "Minus", "−", new Vector2(-130f, 10f), new Vector2(56f, 56f), () => ChangeOpenCount(-1));
        _openCountLabel = MakeText(box.transform, "Count", "1", 36, new Vector2(-30f, 10f), new Vector2(100f, 56f));
        _openCountLabel.alignment = TextAlignmentOptions.Center;
        _openCountLabel.fontStyle = FontStyles.Bold;
        MakeButton(box.transform, "Plus", "+", new Vector2(70f, 10f), new Vector2(56f, 56f), () => ChangeOpenCount(+1));
        MakeButton(box.transform, "Max", "≫", new Vector2(150f, 10f), new Vector2(56f, 56f), () =>
        {
            _openCount = (int)Mathf.Max(1, _pendingMax);
            RefreshOpenCountLabel();
        });

        MakeButton(box.transform, "Confirm", "打开", new Vector2(-90f, -80f), new Vector2(140f, 48f), () =>
        {
            HideOpenDialog();
            _ = OpenChestsAsync(_pendingItemId, _openCount);
        });
        MakeButton(box.transform, "Cancel", "取消", new Vector2(90f, -80f), new Vector2(140f, 48f), HideOpenDialog);

        _openRoot.SetActive(false);
    }

    private void EnsureProgressUi()
    {
        if (_progressRoot != null) return;

        _progressRoot = new GameObject("ProgressOverlay", typeof(RectTransform));
        _progressRoot.transform.SetParent(_root.transform, false);
        var rt = _progressRoot.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var dim = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dim.transform.SetParent(_progressRoot.transform, false);
        var drt = dim.GetComponent<RectTransform>();
        drt.anchorMin = Vector2.zero;
        drt.anchorMax = Vector2.one;
        drt.offsetMin = drt.offsetMax = Vector2.zero;
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        dim.GetComponent<Image>().raycastTarget = true;

        var box = new GameObject("Box", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        box.transform.SetParent(_progressRoot.transform, false);
        var brt = box.GetComponent<RectTransform>();
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.sizeDelta = new Vector2(480f, 160f);
        box.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.20f, 0.98f);

        _progressLabel = MakeText(box.transform, "Label", "开启中…", 24, new Vector2(0f, 36f), new Vector2(420f, 36f));
        _progressLabel.alignment = TextAlignmentOptions.Center;

        var barBg = new GameObject("BarBg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        barBg.transform.SetParent(box.transform, false);
        var bbrt = barBg.GetComponent<RectTransform>();
        bbrt.anchorMin = bbrt.anchorMax = new Vector2(0.5f, 0.5f);
        bbrt.anchoredPosition = new Vector2(0f, -20f);
        bbrt.sizeDelta = new Vector2(400f, 28f);
        barBg.GetComponent<Image>().color = new Color(0.08f, 0.10f, 0.14f, 1f);

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fill.transform.SetParent(barBg.transform, false);
        var frt = fill.GetComponent<RectTransform>();
        frt.anchorMin = new Vector2(0f, 0f);
        frt.anchorMax = new Vector2(0f, 1f);
        frt.pivot = new Vector2(0f, 0.5f);
        frt.anchoredPosition = Vector2.zero;
        frt.sizeDelta = new Vector2(0f, 0f);
        _progressFill = fill.GetComponent<Image>();
        _progressFill.color = new Color(0.82f, 0.68f, 0.32f, 1f);

        _progressRoot.SetActive(false);
    }

    private void EnsureResultUi()
    {
        if (_resultRoot != null) return;

        _resultRoot = new GameObject("ResultOverlay", typeof(RectTransform));
        _resultRoot.transform.SetParent(_root.transform, false);
        var rt = _resultRoot.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var dim = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        dim.transform.SetParent(_resultRoot.transform, false);
        var drt = dim.GetComponent<RectTransform>();
        drt.anchorMin = Vector2.zero;
        drt.anchorMax = Vector2.one;
        drt.offsetMin = drt.offsetMax = Vector2.zero;
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
        dim.GetComponent<Button>().transition = Selectable.Transition.None;
        dim.GetComponent<Button>().onClick.AddListener(HideResult);

        var box = new GameObject("Box", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        box.transform.SetParent(_resultRoot.transform, false);
        var brt = box.GetComponent<RectTransform>();
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.sizeDelta = new Vector2(560f, 480f);
        box.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.20f, 0.98f);

        var title = MakeText(box.transform, "Title", "开启结果", 30, new Vector2(0f, 200f), new Vector2(400f, 40f));
        title.alignment = TextAlignmentOptions.Center;

        // Top-right ×
        var close = new GameObject("CloseX", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        close.transform.SetParent(box.transform, false);
        var crt = close.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(1f, 1f);
        crt.anchoredPosition = new Vector2(-10f, -10f);
        crt.sizeDelta = new Vector2(44f, 44f);
        close.GetComponent<Image>().color = new Color(0.25f, 0.28f, 0.34f, 1f);
        close.GetComponent<Button>().onClick.AddListener(HideResult);
        var xLabel = MakeText(close.transform, "X", "×", 32, Vector2.zero, new Vector2(44f, 44f));
        xLabel.alignment = TextAlignmentOptions.Center;
        var xlrt = xLabel.rectTransform;
        xlrt.anchorMin = Vector2.zero;
        xlrt.anchorMax = Vector2.one;
        xlrt.offsetMin = xlrt.offsetMax = Vector2.zero;

        var scrollGo = new GameObject("Scroll", typeof(RectTransform));
        scrollGo.transform.SetParent(box.transform, false);
        var srt = scrollGo.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.5f, 0.5f);
        srt.anchorMax = new Vector2(0.5f, 0.5f);
        srt.anchoredPosition = new Vector2(0f, -10f);
        srt.sizeDelta = new Vector2(500f, 360f);
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
        viewport.transform.SetParent(scrollGo.transform, false);
        var vrt = viewport.GetComponent<RectTransform>();
        vrt.anchorMin = Vector2.zero;
        vrt.anchorMax = Vector2.one;
        vrt.offsetMin = vrt.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = new Color(0, 0, 0, 0.02f);
        scroll.viewport = vrt;

        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0f, 0f);
        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8f;
        vlg.padding = new RectOffset(8, 8, 8, 8);
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = contentRt;
        _resultList = content.transform;

        _resultRoot.SetActive(false);
    }

    private void ShowOpenDialog(ushort itemId, uint maxCount)
    {
        EnsureOpenDialog();
        _pendingItemId = itemId;
        _pendingMax = maxCount;
        _openCount = 1;
        ItemCatalog.TryGet(itemId, out var def);
        var name = string.IsNullOrEmpty(def.Name) ? $"物品{itemId}" : def.Name;
        _openTitle.text = $"打开 {name}（拥有 {maxCount}）";
        ChineseFontBootstrap.ApplyChineseFont(_openTitle);
        RefreshOpenCountLabel();
        _openRoot.SetActive(true);
        _openRoot.transform.SetAsLastSibling();
    }

    private void HideOpenDialog()
    {
        if (_openRoot != null) _openRoot.SetActive(false);
    }

    private void ShowProgress(float t01, string label)
    {
        EnsureProgressUi();
        _progressRoot.SetActive(true);
        _progressRoot.transform.SetAsLastSibling();
        if (_progressLabel != null)
        {
            _progressLabel.text = label ?? "";
            ChineseFontBootstrap.ApplyChineseFont(_progressLabel);
        }

        if (_progressFill != null)
        {
            var parent = _progressFill.transform.parent as RectTransform;
            var w = parent != null ? parent.rect.width : 400f;
            var frt = _progressFill.rectTransform;
            frt.sizeDelta = new Vector2(Mathf.Clamp01(t01) * w, 0f);
        }
    }

    private void HideProgress()
    {
        if (_progressRoot != null) _progressRoot.SetActive(false);
    }

    private void ShowResult(List<string> grantedNames)
    {
        EnsureResultUi();
        for (var i = _resultList.childCount - 1; i >= 0; i--)
            Destroy(_resultList.GetChild(i).gameObject);

        if (grantedNames == null || grantedNames.Count == 0)
        {
            AddResultRow("（未开出新卡，已获得宝箱奖励）", null);
        }
        else
        {
            // Aggregate duplicates: 卡名 ×N
            var counts = new Dictionary<string, int>();
            foreach (var n in grantedNames)
            {
                if (!counts.ContainsKey(n)) counts[n] = 0;
                counts[n]++;
            }

            foreach (var kv in counts)
            {
                var label = kv.Value > 1 ? $"{kv.Key} ×{kv.Value}" : kv.Key;
                Sprite sp = null;
                EnsureCardIndex();
                foreach (var c in _cardByKey.Values)
                {
                    if (c != null && c.GetDisplayName() == kv.Key)
                    {
                        sp = c.cardSprite;
                        break;
                    }
                }

                AddResultRow(label, sp);
            }
        }

        _resultRoot.SetActive(true);
        _resultRoot.transform.SetAsLastSibling();
    }

    private void AddResultRow(string label, Sprite sprite)
    {
        var row = new GameObject("Row", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        row.transform.SetParent(_resultList, false);
        row.GetComponent<LayoutElement>().preferredHeight = 64f;
        row.GetComponent<Image>().color = new Color(0.16f, 0.20f, 0.26f, 0.95f);

        var icon = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        icon.transform.SetParent(row.transform, false);
        var irt = icon.GetComponent<RectTransform>();
        irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
        irt.pivot = new Vector2(0f, 0.5f);
        irt.anchoredPosition = new Vector2(10f, 0f);
        irt.sizeDelta = new Vector2(48f, 56f);
        var iimg = icon.GetComponent<Image>();
        iimg.preserveAspect = false;
        iimg.raycastTarget = false;
        if (sprite != null)
        {
            iimg.sprite = sprite;
            iimg.color = Color.white;
        }
        else
        {
            iimg.color = new Color(0.3f, 0.35f, 0.4f, 1f);
        }

        var txt = MakeText(row.transform, "Name", label, 22, Vector2.zero, new Vector2(360f, 48f));
        txt.alignment = TextAlignmentOptions.Left;
        var trt = txt.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0f, 0.5f);
        trt.pivot = new Vector2(0f, 0.5f);
        trt.anchoredPosition = new Vector2(72f, 0f);
    }

    private void HideResult()
    {
        if (_resultRoot != null) _resultRoot.SetActive(false);
    }

    private void ChangeOpenCount(int delta)
    {
        _openCount = Mathf.Clamp(_openCount + delta, 1, (int)Mathf.Max(1, _pendingMax));
        RefreshOpenCountLabel();
    }

    private void RefreshOpenCountLabel()
    {
        if (_openCountLabel == null) return;
        _openCountLabel.text = _openCount.ToString();
        ChineseFontBootstrap.ApplyChineseFont(_openCountLabel);
    }

    private async Task RefreshAsync()
    {
        var net = GameNetwork.Instance;
        if (net == null || string.IsNullOrEmpty(net.Token))
        {
            SetStatus("请先登录");
            return;
        }

        try
        {
            var inv = await AuthClient.FetchInventoryAsync(net.AuthHost, net.AuthPort, net.Token);
            if (!inv.Ok)
            {
                SetStatus(inv.Message);
                return;
            }

            CardUpgradeCache.SetWallet(inv.Gold, inv.Dust, inv.Diamond, inv.Ticket);
            WalletHudUI.Instance?.SetBalances(inv.Gold, inv.Dust, inv.Diamond, inv.Ticket, true);
            Rebuild(inv);
            SetStatus(inv.Items.Length == 0 ? "空空如也" : "");
        }
        catch (System.Exception ex)
        {
            SetStatus(ex.Message);
        }
    }

    private void Rebuild(InventorySnapshot inv)
    {
        if (_grid == null) return;
        for (var i = _grid.childCount - 1; i >= 0; i--)
            Destroy(_grid.GetChild(i).gameObject);

        foreach (var (itemId, count) in inv.Items)
        {
            if (count == 0) continue;
            CreateSlot(itemId, count);
        }
    }

    private void CreateSlot(ushort itemId, uint count)
    {
        ItemCatalog.TryGet(itemId, out var def);
        var name = string.IsNullOrEmpty(def.Name) ? $"物品{itemId}" : def.Name;
        var color = def.Id == 0 ? new Color(0.3f, 0.35f, 0.4f) : def.Color;

        var go = new GameObject("Item_" + itemId, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(_grid, false);
        var bg = go.GetComponent<Image>();
        bg.color = new Color(0.16f, 0.20f, 0.26f, 0.95f);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = bg;
        var capturedId = itemId;
        var capturedCount = count;
        btn.onClick.AddListener(() => ShowOpenDialog(capturedId, capturedCount));

        var icon = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        icon.transform.SetParent(go.transform, false);
        var irt = icon.GetComponent<RectTransform>();
        irt.anchorMin = new Vector2(0.5f, 1f);
        irt.anchorMax = new Vector2(0.5f, 1f);
        irt.pivot = new Vector2(0.5f, 1f);
        irt.anchoredPosition = new Vector2(0f, -10f);
        irt.sizeDelta = new Vector2(72f, 72f);
        var iimg = icon.GetComponent<Image>();
        iimg.raycastTarget = false;
        iimg.preserveAspect = true;
        var sprite = ItemCatalog.LoadIcon(itemId);
        if (sprite != null)
        {
            iimg.sprite = sprite;
            iimg.color = Color.white;
        }
        else
        {
            iimg.sprite = null;
            iimg.color = color;
        }

        var label = MakeText(go.transform, "Name", name, 18, new Vector2(0f, -20f), new Vector2(130f, 28f));
        label.alignment = TextAlignmentOptions.Center;
        var lrt = label.rectTransform;
        lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0f);
        lrt.pivot = new Vector2(0.5f, 0f);
        lrt.anchoredPosition = new Vector2(0f, 28f);

        var cnt = MakeText(go.transform, "Count", "x" + count, 18, new Vector2(0f, 4f), new Vector2(120f, 24f));
        cnt.alignment = TextAlignmentOptions.Center;
        cnt.color = new Color(0.9f, 0.85f, 0.55f);
        var crt = cnt.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0f);
        crt.pivot = new Vector2(0.5f, 0f);
        crt.anchoredPosition = new Vector2(0f, 6f);
    }

    private async Task OpenChestsAsync(ushort itemId, int count)
    {
        if (_busy || count <= 0) return;
        var net = GameNetwork.Instance;
        if (net == null || string.IsNullOrEmpty(net.Token))
        {
            SetStatus("请先登录");
            return;
        }

        _busy = true;
        var grantedNames = new List<string>();
        UseItemResult last = default;
        var opened = 0;
        try
        {
            ShowProgress(0f, $"开启中… 0/{count}");
            for (var i = 0; i < count; i++)
            {
                var result = await AuthClient.UseItemAsync(net.AuthHost, net.AuthPort, net.Token, itemId);
                last = result;
                if (!result.Ok)
                {
                    HideProgress();
                    SetStatus(Translate(result.Message));
                    break;
                }

                opened++;
                CardUpgradeCache.SetWallet(result.Gold, result.Dust, result.Diamond, result.Ticket);
                if (!string.IsNullOrEmpty(result.GrantedCard))
                {
                    CardUpgradeCache.AddOwned(result.GrantedCard);
                    grantedNames.Add(ResolveCardDisplayName(result.GrantedCard));
                }

                ShowProgress((i + 1f) / count, $"开启中… {i + 1}/{count}");
            }

            ShowProgress(1f, "完成");
            await Task.Delay(200);
            HideProgress();

            if (last.Ok)
            {
                WalletHudUI.Instance?.SetBalances(last.Gold, last.Dust, last.Diamond, last.Ticket, true);
                Rebuild(new InventorySnapshot(true, last.Message, last.Gold, last.Dust, last.Diamond, last.Ticket, last.Items));
                ShowResult(grantedNames);
                SetStatus(opened > 0 ? $"已打开 {opened} 个" : "");
            }
        }
        catch (System.Exception ex)
        {
            HideProgress();
            SetStatus(ex.Message);
        }
        finally
        {
            _busy = false;
        }
    }

    private string ResolveCardDisplayName(string cardKey)
    {
        if (string.IsNullOrEmpty(cardKey)) return cardKey;
        EnsureCardIndex();
        if (_cardByKey.TryGetValue(cardKey, out var card) && card != null)
            return card.GetDisplayName();

        var deck = PlayerDeck.Instance ?? Object.FindFirstObjectByType<PlayerDeck>();
        if (deck != null && deck.TryGetCard(cardKey, out var dcard) && dcard != null)
            return dcard.GetDisplayName();

        return cardKey;
    }

    private void EnsureCardIndex()
    {
        if (_cardByKey != null) return;
        _cardByKey = new Dictionary<string, Card>();
        foreach (var c in Resources.LoadAll<Card>("Cards"))
        {
            if (c != null && !string.IsNullOrEmpty(c.cardName) && !_cardByKey.ContainsKey(c.cardName))
                _cardByKey[c.cardName] = c;
        }
    }

    private void SetStatus(string msg)
    {
        if (_status == null) return;
        _status.text = msg ?? string.Empty;
        ChineseFontBootstrap.ApplyChineseFont(_status);
    }

    private static string Translate(string msg) => msg switch
    {
        "not enough item" => "数量不足",
        "unknown item" => "未知物品",
        "invalid or expired token" => "请重新登录",
        "item used" => "使用成功",
        _ => msg
    };

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

    private static void MakeButton(Transform parent, string name, string label, Vector2 pos, Vector2 size,
        UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.color = new Color(0.18f, 0.45f, 0.72f, 1f);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 22;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        ChineseFontBootstrap.ApplyChineseFont(tmp);
    }
}
