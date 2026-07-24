using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardBattle.Network;
using MyProjectF.Assets.Scripts.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Card upgrade panel: top category tabs (default Attack), no page title.
/// </summary>
public class CardUpgradePanelUI : MonoBehaviour
{
    public static CardUpgradePanelUI Instance { get; private set; }

    private static readonly (Card.CardType type, string title, Color accent)[] Categories =
    {
        (Card.CardType.Attack, "攻击", new Color(0.78f, 0.32f, 0.28f, 1f)),
        (Card.CardType.Guard, "防御", new Color(0.28f, 0.55f, 0.78f, 1f)),
        (Card.CardType.Tactic, "战术", new Color(0.45f, 0.62f, 0.38f, 1f)),
    };

    private GameObject _root;
    private Transform _tabRow;
    private Transform _cardList;
    private TMP_Text _status;
    private readonly List<Image> _tabBgs = new();
    private readonly List<TMP_Text> _tabLabels = new();
    private bool _busy;
    private Dictionary<string, Card> _byName;
    private Card.CardType _currentType = Card.CardType.Attack;
    private enum OwnFilter { All, Owned, Unowned }
    private OwnFilter _ownFilter = OwnFilter.All;
    private TMP_Text _filterLabel;
    private GameObject _filterMenu;
    private Coroutine _statusCo;

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Open()
    {
        EnsureUi();
        _root.SetActive(true);
        _ownFilter = OwnFilter.All;
        if (_filterLabel != null)
        {
            _filterLabel.text = "全部";
            ChineseFontBootstrap.ApplyChineseFont(_filterLabel);
        }
        SelectTab(Card.CardType.Attack);
        _ = PrefetchAsync();
    }

    public void Close()
    {
        if (_filterMenu != null) _filterMenu.SetActive(false);
        CardDetailPopupUI.Hide();
        if (_root != null) _root.SetActive(false);
    }

    private void EnsureUi()
    {
        if (_root != null) return;

        _root = new GameObject("CardUpgradePanel");
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5500;
        var scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        _root.AddComponent<GraphicRaycaster>();

        var dim = MakePanel(_root.transform, "Dim", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0f, 0f, 0f, 0.55f));
        var dimBtn = dim.gameObject.AddComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(Close);

        var panel = MakePanel(_root.transform, "Panel", V2(0.5f), V2(0.5f), Vector2.zero, new Vector2(820f, 760f),
            new Color(0.10f, 0.13f, 0.18f, 0.97f));

