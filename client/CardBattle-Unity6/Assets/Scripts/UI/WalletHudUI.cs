using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Top-right wallet chips: gold / dust / diamond / ticket.
/// </summary>
public class WalletHudUI : MonoBehaviour
{
    public static WalletHudUI Instance { get; private set; }

    private GameObject _root;
    private TMP_Text _goldCount;
    private TMP_Text _dustCount;
    private TMP_Text _diamondCount;
    private TMP_Text _ticketCount;
    private GameObject _tooltip;
    private TMP_Text _tooltipText;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (Instance != null) return;
        var host = new GameObject("WalletHudUI");
        DontDestroyOnLoad(host);
        host.AddComponent<WalletHudUI>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Build();
        SetVisible(false);
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    private void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        HideTooltip();
        if (scene.name != "MainMenu") SetVisible(false);
    }

    public void SetBalances(int gold, int dust, bool showOnMainMenu)
    {
        SetBalances(gold, dust, CardUpgradeCache.LastDiamond, CardUpgradeCache.LastTicket, showOnMainMenu);
    }

    public void SetBalances(int gold, int dust, int diamond, int ticket, bool showOnMainMenu)
    {
        if (_goldCount != null) _goldCount.text = gold.ToString();
        if (_dustCount != null) _dustCount.text = dust.ToString();
        if (_diamondCount != null) _diamondCount.text = diamond.ToString();
        if (_ticketCount != null) _ticketCount.text = ticket.ToString();
        var onMain = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenu";
        SetVisible(showOnMainMenu && onMain);
    }

    public void SetVisible(bool visible)
    {
        if (_root == null) return;
        _root.SetActive(visible);
        if (!visible) HideTooltip();
    }

    private void Build()
    {
        _root = new GameObject("WalletHudCanvas");
        _root.transform.SetParent(transform, false);
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5300;
        var scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        _root.AddComponent<GraphicRaycaster>();
        _root.AddComponent<CanvasGroup>();

        var row = new GameObject("Row");
        row.transform.SetParent(_root.transform, false);
        var rowRt = row.AddComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(1f, 1f);
        rowRt.anchorMax = new Vector2(1f, 1f);
        rowRt.pivot = new Vector2(1f, 1f);
        rowRt.anchoredPosition = new Vector2(-28f, -20f);
        rowRt.sizeDelta = new Vector2(620f, 64f);
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleRight;
        hlg.spacing = 12f;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        _goldCount = CreateChip(row.transform, "GoldChip", "UI/icon_gold", "金币", new Color(0.95f, 0.78f, 0.22f, 1f));
        _dustCount = CreateChip(row.transform, "DustChip", "UI/icon_dust", "粉尘", new Color(0.55f, 0.45f, 0.95f, 1f));
        _diamondCount = CreateChip(row.transform, "DiamondChip", "UI/icon_diamond", "钻石", new Color(0.45f, 0.75f, 1f, 1f));
        _ticketCount = CreateChip(row.transform, "TicketChip", "UI/icon_ticket", "招募券", new Color(0.95f, 0.55f, 0.35f, 1f));

        _tooltip = new GameObject("Tooltip");
        _tooltip.transform.SetParent(_root.transform, false);
        var tipRt = _tooltip.AddComponent<RectTransform>();
        tipRt.sizeDelta = new Vector2(120f, 36f);
        tipRt.pivot = new Vector2(0.5f, 0f);
        var tipBg = _tooltip.AddComponent<Image>();
        tipBg.color = new Color(0.05f, 0.06f, 0.09f, 0.92f);
        tipBg.raycastTarget = false;
        _tooltipText = CreateTmp(_tooltip.transform, "", 18);
        var tRt = _tooltipText.rectTransform;
        tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
        tRt.offsetMin = tRt.offsetMax = Vector2.zero;
        _tooltip.SetActive(false);
    }

    private TMP_Text CreateChip(Transform parent, string name, string resourcePath, string tipName, Color accent)
    {
        var chip = new GameObject(name);
        chip.transform.SetParent(parent, false);
        var rt = chip.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(132f, 56f);

        var bg = chip.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.1f, 0.14f, 0.75f);
        bg.raycastTarget = true;

        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(chip.transform, false);
        var iconRt = iconGo.AddComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0f, 0.5f);
        iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot = new Vector2(0f, 0.5f);
        iconRt.anchoredPosition = new Vector2(8f, 0f);
        iconRt.sizeDelta = new Vector2(40f, 40f);
        var iconImg = iconGo.AddComponent<Image>();
        iconImg.raycastTarget = false;
        var tex = Resources.Load<Texture2D>(resourcePath);
        if (tex != null)
        {
            iconImg.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            iconImg.preserveAspect = true;
        }
        else
        {
            iconImg.color = accent;
            iconImg.sprite = MakeCircleSprite(accent);
        }

        var count = CreateTmp(chip.transform, "0", 24);
        var cRt = count.rectTransform;
        cRt.anchorMin = new Vector2(0f, 0f);
        cRt.anchorMax = new Vector2(1f, 1f);
        cRt.offsetMin = new Vector2(52f, 4f);
        cRt.offsetMax = new Vector2(-6f, -4f);
        count.alignment = TextAlignmentOptions.Left;
        count.fontStyle = FontStyles.Bold;
        count.color = Color.white;
        ChineseFontBootstrap.ApplyChineseFont(count);

        var tip = chip.AddComponent<WalletChipHover>();
        tip.Setup(tipName, ShowTooltip, HideTooltip, chip.transform as RectTransform);
        return count;
    }

    private void ShowTooltip(string name, RectTransform chip)
    {
        if (_tooltip == null || chip == null) return;
        _tooltip.SetActive(true);
        if (_tooltipText != null)
        {
            _tooltipText.text = name;
            ChineseFontBootstrap.ApplyChineseFont(_tooltipText);
        }
        var tipRt = _tooltip.transform as RectTransform;
        var canvas = _root.GetComponent<Canvas>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            RectTransformUtility.WorldToScreenPoint(null, chip.position),
            null, out var local);
        tipRt.anchoredPosition = local + new Vector2(-40f, -42f);
    }

    private void HideTooltip()
    {
        if (_tooltip != null) _tooltip.SetActive(false);
    }

    private static TMP_Text CreateTmp(Transform parent, string text, int size)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var label = go.AddComponent<TextMeshProUGUI>();
        label.text = text; label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white; label.raycastTarget = false;
        if (ChineseFontBootstrap.EnsureFont() != null)
            ChineseFontBootstrap.ApplyChineseFont(label);
        return label;
    }

    private static Sprite MakeCircleSprite(Color c)
    {
        const int s = 64;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        var cx = (s - 1) * 0.5f;
        for (var y = 0; y < s; y++)
        for (var x = 0; x < s; x++)
        {
            var dx = x - cx; var dy = y - cx;
            var d = Mathf.Sqrt(dx * dx + dy * dy);
            tex.SetPixel(x, y, d <= cx - 1f ? c : Color.clear);
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
    }
}

public class WalletChipHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private string _name;
    private System.Action<string, RectTransform> _show;
    private System.Action _hide;
    private RectTransform _rt;

    public void Setup(string name, System.Action<string, RectTransform> show, System.Action hide, RectTransform rt)
    {
        _name = name; _show = show; _hide = hide; _rt = rt;
    }

    public void OnPointerEnter(PointerEventData eventData) => _show?.Invoke(_name, _rt);
    public void OnPointerExit(PointerEventData eventData) => _hide?.Invoke();
    public void OnPointerClick(PointerEventData eventData) => _show?.Invoke(_name, _rt);
}
