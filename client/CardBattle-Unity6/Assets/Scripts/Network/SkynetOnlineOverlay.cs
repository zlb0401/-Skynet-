using System;
using CardBattle.Network;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// MainMenu gate: centered login first, then reveal Start / Match / Settings / Logout / Quit Game.
/// </summary>
public class SkynetOnlineOverlay : MonoBehaviour
{
    private static TMP_FontAsset _font;

    private GameObject _loginRoot;
    private GameObject _registerRoot;
    private GameObject _canvasGo;
    private TMP_InputField _usernameInput;
    private TMP_InputField _passwordInput;
    private TMP_InputField _regUsernameInput;
    private TMP_InputField _regPasswordInput;
    private TMP_InputField _regConfirmInput;
    private TMP_InputField _regCaptchaInput;
    private TMP_Text _statusText;
    private TMP_Text _regStatusText;
    private TMP_Text _regCaptchaQuestion;
    private TMP_Text _welcomeText;
    private Button _loginButton;
    private Button _openRegisterButton;
    private Button _registerSubmitButton;
    private Button _registerCancelButton;
    private Button _refreshCaptchaButton;
    private string _captchaId = string.Empty;
    private GameObject _mainMenuButtons;
    private Button _matchButton;
    private Button _logoutButton;
    private GameNetwork _network;
    private bool _busy;
    private bool _loggedIn;
    private string _username = "test";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindAnyObjectByType<SkynetOnlineOverlay>() != null)
        {
            return;
        }

        var host = new GameObject("SkynetOnlineOverlay");
        DontDestroyOnLoad(host);
        host.AddComponent<SkynetOnlineOverlay>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        DetachNetworkHandlers();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        WalletHudUI.Instance?.SetVisible(false);

        if (scene.name != "MainMenu")
        {
            if (_loginRoot != null)
            {
                _loginRoot.SetActive(false);
            }

            return;
        }

        CacheMainMenuButtons();
        EnsureLoginUi();
        AttachNetworkHandlers();

        _loggedIn = _network != null && _network.IsConnected && _network.Uid != 0;
        ApplyGateState(_loggedIn
            ? $"已登录 {_username}（uid={_network.Uid}）"
            : "请先登录，再开始游戏或匹配");
    }

    private void CacheMainMenuButtons()
    {
        // GameObject.Find ignores inactive objects — keep existing ref after we hide the menu.
        if (_mainMenuButtons == null)
        {
            _mainMenuButtons = GameObject.Find("MainMenuButtons");
        }

        if (_mainMenuButtons == null)
        {
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == "MainMenuButtons" && t.gameObject.scene.IsValid())
                {
                    _mainMenuButtons = t.gameObject;
                    break;
                }
            }
        }

        RenameQuitButton();
        EnsureExtraMenuButtons();
        StyleAllMenuButtons();
    }

    private void RenameQuitButton()
    {
        Transform quitTf = null;
        if (_mainMenuButtons != null)
        {
            quitTf = _mainMenuButtons.transform.Find("Quit");
        }

        var quit = quitTf != null ? quitTf.gameObject : GameObject.Find("Quit");
        if (quit == null)
        {
            return;
        }

        var label = quit.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = "退出游戏";
            ChineseFontBootstrap.ApplyChineseFont(label);
        }
    }

    private void EnsureExtraMenuButtons()
    {
        if (_mainMenuButtons == null)
        {
            return;
        }

        // Always rebuild so size/hover match StartGame (avoids leftover runtime buttons).
        var oldMatch = _mainMenuButtons.transform.Find("MatchButton");
        if (oldMatch != null)
        {
            oldMatch.name = "MatchButton_OLD";
            Destroy(oldMatch.gameObject);
        }

        var oldLogout = _mainMenuButtons.transform.Find("LogoutButton");
        if (oldLogout != null)
        {
            oldLogout.name = "LogoutButton_OLD";
            Destroy(oldLogout.gameObject);
        }

        // Remove legacy text UpgradeButton if a previous build left it in the vertical list.
        var oldUpgrade = _mainMenuButtons.transform.Find("UpgradeButton")
            ?? _mainMenuButtons.transform.Find("UpgradeButton_OLD");
        if (oldUpgrade != null)
        {
            Destroy(oldUpgrade.gameObject);
        }

        _matchButton = CloneOriginalMenuButton("MatchButton", "匹配对战", OnMatchClicked);
        _logoutButton = CloneOriginalMenuButton("LogoutButton", "退出登录", OnLogoutClicked);
    }

    /// <summary>
    /// Clone StartGame/Quit so size, shadow and MenuButtonEffects hover match exactly.
    /// VerticalLayoutGroup on MainMenuButtons places them automatically.
    /// </summary>
    private Button CloneOriginalMenuButton(string name, string label, UnityEngine.Events.UnityAction onClick)
    {
        var template = _mainMenuButtons.transform.Find("StartGame")
            ?? _mainMenuButtons.transform.Find("Quit")
            ?? _mainMenuButtons.transform.Find("Options");
        if (template == null)
        {
            return CreateMenuButton(_mainMenuButtons.transform, name, label, Vector2.zero, onClick);
        }

        var go = Instantiate(template.gameObject, _mainMenuButtons.transform);
        go.name = name;
        go.transform.localScale = Vector3.one;
        go.SetActive(true);

        var tmp = go.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
        {
            tmp.text = label;
            ChineseFontBootstrap.ApplyChineseFont(tmp);
        }

        var button = go.GetComponent<Button>();
        if (button == null)
        {
            button = go.AddComponent<Button>();
        }

        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(onClick);
        EnsureMenuButtonEffects(go);
        return button;
    }

    private static void EnsureMenuButtonEffects(GameObject go)
    {
        if (go == null)
        {
            return;
        }

        var fx = go.GetComponent<MenuButtonEffects>();
        if (fx == null)
        {
            fx = go.AddComponent<MenuButtonEffects>();
        }

        var shadow = go.transform.Find("ShadowImage")?.GetComponent<Image>();
        if (shadow != null)
        {
            fx.shadowImage = shadow;
        }

        go.transform.localScale = Vector3.one;
    }

    /// <summary>
    /// Scene buttons use transparent Image (a=0) so only text shows on the moon.
    /// Force a solid panel + white bold text for all menu entries.
    /// </summary>
    private void StyleAllMenuButtons()
    {
        if (_mainMenuButtons == null)
        {
            return;
        }

        string[] names = { "StartGame", "Options", "Quit", "MatchButton", "LogoutButton" };
        foreach (var name in names)
        {
            var t = _mainMenuButtons.transform.Find(name);
            if (t != null)
            {
                ApplySolidButtonStyle(t.gameObject);
            }
        }
    }

    private static Sprite _uiWhiteSprite;

    private static Sprite UiWhiteSprite
    {
        get
        {
            if (_uiWhiteSprite != null)
            {
                return _uiWhiteSprite;
            }

            var tex = Texture2D.whiteTexture;
            _uiWhiteSprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f);
            return _uiWhiteSprite;
        }
    }

    private static void ApplySolidButtonStyle(GameObject go)
    {
        EnsureMenuButtonEffects(go);

        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            // Match original StartGame size (300x60).
            rt.sizeDelta = new Vector2(300f, 60f);
        }

        var img = go.GetComponent<Image>();
        if (img != null)
        {
            if (img.sprite == null)
            {
                img.sprite = UiWhiteSprite;
            }

            img.type = Image.Type.Sliced;
            // Keep readable on bright moon, but leave ColorTint to MenuButtonEffects scale hover.
            img.color = new Color(0.07f, 0.11f, 0.18f, 0.88f);
            img.raycastTarget = true;
        }

        var button = go.GetComponent<Button>();
        if (button != null && img != null)
        {
            button.targetGraphic = img;
            // Prefer Animation/None so MenuButtonEffects owns hover feel; keep mild tint.
            button.transition = Selectable.Transition.ColorTint;
            var c = button.colors;
            c.normalColor = Color.white;
            c.highlightedColor = new Color(0.85f, 0.92f, 1f, 1f);
            c.pressedColor = new Color(0.75f, 0.82f, 0.95f, 1f);
            c.selectedColor = Color.white;
            c.fadeDuration = 0.08f;
            button.colors = c;
        }

        var shadow = go.transform.Find("ShadowImage");
        if (shadow != null)
        {
            var simg = shadow.GetComponent<Image>();
            if (simg != null)
            {
                if (simg.sprite == null)
                {
                    simg.sprite = UiWhiteSprite;
                }

                var sc = simg.color;
                sc.a = 0f; // MenuButtonEffects fades this in on hover
                simg.color = sc;
            }
        }

        foreach (var tmp in go.GetComponentsInChildren<TMP_Text>(true))
        {
            try
            {
                tmp.color = Color.white;
                tmp.fontStyle = FontStyles.Bold;
                if (tmp.fontSize < 28f)
                {
                    tmp.fontSize = 32f;
                }

                ChineseFontBootstrap.ApplyChineseFont(tmp);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[SkynetOnline] style button text failed: " + ex.Message);
            }
        }
    }

    private void EnsureLoginUi()
    {
        // Rebuild if old dual-button layout without register popup.
        if (_loginRoot != null && _registerRoot != null)
        {
            StartCoroutine(RefreshInputCaretsNextFrame());
            return;
        }

        if (_canvasGo != null)
        {
            Destroy(_canvasGo);
            _canvasGo = null;
            _loginRoot = null;
            _registerRoot = null;
            _usernameInput = null;
            _passwordInput = null;
            _regUsernameInput = null;
            _regPasswordInput = null;
            _regConfirmInput = null;
            _statusText = null;
            _regStatusText = null;
            _welcomeText = null;
            _loginButton = null;
            _openRegisterButton = null;
            _registerSubmitButton = null;
            _registerCancelButton = null;
            _refreshCaptchaButton = null;
            _regCaptchaInput = null;
            _regCaptchaQuestion = null;
            _captchaId = string.Empty;
        }

        _ = DefaultFont;

        _canvasGo = new GameObject("SkynetLoginCanvas");
        var canvas = _canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5200;
        var scaler = _canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        _canvasGo.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(_canvasGo);

        var dim = new GameObject("Dim");
        dim.transform.SetParent(_canvasGo.transform, false);
        var dimRt = dim.AddComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;
        var dimImg = dim.AddComponent<Image>();
        dimImg.color = new Color(0.02f, 0.03f, 0.05f, 0.55f);
        dimImg.raycastTarget = true;

        BuildLoginPanel();
        BuildRegisterPopup();
        ChineseFontBootstrap.ApplyToScene();
        StartCoroutine(RefreshInputCaretsNextFrame());
    }

    private void BuildLoginPanel()
    {
        _loginRoot = new GameObject("LoginPanel");
        _loginRoot.transform.SetParent(_canvasGo.transform, false);
        var panelRect = _loginRoot.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(460f, 380f);
        panelRect.anchoredPosition = Vector2.zero;

        var bg = _loginRoot.AddComponent<Image>();
        bg.sprite = UiWhiteSprite;
        bg.color = new Color(0.08f, 0.1f, 0.14f, 0.96f);

        CreateLabel(_loginRoot.transform, "卡牌对战 — 登录", new Vector2(0f, 130f), 30);
        _welcomeText = CreateLabel(_loginRoot.transform, "使用账号登录后进入主菜单", new Vector2(0f, 88f), 18);
        _usernameInput = CreateInput(_loginRoot.transform, "账号", new Vector2(0f, 20f), "test");
        _passwordInput = CreateInput(_loginRoot.transform, "密码", new Vector2(0f, -40f), "123456", true);
        _loginButton = CreateButton(_loginRoot.transform, "登录", new Vector2(0f, -110f), OnLoginClicked, new Vector2(220f, 48f));
        _openRegisterButton = CreateButton(_loginRoot.transform, "注册账号", new Vector2(0f, -168f), OpenRegisterPopup, new Vector2(220f, 40f));
        _statusText = CreateLabel(_loginRoot.transform, "", new Vector2(0f, -215f), 18);
    }

    private void BuildRegisterPopup()
    {
        _registerRoot = new GameObject("RegisterPanel");
        _registerRoot.transform.SetParent(_canvasGo.transform, false);
        var panelRect = _registerRoot.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(500f, 520f);
        panelRect.anchoredPosition = Vector2.zero;

        var bg = _registerRoot.AddComponent<Image>();
        bg.sprite = UiWhiteSprite;
        bg.color = new Color(0.07f, 0.09f, 0.13f, 0.98f);

        CreateLabel(_registerRoot.transform, "注册新账号", new Vector2(0f, 210f), 30);
        CreateLabel(_registerRoot.transform, "账号 3-16 位 · 密码 6-32 位", new Vector2(0f, 170f), 16);
        _regUsernameInput = CreateInput(_registerRoot.transform, "新账号", new Vector2(0f, 110f), "");
        _regPasswordInput = CreateInput(_registerRoot.transform, "设置密码", new Vector2(0f, 50f), "", true);
        _regConfirmInput = CreateInput(_registerRoot.transform, "确认密码", new Vector2(0f, -10f), "", true);

        _regCaptchaQuestion = CreateLabel(_registerRoot.transform, "验证码：加载中…", new Vector2(-60f, -70f), 20);
        _regCaptchaQuestion.alignment = TextAlignmentOptions.Left;
        var qRect = _regCaptchaQuestion.GetComponent<RectTransform>();
        qRect.sizeDelta = new Vector2(260f, 40f);
        qRect.anchoredPosition = new Vector2(-40f, -70f);

        _refreshCaptchaButton = CreateButton(_registerRoot.transform, "换一题", new Vector2(160f, -70f), () => _ = RefreshCaptchaAsync(), new Vector2(120f, 40f));
        _regCaptchaInput = CreateInput(_registerRoot.transform, "验证码答案", new Vector2(0f, -125f), "");

        _registerSubmitButton = CreateButton(_registerRoot.transform, "确认注册", new Vector2(-115f, -190f), OnRegisterSubmitClicked, new Vector2(200f, 48f));
        _registerCancelButton = CreateButton(_registerRoot.transform, "返回登录", new Vector2(115f, -190f), CloseRegisterPopup, new Vector2(200f, 48f));
        _regStatusText = CreateLabel(_registerRoot.transform, "", new Vector2(0f, -245f), 18);

        _registerRoot.SetActive(false);
    }

    private async void OpenRegisterPopup()
    {
        if (_busy)
        {
            return;
        }

        if (_registerRoot != null)
        {
            _registerRoot.SetActive(true);
        }

        if (_loginRoot != null)
        {
            _loginRoot.SetActive(false);
        }

        if (_regStatusText != null)
        {
            _regStatusText.text = "";
        }

        StartCoroutine(RefreshInputCaretsNextFrame());
        await RefreshCaptchaAsync();
    }

    private void CloseRegisterPopup()
    {
        if (_busy)
        {
            return;
        }

        if (_registerRoot != null)
        {
            _registerRoot.SetActive(false);
        }

        if (_loginRoot != null)
        {
            _loginRoot.SetActive(true);
        }

        StartCoroutine(RefreshInputCaretsNextFrame());
    }

    private async System.Threading.Tasks.Task RefreshCaptchaAsync()
    {
        if (_network == null)
        {
            SetRegStatus("未找到网络组件");
            return;
        }

        try
        {
            if (_regCaptchaQuestion != null)
            {
                _regCaptchaQuestion.text = "验证码：加载中…";
            }

            var challenge = await AuthClient.FetchCaptchaAsync(_network.AuthHost, _network.AuthPort);
            _captchaId = challenge.Id;
            if (_regCaptchaQuestion != null)
            {
                _regCaptchaQuestion.text = string.IsNullOrEmpty(challenge.Question)
                    ? "验证码：获取失败"
                    : "验证码：" + challenge.Question;
                ChineseFontBootstrap.ApplyChineseFont(_regCaptchaQuestion);
            }

            if (_regCaptchaInput != null)
            {
                _regCaptchaInput.text = "";
            }
        }
        catch (Exception ex)
        {
            _captchaId = string.Empty;
            if (_regCaptchaQuestion != null)
            {
                _regCaptchaQuestion.text = "验证码：获取失败";
            }

            SetRegStatus("验证码加载失败: " + TranslateAuthMessage(ex.Message));
            Debug.LogWarning("[SkynetOnline] captcha: " + ex.Message);
        }
    }

    private System.Collections.IEnumerator RefreshInputCaretsNextFrame()
    {
        // Wait until EventSystem + TMP internals are ready.
        yield return null;
        yield return null;

        // Do not bring login UI back after a successful login.
        if (_loggedIn)
        {
            yield break;
        }

        if (_loginRoot != null && _canvasGo != null && _canvasGo.activeSelf && _loginRoot.activeInHierarchy)
        {
            _loginRoot.SetActive(false);
            yield return null;
            if (_loggedIn)
            {
                yield break;
            }

            _loginRoot.SetActive(true);
        }

        if (_loggedIn)
        {
            yield break;
        }

        foreach (var input in new[]
                 {
                     _usernameInput, _passwordInput,
                     _regUsernameInput, _regPasswordInput, _regConfirmInput, _regCaptchaInput
                 })
        {
            if (input == null)
            {
                continue;
            }

            if (input.textComponent != null && input.textComponent.font == null)
            {
                input.textComponent.font = DefaultFont;
            }

            if (input.placeholder is TMP_Text ph && ph.font == null)
            {
                ph.font = DefaultFont;
            }

            input.customCaretColor = true;
            input.caretColor = Color.white;
            input.caretWidth = 3;
            input.caretBlinkRate = 0.85f;
            input.selectionColor = new Color(0.35f, 0.55f, 0.95f, 0.45f);
            input.enabled = false;
            input.enabled = true;
            input.ForceLabelUpdate();
        }
    }

    private void ApplyGateState(string status)
    {
        SetStatus(status);

        if (_loggedIn)
        {
            // Re-resolve in case Find failed earlier while inactive.
            if (_mainMenuButtons == null)
            {
                CacheMainMenuButtons();
            }

            if (_canvasGo != null)
            {
                _canvasGo.SetActive(false);
            }

            if (_registerRoot != null)
            {
                _registerRoot.SetActive(false);
            }

            if (_mainMenuButtons != null)
            {
                _mainMenuButtons.SetActive(true);
                StyleAllMenuButtons();
                Debug.Log("[SkynetOnline] main menu shown");
            }
            else
            {
                Debug.LogError("[SkynetOnline] MainMenuButtons not found after login");
            }

            if (_network != null && !string.IsNullOrEmpty(_network.Token))
            {
                _ = RefreshWalletAsync(_network.Token);
            }

            MainMenuCornerDockUI.Instance?.SetVisible(true);
        }
        else
        {
            if (_mainMenuButtons != null)
            {
                _mainMenuButtons.SetActive(false);
            }

            if (_canvasGo != null)
            {
                _canvasGo.SetActive(true);
            }

            if (_loginRoot != null)
            {
                _loginRoot.SetActive(true);
            }

            if (_registerRoot != null)
            {
                _registerRoot.SetActive(false);
            }

            WalletHudUI.Instance?.SetVisible(false);
            MainMenuCornerDockUI.Instance?.SetVisible(false);
            CardUpgradePanelUI.Instance?.Close();
            BagPanelUI.Instance?.Close();
            DeckEditPanelUI.Instance?.Close();
            GachaPanelUI.Instance?.Close();
        }
    }

    private void AttachNetworkHandlers()
    {
        _network = GameNetwork.Instance;
        if (_network == null)
        {
            SetStatus("未找到网络组件");
            return;
        }

        DetachNetworkHandlers();
        _network.OnLoginResult += HandleLoginResult;
        _network.OnMatchResult += HandleMatchResult;
        _network.OnError += HandleError;
        _network.OnDisconnected += HandleDisconnected;
    }

    private void DetachNetworkHandlers()
    {
        if (_network == null)
        {
            return;
        }

        _network.OnLoginResult -= HandleLoginResult;
        _network.OnMatchResult -= HandleMatchResult;
        _network.OnError -= HandleError;
        _network.OnDisconnected -= HandleDisconnected;
    }

    private async void OnLoginClicked()
    {
        if (_busy || _network == null)
        {
            return;
        }

        // Already logged in (e.g. previous session) — just show menu.
        if (_network.IsConnected && _network.Uid != 0)
        {
            _loggedIn = true;
            ApplyGateState($"已登录 {_username}");
            try
            {
                CacheMainMenuButtons();
                if (_mainMenuButtons != null)
                {
                    _mainMenuButtons.SetActive(true);
                    StyleAllMenuButtons();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[SkynetOnline] show menu failed: " + ex);
            }

            return;
        }

        await RunCppAuthThenEnterSkynet(isRegister: false);
    }

    private async void OnRegisterSubmitClicked()
    {
        if (_busy || _network == null)
        {
            return;
        }

        if (_network.IsConnected && _network.Uid != 0)
        {
            SetRegStatus("已登录，请先退出登录");
            return;
        }

        var user = _regUsernameInput != null ? _regUsernameInput.text.Trim() : string.Empty;
        var pass = _regPasswordInput != null ? _regPasswordInput.text : string.Empty;
        var confirm = _regConfirmInput != null ? _regConfirmInput.text : string.Empty;
        var captchaAnswer = _regCaptchaInput != null ? _regCaptchaInput.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
        {
            SetRegStatus("请输入账号和密码");
            return;
        }

        if (pass != confirm)
        {
            SetRegStatus("两次密码不一致");
            return;
        }

        if (string.IsNullOrEmpty(_captchaId) || string.IsNullOrEmpty(captchaAnswer))
        {
            SetRegStatus("请填写验证码");
            return;
        }

        if (_usernameInput != null)
        {
            _usernameInput.text = user;
        }

        if (_passwordInput != null)
        {
            _passwordInput.text = pass;
        }

        await RunCppAuthThenEnterSkynet(
            isRegister: true,
            usernameOverride: user,
            passwordOverride: pass,
            captchaId: _captchaId,
            captchaAnswer: captchaAnswer);
    }

    /// <summary>
    /// 1) C++ Auth :8889 register/login → token
    /// 2) Skynet :8888 TokenLogin → enter game session
    /// </summary>
    private async System.Threading.Tasks.Task RunCppAuthThenEnterSkynet(
        bool isRegister,
        string usernameOverride = null,
        string passwordOverride = null,
        string captchaId = null,
        string captchaAnswer = null)
    {
        _busy = true;
        SetAuthButtonsInteractable(false);

        _username = !string.IsNullOrEmpty(usernameOverride)
            ? usernameOverride
            : (_usernameInput != null ? _usernameInput.text.Trim() : string.Empty);
        var password = !string.IsNullOrEmpty(passwordOverride)
            ? passwordOverride
            : (_passwordInput != null ? _passwordInput.text : string.Empty);
        if (string.IsNullOrEmpty(_username) || string.IsNullOrEmpty(password))
        {
            _busy = false;
            SetAuthButtonsInteractable(true);
            if (isRegister)
            {
                SetRegStatus("请输入账号和密码");
            }
            else
            {
                SetStatus("请输入账号和密码");
            }

            return;
        }

        var action = isRegister ? "注册" : "登录";
        try
        {
            if (isRegister)
            {
                SetRegStatus("连接鉴权服务中...");
            }
            else
            {
                SetStatus("连接鉴权服务中...");
            }

            var authHost = _network.AuthHost;
            var authPort = _network.AuthPort;
            LoginResult authResult = isRegister
                ? await AuthClient.RegisterAsync(authHost, authPort, _username, password, captchaId, captchaAnswer)
                : await AuthClient.LoginAsync(authHost, authPort, _username, password);

            if (!authResult.Ok)
            {
                HandleAuthResult(authResult, action);
                if (isRegister)
                {
                    await RefreshCaptchaAsync();
                }

                return;
            }

            if (isRegister)
            {
                SetRegStatus("进入游戏服务...");
            }
            else
            {
                SetStatus("进入游戏服务...");
            }

            if (!_network.IsConnected)
            {
                await _network.ConnectAsync();
            }

            if (isRegister)
            {
                SetRegStatus($"{action}确认中...");
            }
            else
            {
                SetStatus($"{action}确认中...");
            }

            _network.SendTokenLogin(authResult.Token);
            StartCoroutine(AuthTimeoutWatchdog(8f, action));
            _ = RefreshWalletAsync(authResult.Token);
        }
        catch (Exception ex)
        {
            _busy = false;
            SetAuthButtonsInteractable(true);
            var msg = $"{action}失败: " + TranslateAuthMessage(ex.Message);
            if (isRegister)
            {
                SetRegStatus(msg);
                await RefreshCaptchaAsync();
            }
            else
            {
                SetStatus(msg);
            }

            Debug.LogError("[SkynetOnline] cpp auth flow: " + ex);
        }
    }

    private async System.Threading.Tasks.Task RefreshWalletAsync(string token)
    {
        if (_network == null || string.IsNullOrEmpty(token))
        {
            return;
        }

        try
        {
            var inv = await AuthClient.FetchInventoryAsync(_network.AuthHost, _network.AuthPort, token);
            if (inv.Ok)
            {
                CardUpgradeCache.SetWallet(inv.Gold, inv.Dust, inv.Diamond, inv.Ticket);
                WalletHudUI.Instance?.SetBalances(inv.Gold, inv.Dust, inv.Diamond, inv.Ticket, _loggedIn);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[SkynetOnline] wallet: " + ex.Message);
        }

        try
        {
            var ups = await AuthClient.ListUpgradesAsync(_network.AuthHost, _network.AuthPort, token);
            if (ups.Ok)
            {
                CardUpgradeCache.ApplyList(ups);
                WalletHudUI.Instance?.SetBalances(ups.Gold, ups.Dust, ups.Diamond, ups.Ticket, _loggedIn);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[SkynetOnline] upgrades: " + ex.Message);
        }

        try
        {
            var deck = await AuthClient.GetDeckAsync(_network.AuthHost, _network.AuthPort, token);
            if (deck.Ok)
            {
                var pd = PlayerDeck.Instance ?? FindFirstObjectByType<PlayerDeck>();
                pd?.SetDeckFromKeys(deck.Deck);
                CardUpgradeCache.SetOwned(deck.Owned);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[SkynetOnline] deck: " + ex.Message);
        }
    }

    private void OnUpgradeClicked()
    {
        // Kept for compatibility; entry is now the bottom-left corner dock.
        MainMenuCornerDockUI.Instance?.SetVisible(_loggedIn);
        if (CardUpgradePanelUI.Instance == null)
        {
            var go = new GameObject("CardUpgradePanelHost");
            go.AddComponent<CardUpgradePanelUI>();
        }

        CardUpgradePanelUI.Instance.Open();
    }

    private void SetRegStatus(string msg)
    {
        if (_regStatusText != null)
        {
            _regStatusText.text = msg ?? "";
            ChineseFontBootstrap.ApplyChineseFont(_regStatusText);
        }
    }

    private void SetAuthButtonsInteractable(bool interactable)
    {
        if (_loginButton != null)
        {
            _loginButton.interactable = interactable;
        }

        if (_openRegisterButton != null)
        {
            _openRegisterButton.interactable = interactable;
        }

        if (_registerSubmitButton != null)
        {
            _registerSubmitButton.interactable = interactable;
        }

        if (_registerCancelButton != null)
        {
            _registerCancelButton.interactable = interactable;
        }

        if (_refreshCaptchaButton != null)
        {
            _refreshCaptchaButton.interactable = interactable;
        }
    }

    private System.Collections.IEnumerator AuthTimeoutWatchdog(float seconds, string actionLabel)
    {
        yield return new WaitForSecondsRealtime(seconds);
        if (!_busy || _loggedIn)
        {
            yield break;
        }

        _busy = false;
        SetAuthButtonsInteractable(true);

        // Server may have accepted auth but UI callback failed — recover if uid set.
        if (_network != null && _network.Uid != 0)
        {
            _loggedIn = true;
            ApplyGateState($"欢迎 {_username}");
            try
            {
                CacheMainMenuButtons();
                if (_mainMenuButtons != null)
                {
                    _mainMenuButtons.SetActive(true);
                    StyleAllMenuButtons();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[SkynetOnline] timeout recover menu failed: " + ex);
            }

            ShowToast($"{actionLabel}已完成");
            yield break;
        }

        SetStatus($"{actionLabel}超时，请重试（服务器可能未响应）");
        if (actionLabel == "注册")
        {
            SetRegStatus("注册超时，请重试（鉴权服务可能未响应）");
        }
    }

    private void OnMatchClicked()
    {
        if (_network == null)
        {
            SetStatus("未找到网络组件");
            return;
        }

        if (!_network.IsConnected || _network.Uid == 0)
        {
            _loggedIn = false;
            ApplyGateState("请先登录");
            return;
        }

        // Lightweight toast via temporary status canvas
        ShowToast("匹配中...（需两人同时点匹配）");
        _network.SendMatch();
    }

    private void OnLogoutClicked()
    {
        // Flip gate first so disconnect callbacks cannot fight the UI.
        OnlineSession.Clear();
        CardUpgradeCache.Clear();
        CardUpgradePanelUI.Instance?.Close();
        BagPanelUI.Instance?.Close();
        DeckEditPanelUI.Instance?.Close();
        GachaPanelUI.Instance?.Close();
        MainMenuCornerDockUI.Instance?.SetVisible(false);
        _loggedIn = false;
        _busy = false;
        SetAuthButtonsInteractable(true);

        if (_canvasGo == null)
        {
            EnsureLoginUi();
        }

        ApplyGateState("已退出登录，请重新登录");
        StartCoroutine(RefreshInputCaretsNextFrame());

        // Disconnect last: player builds can block if Close runs on the main thread.
        try
        {
            _network?.Disconnect();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[SkynetOnline] disconnect on logout: " + ex.Message);
        }
    }

    private void HandleLoginResult(LoginResult result) => HandleAuthResult(result, "登录");

    private void HandleAuthResult(LoginResult result, string actionLabel)
    {
        _busy = false;
        SetAuthButtonsInteractable(true);
        var isRegister = actionLabel == "注册";

        if (!result.Ok)
        {
            _loggedIn = false;
            var msg = $"{actionLabel}失败: " + TranslateAuthMessage(result.Message);
            if (_canvasGo != null)
            {
                _canvasGo.SetActive(true);
            }

            if (isRegister)
            {
                if (_registerRoot != null)
                {
                    _registerRoot.SetActive(true);
                }

                if (_loginRoot != null)
                {
                    _loginRoot.SetActive(false);
                }

                SetRegStatus(msg);
            }
            else
            {
                if (_loginRoot != null)
                {
                    _loginRoot.SetActive(true);
                }

                if (_registerRoot != null)
                {
                    _registerRoot.SetActive(false);
                }

                SetStatus(msg);
            }

            return;
        }

        _loggedIn = true;
        if (_registerRoot != null)
        {
            _registerRoot.SetActive(false);
        }

        try
        {
            _network?.SendHeartbeat();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[SkynetOnline] heartbeat after auth failed: " + ex.Message);
        }

        ApplyGateState($"欢迎 {_username}");

        try
        {
            CacheMainMenuButtons();
            if (_mainMenuButtons != null)
            {
                _mainMenuButtons.SetActive(true);
                StyleAllMenuButtons();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[SkynetOnline] build main menu after auth failed: " + ex);
        }

        ShowToast($"{actionLabel}成功：{_username}");
    }

    private static string TranslateAuthMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return "未知错误";
        }

        return message switch
        {
            "username is empty" => "账号不能为空",
            "password is empty" => "密码不能为空",
            "username length 3-16" => "账号长度需 3-16 位",
            "username only letters, digits, underscore" => "账号只能包含字母、数字、下划线",
            "password length 6-32" => "密码长度需 6-32 位",
            "username already exists" => "账号已存在",
            "not enough dust" => "粉尘不足",
            "not enough ticket" => "招募券不足",
            "not enough diamond" => "钻石不足",
            "already max level" => "已达最高等级",
            "unknown card" => "不可升级的卡牌",
            "invalid username or password" => "账号或密码错误",
            "register failed" => "注册失败，请稍后重试",
            "captcha invalid or expired" => "验证码错误或已过期",
            "invalid or expired token" => "登录态已失效，请重新登录",
            "use C++ Auth service :8889" => "请使用注册弹窗（走鉴权服务）",
            "token is empty" => "缺少登录凭证",
            _ => message.Contains("Connection refused") || message.Contains("积极拒绝")
                ? "连不上鉴权服务(8889)，请确认服务已启动"
                : message
        };
    }

    private void HandleMatchResult(MatchResult result)
    {
        if (!result.Ok)
        {
            ShowToast("匹配: " + result.Message);
            return;
        }

        ShowToast($"匹配成功 房间={result.RoomId} 对手={result.OpponentName}");
        OnlineSession.Begin(result);
        GameSession.Instance?.ResetStats();

        if (SceneFlowManager.Instance != null)
        {
            SceneFlowManager.Instance.LoadScene(SceneType.Battle1);
        }
        else
        {
            SceneManager.LoadScene("Battle1");
        }
    }

    private void HandleError(string msg) => ShowToast("错误: " + msg);

    private void HandleDisconnected(string msg)
    {
        // Intentional logout already refreshed the gate; avoid stomping the login form.
        if (!_loggedIn)
        {
            return;
        }

        _loggedIn = false;
        ApplyGateState(string.IsNullOrEmpty(msg) ? "连接已断开" : msg);
    }

    private void SetStatus(string msg)
    {
        if (_statusText != null)
        {
            _statusText.text = msg;
            ChineseFontBootstrap.ApplyChineseFont(_statusText);
        }

        if (_welcomeText != null && !_loggedIn)
        {
            // keep subtitle
        }

        Debug.Log("[SkynetOnline] " + msg);
    }

    private void ShowToast(string msg)
    {
        Debug.Log("[SkynetOnline] " + msg);
        // Ephemeral top banner on main menu
        var existing = GameObject.Find("MainMenuToast");
        if (existing != null)
        {
            Destroy(existing);
        }

        var canvas = _mainMenuButtons != null
            ? _mainMenuButtons.GetComponentInParent<Canvas>()
            : null;
        if (canvas == null)
        {
            return;
        }

        var go = new GameObject("MainMenuToast");
        go.transform.SetParent(canvas.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -24f);
        rt.sizeDelta = new Vector2(720f, 48f);
        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.07f, 0.1f, 0.75f);
        bg.raycastTarget = false;
        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var tr = textGo.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;
        var text = textGo.AddComponent<TextMeshProUGUI>();
        ChineseFontBootstrap.ApplyChineseFont(text);
        text.text = msg;
        text.fontSize = 22f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        Destroy(go, 3.5f);
    }

    private static TMP_FontAsset DefaultFont
    {
        get
        {
            if (_font == null)
            {
                _font = ChineseFontBootstrap.EnsureFont();
            }

            if (_font == null)
            {
                _font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            }

            if (_font == null)
            {
                _font = TMP_Settings.defaultFontAsset;
            }

            return _font;
        }
    }

    private static TMP_Text CreateLabel(Transform parent, string text, Vector2 pos, int fontSize)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(420f, 40f);
        rect.anchoredPosition = pos;
        var label = go.AddComponent<TextMeshProUGUI>();
        label.font = DefaultFont;
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        return label;
    }

    private static TMP_InputField CreateInput(Transform parent, string placeholder, Vector2 pos, string defaultValue, bool password = false)
    {
        var go = new GameObject(placeholder + "Input");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(360f, 44f);
        rect.anchoredPosition = pos;

        var bg = go.AddComponent<Image>();
        bg.sprite = UiWhiteSprite;
        bg.color = new Color(0.15f, 0.17f, 0.22f, 1f);
        bg.raycastTarget = true;

        // Standard TMP structure: Text Area + mask so caret / selection render correctly.
        var areaGo = new GameObject("Text Area");
        areaGo.transform.SetParent(go.transform, false);
        var areaRect = areaGo.AddComponent<RectTransform>();
        areaRect.anchorMin = Vector2.zero;
        areaRect.anchorMax = Vector2.one;
        areaRect.offsetMin = new Vector2(12f, 6f);
        areaRect.offsetMax = new Vector2(-12f, -6f);
        areaGo.AddComponent<RectMask2D>();

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(areaGo.transform, false);
        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.font = DefaultFont;
        text.fontSize = 22f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;

        var phGo = new GameObject("Placeholder");
        phGo.transform.SetParent(areaGo.transform, false);
        var phRect = phGo.AddComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = Vector2.zero;
        phRect.offsetMax = Vector2.zero;
        var ph = phGo.AddComponent<TextMeshProUGUI>();
        ph.font = DefaultFont;
        ph.text = placeholder;
        ph.fontSize = 22f;
        ph.fontStyle = FontStyles.Italic;
        ph.color = new Color(1f, 1f, 1f, 0.4f);
        ph.alignment = TextAlignmentOptions.MidlineLeft;
        ph.raycastTarget = false;

        var input = go.AddComponent<TMP_InputField>();
        input.textViewport = areaRect;
        input.textComponent = text;
        input.placeholder = ph;
        input.targetGraphic = bg;
        input.text = defaultValue;
        input.caretBlinkRate = 0.85f;
        input.caretWidth = 3;
        input.customCaretColor = true;
        input.caretColor = Color.white;
        input.selectionColor = new Color(0.35f, 0.55f, 0.95f, 0.45f);
        input.shouldHideMobileInput = true;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.richText = false;
        input.transition = Selectable.Transition.ColorTint;
        var colors = input.colors;
        colors.normalColor = new Color(0.15f, 0.17f, 0.22f, 1f);
        colors.highlightedColor = new Color(0.20f, 0.24f, 0.32f, 1f);
        colors.selectedColor = new Color(0.22f, 0.28f, 0.40f, 1f);
        colors.pressedColor = new Color(0.18f, 0.22f, 0.30f, 1f);
        input.colors = colors;

        if (password)
        {
            input.contentType = TMP_InputField.ContentType.Password;
        }

        return input;
    }

    private static Button CreateButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick, Vector2? size = null)
    {
        var go = new GameObject(label + "Button");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = size ?? new Vector2(160f, 44f);
        rect.anchoredPosition = pos;

        var image = go.AddComponent<Image>();
        image.sprite = UiWhiteSprite;
        image.color = new Color(0.22f, 0.45f, 0.78f, 1f);

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.font = DefaultFont;
        text.text = label;
        text.fontSize = 24f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;

        return button;
    }

    private Button CreateMenuButton(Transform parent, string name, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(360f, 74f);
        rect.anchoredPosition = pos;

        var image = go.AddComponent<Image>();
        image.sprite = UiWhiteSprite;
        image.color = new Color(0.07f, 0.11f, 0.18f, 0.92f);

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        // Optional shadow like other menu buttons
        var shadow = new GameObject("ShadowImage");
        shadow.transform.SetParent(go.transform, false);
        shadow.transform.SetAsFirstSibling();
        var srt = shadow.AddComponent<RectTransform>();
        srt.anchorMin = Vector2.zero;
        srt.anchorMax = Vector2.one;
        srt.offsetMin = new Vector2(6f, -6f);
        srt.offsetMax = new Vector2(6f, -6f);
        var simg = shadow.AddComponent<Image>();
        simg.color = new Color(0f, 0f, 0f, 0.35f);
        simg.raycastTarget = false;

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.font = DefaultFont;
        text.text = label;
        text.fontSize = 32f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        ChineseFontBootstrap.ApplyChineseFont(text);

        return button;
    }
}