        // Layer1: category tabs (top)
        _tabRow = new GameObject("Tabs", typeof(RectTransform)).transform;
        _tabRow.SetParent(panel, false);
        var trt = _tabRow as RectTransform;
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -12f);
        trt.sizeDelta = new Vector2(-40f, 52f);
        var hlg = _tabRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.padding = new RectOffset(8, 8, 4, 4);

        foreach (var (type, title, accent) in Categories)
        {
            MakeTab(type, title, accent);
        }

        // Layer2: real dropdown (below tabs, no overlap)
        var filterBar = MakePanel(panel, "FilterBar", new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -76f), new Vector2(-40f, 44f), new Color(0.14f, 0.18f, 0.24f, 1f));
        filterBar.pivot = new Vector2(0.5f, 1f);
        var show = MakeText(filterBar, "FLab", "显示", 22, new Vector2(0f, 0f), new Vector2(80f, 36f));
        show.alignment = TextAlignmentOptions.Left;
        var srtf = show.rectTransform;
        srtf.anchorMin = srtf.anchorMax = new Vector2(0f, 0.5f);
        srtf.pivot = new Vector2(0f, 0.5f);
        srtf.anchoredPosition = new Vector2(16f, 0f);

        var dropBtn = new GameObject("DropBtn", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        dropBtn.transform.SetParent(filterBar, false);
        var dbrt = dropBtn.GetComponent<RectTransform>();
        dbrt.anchorMin = dbrt.anchorMax = new Vector2(0f, 0.5f);
        dbrt.pivot = new Vector2(0f, 0.5f);
        dbrt.anchoredPosition = new Vector2(90f, 0f);
        dbrt.sizeDelta = new Vector2(200f, 36f);
        dropBtn.GetComponent<Image>().color = new Color(0.18f, 0.22f, 0.28f, 1f);
        dropBtn.GetComponent<Button>().onClick.AddListener(ToggleFilterMenu);

        _filterLabel = MakeText(dropBtn.transform, "FilterValue", "全部", 22, Vector2.zero, new Vector2(200f, 36f));
        _filterLabel.alignment = TextAlignmentOptions.Left;
        var frt = _filterLabel.rectTransform;
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.offsetMin = new Vector2(12f, 0f);
        frt.offsetMax = new Vector2(-36f, 0f);

        // Real chevron arrow (font glyph ▾ often shows as □ with CJK fonts).
        var arrowGo = new GameObject("Arrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        arrowGo.transform.SetParent(dropBtn.transform, false);
        var art = arrowGo.GetComponent<RectTransform>();
        art.anchorMin = art.anchorMax = new Vector2(1f, 0.5f);
        art.pivot = new Vector2(1f, 0.5f);
        art.anchoredPosition = new Vector2(-10f, 0f);
        art.sizeDelta = new Vector2(22f, 22f);
        var aimg = arrowGo.GetComponent<Image>();
        aimg.raycastTarget = false;
        aimg.preserveAspect = true;
        var atex = Resources.Load<Texture2D>("UI/dropdown_arrow");
        if (atex != null)
        {
            aimg.sprite = Sprite.Create(atex, new Rect(0, 0, atex.width, atex.height), new Vector2(0.5f, 0.5f), 100f);
            aimg.color = new Color(0.88f, 0.84f, 0.76f, 1f);
        }
        else
        {
            aimg.color = new Color(0.88f, 0.84f, 0.76f, 1f);
        }

        _filterMenu = new GameObject("FilterMenu", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        _filterMenu.transform.SetParent(filterBar, false);
        var fmrt = _filterMenu.GetComponent<RectTransform>();
        fmrt.anchorMin = fmrt.anchorMax = new Vector2(0f, 1f);
        fmrt.pivot = new Vector2(0f, 1f);
        fmrt.anchoredPosition = new Vector2(90f, -44f);
        fmrt.sizeDelta = new Vector2(200f, 132f);
        _filterMenu.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.20f, 0.98f);
        var fvl = _filterMenu.AddComponent<VerticalLayoutGroup>();
        fvl.spacing = 2f;
        fvl.padding = new RectOffset(4, 4, 4, 4);
        fvl.childControlHeight = true;
        fvl.childControlWidth = true;
        fvl.childForceExpandHeight = false;
        fvl.childForceExpandWidth = true;
        MakeFilterOption(_filterMenu.transform, "全部", OwnFilter.All);
        MakeFilterOption(_filterMenu.transform, "已拥有", OwnFilter.Owned);
        MakeFilterOption(_filterMenu.transform, "未拥有", OwnFilter.Unowned);
        _filterMenu.SetActive(false);
        _filterMenu.transform.SetAsLastSibling();

        // Layer3: card list below filter
        var scrollGo = new GameObject("Scroll", typeof(RectTransform));
        scrollGo.transform.SetParent(panel, false);
        var srt = scrollGo.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 0f);
        srt.anchorMax = new Vector2(1f, 1f);
        srt.offsetMin = new Vector2(20f, 70f);
        srt.offsetMax = new Vector2(-20f, -132f);
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;

        var viewport = MakePanel(scrollGo.transform, "Viewport", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0, 0, 0, 0.02f));
        viewport.gameObject.AddComponent<RectMask2D>();
        scroll.viewport = viewport;

        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport, false);
        var crt = content.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta = new Vector2(0f, 0f);
        var layout = content.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 12f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = crt;
        _cardList = content.transform;

        _status = MakeText(panel, "Status", "", 20, new Vector2(0f, -340f), new Vector2(740f, 36f));
        _status.alignment = TextAlignmentOptions.Center;
        _status.color = new Color(1f, 0.85f, 0.45f);

        MakeButton(panel, "Close", "关闭", new Vector2(0f, -385f), new Vector2(200f, 52f), Close);
        _root.SetActive(false);
    }

    private void MakeTab(Card.CardType type, string title, Color accent)
    {
        var go = new GameObject("Tab_" + title, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button),
            typeof(LayoutElement));
        go.transform.SetParent(_tabRow, false);
        go.GetComponent<LayoutElement>().preferredHeight = 48f;
        var img = go.GetComponent<Image>();
        img.color = new Color(0.16f, 0.20f, 0.26f, 1f);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var captured = type;
        btn.onClick.AddListener(() => SelectTab(captured));

        var label = MakeText(go.transform, "L", title, 26, Vector2.zero, new Vector2(120f, 40f));
        label.alignment = TextAlignmentOptions.Center;
        var lrt = label.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;

        _tabBgs.Add(img);
        _tabLabels.Add(label);
        // store accent on label via color when selected
        label.color = Color.white;
    }

    private void SelectTab(Card.CardType type)
    {
        _currentType = type;
        for (var i = 0; i < Categories.Length; i++)
        {
            var selected = Categories[i].type == type;
            var accent = Categories[i].accent;
            if (i < _tabBgs.Count)
            {
                _tabBgs[i].color = selected
                    ? new Color(accent.r * 0.55f, accent.g * 0.55f, accent.b * 0.55f, 1f)
                    : new Color(0.16f, 0.20f, 0.26f, 1f);
            }

            if (i < _tabLabels.Count)
            {
                _tabLabels[i].color = selected ? Color.white : new Color(0.75f, 0.8f, 0.85f, 1f);
            }
        }

        RebuildCardRows();
        SetStatus("");
    }

    private async Task PrefetchAsync()
    {
        var net = GameNetwork.Instance;
        if (net == null || string.IsNullOrEmpty(net.Token)) return;
        try
        {
            var result = await AuthClient.ListUpgradesAsync(net.AuthHost, net.AuthPort, net.Token);
            if (result.Ok)
            {
                CardUpgradeCache.ApplyList(result);
                WalletHudUI.Instance?.SetBalances(result.Gold, result.Dust, result.Diamond, result.Ticket, true);
            }

            var deck = await AuthClient.GetDeckAsync(net.AuthHost, net.AuthPort, net.Token);
            if (deck.Ok)
            {
                CardUpgradeCache.SetOwned(deck.Owned);
            }

            RebuildCardRows();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[CardUI] prefetch: " + ex.Message);
        }
    }


    private void ToggleFilterMenu()
    {
        if (_filterMenu == null) return;
        _filterMenu.SetActive(!_filterMenu.activeSelf);
        if (_filterMenu.activeSelf)
        {
            // Keep dropdown above the card scroll layer.
            var bar = _filterMenu.transform.parent;
            if (bar != null && bar.parent != null)
            {
                bar.SetAsLastSibling();
            }
            _filterMenu.transform.SetAsLastSibling();
        }
    }

    private void SelectOwnFilter(OwnFilter filter)
    {
        _ownFilter = filter;
        if (_filterLabel != null)
        {
            _filterLabel.text = filter switch
            {
                OwnFilter.Owned => "已拥有",
                OwnFilter.Unowned => "未拥有",
                _ => "全部"
            };
            ChineseFontBootstrap.ApplyChineseFont(_filterLabel);
        }
        if (_filterMenu != null) _filterMenu.SetActive(false);
        RebuildCardRows();
    }

    private void MakeFilterOption(Transform parent, string label, OwnFilter filter)
    {
        var go = new GameObject("Opt_" + label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 40f;
        go.GetComponent<Image>().color = new Color(0.16f, 0.20f, 0.26f, 1f);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = go.GetComponent<Image>();
        var captured = filter;
        btn.onClick.AddListener(() => SelectOwnFilter(captured));
        var tmp = MakeText(go.transform, "L", label, 22, Vector2.zero, new Vector2(180f, 36f));
        tmp.alignment = TextAlignmentOptions.Left;
        var lrt = tmp.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(12f, 0f);
        lrt.offsetMax = new Vector2(-8f, 0f);
    }

    private void EnsureCardIndex()
    {
        if (_byName != null) return;
        _byName = new Dictionary<string, Card>();
        foreach (var c in Resources.LoadAll<Card>("Cards"))
        {
            if (c != null && !string.IsNullOrEmpty(c.cardName) && !_byName.ContainsKey(c.cardName))
                _byName[c.cardName] = c;
        }
    }

    private void RebuildCardRows()
    {
        if (_cardList == null) return;
        for (var i = _cardList.childCount - 1; i >= 0; i--)
            Destroy(_cardList.GetChild(i).gameObject);

        EnsureCardIndex();
        var cards = new List<Card>();
        foreach (var kv in _byName)
        {
            if (kv.Value.cardType != _currentType) continue;
            var owned = CardUpgradeCache.IsOwned(kv.Key);
            if (_ownFilter == OwnFilter.Owned && !owned) continue;
            if (_ownFilter == OwnFilter.Unowned && owned) continue;
            cards.Add(kv.Value);
        }

        cards.Sort((a, b) => string.CompareOrdinal(a.GetDisplayName(), b.GetDisplayName()));
        foreach (var card in cards) CreateCardRow(card);
    }

    private void CreateCardRow(Card card)
    {
        var key = card.cardName;
        var row = MakePanel(_cardList, "Row_" + key, V2(0.5f), V2(0.5f), Vector2.zero, new Vector2(720f, 108f),
            new Color(0.16f, 0.20f, 0.26f, 0.95f));
        var le = row.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 108f;
        le.preferredHeight = 108f;

        var thumbGo = new GameObject("Thumb", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        thumbGo.transform.SetParent(row, false);
        var trt = thumbGo.GetComponent<RectTransform>();
        trt.anchorMin = trt.anchorMax = new Vector2(0f, 0.5f);
        trt.pivot = new Vector2(0f, 0.5f);
        trt.anchoredPosition = new Vector2(14f, 0f);
        trt.sizeDelta = new Vector2(78f, 110f);
        var thumb = thumbGo.GetComponent<Image>();
        thumb.preserveAspect = false;
        thumb.raycastTarget = false;
        if (card.cardSprite != null)
        {
            thumb.sprite = card.cardSprite;
            thumb.color = Color.white;
        }
        else thumb.color = new Color(0.25f, 0.3f, 0.38f, 1f);

        var level = CardUpgradeCache.GetLevel(key);
        var cost = CardUpgradeCache.NextUpgradeCost(level);
        var copies = CardUpgradeCache.MaxCopiesAllowed(level);
        var info = MakeText(row, "Info",
            $"{card.GetDisplayName()}\nLv.{level}/{CardUpgradeCache.MaxLevel} · 可带{copies}张",
            22, Vector2.zero, new Vector2(360f, 80f));
        info.alignment = TextAlignmentOptions.Left;
        var irt = info.rectTransform;
        irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
        irt.pivot = new Vector2(0f, 0.5f);
        irt.anchoredPosition = new Vector2(110f, 0f);

        var owned = CardUpgradeCache.IsOwned(key);
        Button btn;
        if (!owned)
        {
            btn = MakeButton(row, "Up", "未拥有", Vector2.zero, new Vector2(150f, 52f), () => { });
            btn.interactable = false;
            btn.GetComponent<Image>().color = new Color(0.28f, 0.30f, 0.34f, 1f);
        }
        else
        {
            btn = MakeUpgradeButton(row, "Up", level, cost, () => _ = DoUpgrade(key));
            btn.interactable = level < CardUpgradeCache.MaxLevel && CardUpgradeCache.LastDust >= cost;
        }
        var brt = btn.transform as RectTransform;
        brt.anchorMin = brt.anchorMax = new Vector2(1f, 0.5f);
        brt.pivot = new Vector2(1f, 0.5f);
        brt.anchoredPosition = new Vector2(-18f, 0f);

        // Left hit area opens detail; right upgrade button stays independent.
        var hit = new GameObject("DetailHit", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        hit.transform.SetParent(row, false);
        hit.transform.SetSiblingIndex(0);
        var hrt = hit.GetComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0f, 0f);
        hrt.anchorMax = new Vector2(1f, 1f);
        hrt.offsetMin = Vector2.zero;
        hrt.offsetMax = new Vector2(-180f, 0f);
        var himg = hit.GetComponent<Image>();
        himg.color = new Color(1f, 1f, 1f, 0.001f);
        himg.raycastTarget = true;
        var hbtn = hit.GetComponent<Button>();
        hbtn.transition = Selectable.Transition.None;
        hbtn.targetGraphic = himg;
        var capturedCard = card;
        hbtn.onClick.AddListener(() => CardDetailPopupUI.Show(capturedCard));
    }

    private async Task DoUpgrade(string cardKey)
    {
        if (_busy) return;
        var net = GameNetwork.Instance;
        if (net == null || string.IsNullOrEmpty(net.Token))
        {
            SetStatus("请先登录");
            return;
        }

        _busy = true;
        try
        {
            var result = await AuthClient.UpgradeCardAsync(net.AuthHost, net.AuthPort, net.Token, cardKey);
            if (!result.Ok)
            {
                SetStatus(Translate(result.Message));
                return;
            }

            CardUpgradeCache.ApplyUpgrade(result);
            WalletHudUI.Instance?.SetBalances(result.Gold, result.Dust, result.Diamond, result.Ticket, true);
            var name = _byName != null && _byName.TryGetValue(result.CardKey, out var c)
                ? c.GetDisplayName() : result.CardKey;
            SetStatus($"{name} → Lv.{result.Level}");
            RebuildCardRows();
        }
        catch (System.Exception ex) { SetStatus(ex.Message); }
        finally { _busy = false; }
    }

    private void SetStatus(string msg)
    {
        if (_status == null) return;
        _status.text = msg ?? string.Empty;
        ChineseFontBootstrap.ApplyChineseFont(_status);
        if (_statusCo != null) StopCoroutine(_statusCo);
        if (!string.IsNullOrEmpty(msg))
            _statusCo = StartCoroutine(ClearStatusAfter(1f));
    }

    private IEnumerator ClearStatusAfter(float sec)
    {
        yield return new WaitForSeconds(sec);
        if (_status != null) _status.text = string.Empty;
        _statusCo = null;
    }

    private static string Translate(string msg) => msg switch
    {
        "not enough dust" => "粉尘不足",
        "already max level" => "已达最高等级",
        "unknown card" => "不可升级",
        "invalid or expired token" => "请重新登录",
        _ => msg
    };

    private static Vector2 V2(float v) => new Vector2(v, v);

    private static RectTransform MakePanel(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pos,
        Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        go.GetComponent<Image>().color = color;
        return rt;
    }

    private static TMP_Text MakeText(Transform parent, string name, string text, float size, Vector2 pos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.color = Color.white; tmp.raycastTarget = false;
        ChineseFontBootstrap.ApplyChineseFont(tmp);
        return tmp;
    }


    private static Button MakeUpgradeButton(Transform parent, string name, byte level, int cost,
        UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(178f, 52f);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.18f, 0.45f, 0.72f, 1f);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var row = new GameObject("Content", typeof(RectTransform));
        row.transform.SetParent(go.transform, false);
        var rrt = row.GetComponent<RectTransform>();
        rrt.anchorMin = Vector2.zero;
        rrt.anchorMax = Vector2.one;
        rrt.offsetMin = new Vector2(8f, 4f);
        rrt.offsetMax = new Vector2(-8f, -4f);
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 6f;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(row.transform, false);
        var lrt = labelGo.GetComponent<RectTransform>();
        lrt.sizeDelta = new Vector2(110f, 40f);
        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        if (level >= CardUpgradeCache.MaxLevel)
        {
            tmp.text = "已满级";
            lrt.sizeDelta = new Vector2(140f, 40f);
        }
        else
        {
            tmp.text = $"升级 -{cost}";
        }

        tmp.fontSize = 22;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        ChineseFontBootstrap.ApplyChineseFont(tmp);

        if (level < CardUpgradeCache.MaxLevel)
        {
            var iconGo = new GameObject("Dust", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGo.transform.SetParent(row.transform, false);
            var irt = iconGo.GetComponent<RectTransform>();
            irt.sizeDelta = new Vector2(28f, 28f);
            var icon = iconGo.GetComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            var tex = Resources.Load<Texture2D>("UI/icon_dust");
            if (tex != null)
            {
                icon.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                icon.color = Color.white;
            }
            else
            {
                icon.color = new Color(0.55f, 0.45f, 0.95f, 1f);
            }
        }

        return btn;
    }

    private static Button MakeButton(Transform parent, string name, string label, Vector2 pos, Vector2 size,
        UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.color = new Color(0.18f, 0.45f, 0.72f, 1f);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 22; tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white; tmp.raycastTarget = false;
        ChineseFontBootstrap.ApplyChineseFont(tmp);
        return btn;
    }
}
