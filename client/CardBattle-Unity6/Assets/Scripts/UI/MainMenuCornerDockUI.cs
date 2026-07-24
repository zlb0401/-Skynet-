using CardBattle.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bottom-left main-menu dock: 卡牌 / 卡组 / 抽卡 / 背包.
/// </summary>
public class MainMenuCornerDockUI : MonoBehaviour
{
    public static MainMenuCornerDockUI Instance { get; private set; }

    private GameObject _root;
    private CanvasGroup _cg;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (Instance != null) return;
        var host = new GameObject("MainMenuCornerDockUI");
        DontDestroyOnLoad(host);
        host.AddComponent<MainMenuCornerDockUI>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Build();
        SetVisible(false);
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),
            UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    private void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
        UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (scene.name != "MainMenu") SetVisible(false);
    }

    public void SetVisible(bool visible)
    {
        if (_root == null) return;
        var onMain = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenu";
        var show = visible && onMain;
        _root.SetActive(show);
        if (_cg != null)
        {
            _cg.alpha = show ? 1f : 0f;
            _cg.blocksRaycasts = show;
            _cg.interactable = show;
        }
    }

    private void Build()
    {
        _root = new GameObject("CornerDock");
        _root.transform.SetParent(transform, false);
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5200;
        var scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        _root.AddComponent<GraphicRaycaster>();
        _cg = _root.AddComponent<CanvasGroup>();

        var dock = new GameObject("Dock", typeof(RectTransform));
        dock.transform.SetParent(_root.transform, false);
        var drt = dock.GetComponent<RectTransform>();
        drt.anchorMin = new Vector2(0f, 0f);
        drt.anchorMax = new Vector2(0f, 0f);
        drt.pivot = new Vector2(0f, 0f);
        drt.anchoredPosition = new Vector2(28f, 28f);
        drt.sizeDelta = new Vector2(480f, 120f);

        var hlg = dock.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 18f;
        hlg.childAlignment = TextAnchor.LowerLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        CreateIconButton(dock.transform, "CardsBtn", "UI/icon_cards", "卡牌", OnCardsClicked);
        CreateIconButton(dock.transform, "DeckBtn", "UI/icon_deck", "卡组", OnDeckClicked);
        CreateIconButton(dock.transform, "GachaBtn", "UI/icon_gacha", "抽卡", OnGachaClicked);
        CreateIconButton(dock.transform, "BagBtn", "UI/icon_bag", "背包", OnBagClicked);
    }

    private static void CreateIconButton(Transform parent, string name, string iconPath, string label,
        UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(96f, 118f);

        var bg = go.GetComponent<Image>();
        bg.color = new Color(0.08f, 0.10f, 0.14f, 0.55f);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = bg;
        btn.onClick.AddListener(onClick);

        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGo.transform.SetParent(go.transform, false);
        var irt = iconGo.GetComponent<RectTransform>();
        irt.anchorMin = new Vector2(0.5f, 1f);
        irt.anchorMax = new Vector2(0.5f, 1f);
        irt.pivot = new Vector2(0.5f, 1f);
        irt.anchoredPosition = new Vector2(0f, -6f);
        irt.sizeDelta = new Vector2(72f, 72f);
        var iconImg = iconGo.GetComponent<Image>();
        iconImg.raycastTarget = false;
        iconImg.preserveAspect = true;
        var tex = Resources.Load<Texture2D>(iconPath);
        if (tex != null)
        {
            iconImg.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            iconImg.color = Color.white;
        }
        else iconImg.color = new Color(0.35f, 0.55f, 0.75f, 1f);

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 0f);
        trt.anchorMax = new Vector2(1f, 0f);
        trt.pivot = new Vector2(0.5f, 0f);
        trt.anchoredPosition = new Vector2(0f, 6f);
        trt.sizeDelta = new Vector2(0f, 28f);
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 22;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white; tmp.raycastTarget = false;
        ChineseFontBootstrap.ApplyChineseFont(tmp);
    }

    private static bool NeedLogin()
    {
        var net = GameNetwork.Instance;
        return net == null || string.IsNullOrEmpty(net.Token);
    }

    private void OnCardsClicked()
    {
        if (NeedLogin()) { Debug.Log("[CornerDock] need login"); return; }
        if (CardUpgradePanelUI.Instance == null)
            new GameObject("CardUpgradePanelHost").AddComponent<CardUpgradePanelUI>();
        CardUpgradePanelUI.Instance.Open();
    }

    private void OnDeckClicked()
    {
        if (NeedLogin()) { Debug.Log("[CornerDock] need login"); return; }
        if (DeckEditPanelUI.Instance == null)
            new GameObject("DeckEditPanelHost").AddComponent<DeckEditPanelUI>();
        DeckEditPanelUI.Instance.Open();
    }

    private void OnGachaClicked()
    {
        if (NeedLogin()) { Debug.Log("[CornerDock] need login"); return; }
        if (GachaPanelUI.Instance == null)
            new GameObject("GachaPanelHost").AddComponent<GachaPanelUI>();
        GachaPanelUI.Instance.Open();
    }

    private void OnBagClicked()
    {
        if (BagPanelUI.Instance == null)
            new GameObject("BagPanelHost").AddComponent<BagPanelUI>();
        BagPanelUI.Instance.Open();
    }
}
