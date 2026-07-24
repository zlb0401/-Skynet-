using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardBattle.Network;
using MyProjectF.Assets.Scripts.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Deck editor: category tabs + count/edit bar + per-card +/- in edit mode.
/// </summary>
public class DeckEditPanelUI : MonoBehaviour
{
    public static DeckEditPanelUI Instance { get; private set; }

    private const int MinSize = 5;
    private const int MaxSize = 12;

    private static readonly (Card.CardType type, string title, Color accent)[] Categories =
    {
        (Card.CardType.Attack, "攻击", new Color(0.78f, 0.32f, 0.28f, 1f)),
        (Card.CardType.Guard, "防御", new Color(0.28f, 0.55f, 0.78f, 1f)),
        (Card.CardType.Tactic, "战术", new Color(0.45f, 0.62f, 0.38f, 1f)),
    };

    private GameObject _root;
    private Transform _tabRow;
    private Transform _cardList;
    private TMP_Text _countLabel;
    private TMP_Text _editBtnLabel;
    private TMP_Text _status;
    private GameObject _confirmRoot;
    private readonly List<Image> _tabBgs = new();
    private readonly List<TMP_Text> _tabLabels = new();

    private readonly List<string> _draft = new();
    private readonly List<string> _baseline = new();
    private HashSet<string> _owned = new();
    private Dictionary<string, Card> _byName;
    private Card.CardType _currentType = Card.CardType.Attack;
    private bool _editing;
    private bool _busy;

    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }

    public void Open()
    {
        EnsureUi();
        _editing = false;
        UpdateEditButtonLabel();
        HideConfirm();
        _root.SetActive(true);
        SelectTab(Card.CardType.Attack);
        _ = LoadAsync();
    }

    public void Close()
    {
        CardDetailPopupUI.Hide();
        if (_root != null) _root.SetActive(false);
        HideConfirm();
        _editing = false;
    }

    private void EnsureUi()
    {
        if (_root != null) return;

        _root = new GameObject("DeckEditPanel");
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5500;
        var scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        _root.AddComponent<GraphicRaycaster>();

        var dim = MakePanel(_root.transform, "Dim", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0, 0, 0, 0.55f));
        var dimBtn = dim.gameObject.AddComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(() =>
        {
            if (_editing) OnExitEditClicked();
            else Close();
        });

        var panel = MakePanel(_root.transform, "Panel", V2(0.5f), V2(0.5f), Vector2.zero, new Vector2(860f, 780f),
            new Color(0.10f, 0.13f, 0.18f, 0.97f));

        // Top tabs
        _tabRow = new GameObject("Tabs", typeof(RectTransform)).transform;
        _tabRow.SetParent(panel, false);
        var trt = _tabRow as RectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -16f);
        trt.sizeDelta = new Vector2(800f, 52f);
        var hlg = _tabRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.padding = new RectOffset(8, 8, 4, 4);
        foreach (var (type, title, accent) in Categories)
            MakeTab(type, title, accent);

        // Count + Edit bar
        var bar = MakePanel(panel, "Bar", V2(0.5f), V2(0.5f), new Vector2(0f, 280f), new Vector2(800f, 48f),
            new Color(0.14f, 0.18f, 0.24f, 1f));

        _countLabel = MakeText(bar, "Count", "0 / 12", 26, new Vector2(-250f, 0f), new Vector2(280f, 40f));
        _countLabel.alignment = TextAlignmentOptions.Left;
        var crt = _countLabel.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0f, 0.5f);
        crt.pivot = new Vector2(0f, 0.5f);
        crt.anchoredPosition = new Vector2(20f, 0f);

        var editBtn = MakeButton(bar, "EditBtn", "编辑卡组", new Vector2(0f, 0f), new Vector2(180f, 40f), OnEditOrExitClicked);
        var ebrt = editBtn.transform as RectTransform;
        ebrt.anchorMin = ebrt.anchorMax = new Vector2(1f, 0.5f);
        ebrt.pivot = new Vector2(1f, 0.5f);
        ebrt.anchoredPosition = new Vector2(-16f, 0f);
        _editBtnLabel = editBtn.GetComponentInChildren<TMP_Text>();

        // Card list
        var scrollGo = new GameObject("Scroll", typeof(RectTransform));
        scrollGo.transform.SetParent(panel, false);
        var srt = scrollGo.GetComponent<RectTransform>();
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
        srt.anchoredPosition = new Vector2(0f, -20f);
        srt.sizeDelta = new Vector2(800f, 520f);
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;

        var viewport = MakePanel(scrollGo.transform, "Viewport", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0, 0, 0, 0.02f));
        viewport.gameObject.AddComponent<RectMask2D>();
        scroll.viewport = viewport;

        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport, false);
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0f, 0f);
        var layout = content.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 12f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = contentRt;
        _cardList = content.transform;

        _status = MakeText(panel, "Status", "", 20, new Vector2(0f, -340f), new Vector2(780f, 32f));
        _status.alignment = TextAlignmentOptions.Center;
        _status.color = new Color(1f, 0.85f, 0.45f);

        MakeButton(panel, "Close", "关闭", new Vector2(0f, -380f), new Vector2(180f, 48f), () =>
        {
            if (_editing) OnExitEditClicked();
            else Close();
        });

        BuildConfirmOverlay();
        _root.SetActive(false);
    }

    private void BuildConfirmOverlay()
    {
        _confirmRoot = new GameObject("ConfirmSave", typeof(RectTransform));
        _confirmRoot.transform.SetParent(_root.transform, false);
        var rt = _confirmRoot.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var dim = MakePanel(_confirmRoot.transform, "Dim", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0, 0, 0, 0.55f));
        dim.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;

        var box = MakePanel(_confirmRoot.transform, "Box", V2(0.5f), V2(0.5f), Vector2.zero, new Vector2(420f, 220f),
            new Color(0.12f, 0.15f, 0.20f, 0.98f));
        MakeText(box, "Q", "是否保存卡组修改？", 28, new Vector2(0f, 50f), new Vector2(380f, 48f)).alignment =
            TextAlignmentOptions.Center;

        MakeButton(box, "Yes", "是", new Vector2(-90f, -50f), new Vector2(120f, 48f), () => _ = ConfirmSaveAsync(true));
        MakeButton(box, "No", "否", new Vector2(90f, -50f), new Vector2(120f, 48f), () => _ = ConfirmSaveAsync(false));
        _confirmRoot.SetActive(false);
    }

    private void HideConfirm()
    {
        if (_confirmRoot != null) _confirmRoot.SetActive(false);
    }

    private void MakeTab(Card.CardType type, string title, Color accent)
    {
        var go = new GameObject("Tab_" + title, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(_tabRow, false);
        go.GetComponent<LayoutElement>().preferredHeight = 44f;
        var img = go.GetComponent<Image>();
        img.color = new Color(0.16f, 0.20f, 0.26f, 1f);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var captured = type;
        btn.onClick.AddListener(() => SelectTab(captured));

        var label = MakeText(go.transform, "L", title, 24, Vector2.zero, new Vector2(120f, 36f));
        label.alignment = TextAlignmentOptions.Center;
        var lrt = label.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        _tabBgs.Add(img);
        _tabLabels.Add(label);
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
                _tabLabels[i].color = selected ? Color.white : new Color(0.75f, 0.8f, 0.85f, 1f);
        }

        RebuildCardRows();
    }

    private async Task LoadAsync()
    {
        var net = GameNetwork.Instance;
        if (net == null || string.IsNullOrEmpty(net.Token))
        {
            SetStatus("请先登录");
            return;
        }

        try
        {
            var ups = await AuthClient.ListUpgradesAsync(net.AuthHost, net.AuthPort, net.Token);
            if (ups.Ok) CardUpgradeCache.ApplyList(ups);

            var snap = await AuthClient.GetDeckAsync(net.AuthHost, net.AuthPort, net.Token);
            if (!snap.Ok)
            {
                SetStatus(Translate(snap.Message));
                return;
            }

            _draft.Clear();
            _draft.AddRange(snap.Deck);
            _baseline.Clear();
            _baseline.AddRange(snap.Deck);
            _owned = new HashSet<string>(snap.Owned);
            CardUpgradeCache.SetOwned(snap.Owned);
            ApplyToLocalDeck();
            UpdateCountLabel();
            RebuildCardRows();
            SetStatus("");
        }
        catch (System.Exception ex) { SetStatus(ex.Message); }
    }

    private void OnEditOrExitClicked()
    {
        if (_editing) OnExitEditClicked();
        else
        {
            _editing = true;
            UpdateEditButtonLabel();
            RebuildCardRows();
            SetStatus("编辑中：用 +/- 调整数量");
        }
    }

    private void OnExitEditClicked()
    {
        if (!_editing) return;
        if (IsDraftSameAsBaseline())
        {
            _editing = false;
            UpdateEditButtonLabel();
            RebuildCardRows();
            SetStatus("");
            return;
        }

        if (_confirmRoot != null) _confirmRoot.SetActive(true);
    }

    private bool IsDraftSameAsBaseline()
    {
        if (_draft.Count != _baseline.Count) return false;
        var a = new Dictionary<string, int>();
        foreach (var k in _draft)
        {
            a.TryGetValue(k, out var n);
            a[k] = n + 1;
        }

        var b = new Dictionary<string, int>();
        foreach (var k in _baseline)
        {
            b.TryGetValue(k, out var n);
            b[k] = n + 1;
        }

        if (a.Count != b.Count) return false;
        foreach (var kv in a)
        {
            if (!b.TryGetValue(kv.Key, out var n) || n != kv.Value) return false;
        }

        return true;
    }

    private async Task ConfirmSaveAsync(bool save)
    {
        HideConfirm();
        if (save)
        {
            if (_draft.Count < MinSize || _draft.Count > MaxSize)
            {
                SetStatus($"卡组需 {MinSize}-{MaxSize} 张");
                return;
            }

            await SaveAsync();
        }
        else
        {
            _draft.Clear();
            _draft.AddRange(_baseline);
            ApplyToLocalDeck();
            SetStatus("已取消修改");
        }

        _editing = false;
        UpdateEditButtonLabel();
        UpdateCountLabel();
        RebuildCardRows();
    }

    private async Task SaveAsync()
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
            var snap = await AuthClient.SaveDeckAsync(net.AuthHost, net.AuthPort, net.Token, _draft.ToArray());
            if (!snap.Ok)
            {
                SetStatus(Translate(snap.Message));
                return;
            }

            _draft.Clear();
            _draft.AddRange(snap.Deck);
            _baseline.Clear();
            _baseline.AddRange(snap.Deck);
            _owned = new HashSet<string>(snap.Owned);
            CardUpgradeCache.SetOwned(snap.Owned);
            ApplyToLocalDeck();
            SetStatus("已保存");
        }
        catch (System.Exception ex) { SetStatus(ex.Message); }
        finally { _busy = false; }
    }

    private void ApplyToLocalDeck()
    {
        var deck = PlayerDeck.Instance ?? Object.FindFirstObjectByType<PlayerDeck>();
        deck?.SetDeckFromKeys(_draft);
    }

    private void UpdateEditButtonLabel()
    {
        if (_editBtnLabel == null) return;
        _editBtnLabel.text = _editing ? "退出编辑" : "编辑卡组";
        ChineseFontBootstrap.ApplyChineseFont(_editBtnLabel);
    }

    private void UpdateCountLabel()
    {
        if (_countLabel == null) return;
        _countLabel.text = $"{_draft.Count} / {MaxSize}";
        ChineseFontBootstrap.ApplyChineseFont(_countLabel);
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

    private int CountInDraft(string key)
    {
        var n = 0;
        foreach (var k in _draft) if (k == key) n++;
        return n;
    }

    private void RebuildCardRows()
    {
        if (_cardList == null) return;
        for (var i = _cardList.childCount - 1; i >= 0; i--)
            Object.Destroy(_cardList.GetChild(i).gameObject);

        EnsureCardIndex();
        UpdateCountLabel();

        var cards = new List<Card>();
        foreach (var kv in _byName)
        {
            if (kv.Value.cardType != _currentType) continue;
            // Show owned cards; also show if already in deck
            if (_owned.Count > 0 && !_owned.Contains(kv.Key) && CountInDraft(kv.Key) == 0)
                continue;
            cards.Add(kv.Value);
        }

        cards.Sort((a, b) => string.CompareOrdinal(a.GetDisplayName(), b.GetDisplayName()));
        foreach (var card in cards) CreateCardRow(card);
    }

    private void CreateCardRow(Card card)
    {
        var key = card.cardName;
        var count = CountInDraft(key);
        var level = CardUpgradeCache.GetLevel(key);
        var maxCopies = CardUpgradeCache.MaxCopiesAllowed(level);

        var row = MakePanel(_cardList, "Row_" + key, V2(0.5f), V2(0.5f), Vector2.zero, new Vector2(760f, 108f),
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

        var info = MakeText(row, "Info",
            $"{card.GetDisplayName()}\nLv.{level} · 上限{maxCopies}",
            22, Vector2.zero, new Vector2(360f, 80f));
        info.alignment = TextAlignmentOptions.Left;
        var irt = info.rectTransform;
        irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
        irt.pivot = new Vector2(0f, 0.5f);
        irt.anchoredPosition = new Vector2(110f, 0f);

        // Click left area (art + info) for card detail; +/- stay independent.
        var hit = new GameObject("DetailHit", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        hit.transform.SetParent(row, false);
        var hrt = hit.GetComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0f, 0f);
        hrt.anchorMax = new Vector2(1f, 1f);
        hrt.offsetMin = new Vector2(0f, 0f);
        hrt.offsetMax = new Vector2(-180f, 0f);
        var himg = hit.GetComponent<Image>();
        himg.color = new Color(1f, 1f, 1f, 0.001f);
        himg.raycastTarget = true;
        var hbtn = hit.GetComponent<Button>();
        hbtn.transition = Selectable.Transition.None;
        hbtn.targetGraphic = himg;
        var captured = card;
        hbtn.onClick.AddListener(() => CardDetailPopupUI.Show(captured));

        // Count controls: [-] N [+]
        var ctrl = new GameObject("CountCtrl", typeof(RectTransform));
        ctrl.transform.SetParent(row, false);
        var crt = ctrl.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = new Vector2(1f, 0.5f);
        crt.pivot = new Vector2(1f, 0.5f);
        crt.anchoredPosition = new Vector2(-18f, 0f);
        crt.sizeDelta = new Vector2(170f, 48f);
        var hlg = ctrl.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleRight;
        hlg.spacing = 8f;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        var minusBtn = MakeSmallBtn(ctrl.transform, "Minus", "−", () => ChangeCount(key, -1));
        minusBtn.gameObject.SetActive(_editing);

        var numGo = new GameObject("Num", typeof(RectTransform));
        numGo.transform.SetParent(ctrl.transform, false);
        var nrt = numGo.GetComponent<RectTransform>();
        nrt.sizeDelta = new Vector2(44f, 40f);
        var num = numGo.AddComponent<TextMeshProUGUI>();
        num.text = count.ToString();
        num.fontSize = 28;
        num.alignment = TextAlignmentOptions.Center;
        num.color = count > 0 ? Color.white : new Color(0.7f, 0.75f, 0.8f, 1f);
        num.raycastTarget = false;
        ChineseFontBootstrap.ApplyChineseFont(num);

        var plusBtn = MakeSmallBtn(ctrl.transform, "Plus", "+", () => ChangeCount(key, +1));
        plusBtn.gameObject.SetActive(_editing);
    }

    private void ChangeCount(string key, int delta)
    {
        if (!_editing || _busy) return;

        if (delta > 0)
        {
            if (_draft.Count >= MaxSize)
            {
                SetStatus($"最多 {MaxSize} 张");
                return;
            }

            if (!_owned.Contains(key))
            {
                SetStatus("尚未拥有该卡");
                return;
            }

            var max = CardUpgradeCache.MaxCopiesAllowed(CardUpgradeCache.GetLevel(key));
            if (CountInDraft(key) >= max)
            {
                SetStatus("同名卡已达上限");
                return;
            }

            _draft.Add(key);
        }
        else
        {
            var idx = _draft.LastIndexOf(key);
            if (idx < 0) return;
            if (_draft.Count <= MinSize)
            {
                SetStatus($"至少保留 {MinSize} 张");
                return;
            }

            _draft.RemoveAt(idx);
        }

        UpdateCountLabel();
        RebuildCardRows();
    }

    private Coroutine _statusCo;

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
        "deck size 5-12" => "卡组需 5-12 张",
        "card not owned" => "尚未拥有该卡",
        "unknown card" => "未知卡牌",
        "too many copies" => "同名卡已达上限",
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

    private static Button MakeButton(Transform parent, string name, string label, Vector2 pos, Vector2 size,
        UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        go.GetComponent<Image>().color = new Color(0.18f, 0.45f, 0.72f, 1f);
        var btn = go.GetComponent<Button>();
        btn.onClick.AddListener(onClick);
        var t = MakeText(go.transform, "L", label, 22, Vector2.zero, size);
        t.alignment = TextAlignmentOptions.Center;
        var trt = t.rectTransform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        return btn;
    }

    private static Button MakeSmallBtn(Transform parent, string name, string label, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(40f, 40f);
        go.GetComponent<Image>().color = new Color(0.22f, 0.40f, 0.58f, 1f);
        var btn = go.GetComponent<Button>();
        btn.onClick.AddListener(onClick);
        var t = MakeText(go.transform, "L", label, 28, Vector2.zero, new Vector2(40f, 40f));
        t.alignment = TextAlignmentOptions.Center;
        var trt = t.rectTransform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        return btn;
    }
}
