using System.Collections.Generic;
using CardBattle.Network;
using DG.Tweening;
using MyProjectF.Assets.Scripts.Cards;
using MyProjectF.Assets.Scripts.Managers;
using MyProjectF.Assets.Scripts.Player;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Server-authoritative 1v1 battle: reuses PvE player / card visuals for both sides.
/// </summary>
public class OnlineBattleController : MonoBehaviour
{
    private static OnlineBattleController _boot;
    private static OnlineBattleController _active;

    public static bool AllowPlayerInput { get; private set; }
    public static OnlineBattleController Active => _active;

    /// <summary>World-space aim point for attack arc (opponent chest).</summary>
    public static Vector3? GetOpponentAimWorldPoint()
    {
        if (_active?._opponentStats == null)
        {
            return null;
        }

        var rt = _active._opponentStats.transform as RectTransform;
        if (rt == null)
        {
            return _active._opponentStats.transform.position;
        }

        return rt.TransformPoint(new Vector3(0f, 40f, 0f));
    }

    private GameNetwork _net;
    private BattleStateView _state;
    private bool _ended;
    private bool _spawned;
    private PlayerStats _opponentStats;
    private PlayerDisplay _opponentDisplay;
    private GameObject _hudGo;
    private TMP_Text _status;
    private TMP_Text _eventText;
    private TMP_Text _nameBanner;
    private ushort[] _lastHand;
    private ushort _lastTurnNo;
    private uint _lastTurnUid;
    private bool _playLocked;
    private int _prevSelfHp = -1;
    private int _prevOppHp = -1;
    private int _prevSelfArmor = -1;
    private int _prevOppArmor = -1;
    private int _prevSelfEnergy = -1;
    private Coroutine _unlockRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallBoot()
    {
        if (_boot != null)
        {
            return;
        }

        var host = new GameObject("OnlineBattleBoot");
        Object.DontDestroyOnLoad(host);
        _boot = host.AddComponent<OnlineBattleController>();
        _boot.enabled = false;
        SceneManager.sceneLoaded += (_, __) => EnsureInBattleScene();
    }

    public static void EnsureInBattleScene(string sceneName = null)
    {
        sceneName ??= SceneManager.GetActiveScene().name;
        if (!OnlineSession.Active || sceneName != "Battle1")
        {
            return;
        }

        if (_active != null)
        {
            return;
        }

        var go = new GameObject("OnlineBattleController");
        go.AddComponent<OnlineBattleController>();
    }

    private void Start()
    {
        if (this == _boot || name == "OnlineBattleBoot")
        {
            return;
        }

        if (!OnlineSession.Active)
        {
            Destroy(gameObject);
            return;
        }

        _active = this;
        PrepareLocalSystems();
        SpawnFighters();
        BuildLightHud();
        BindNetwork();
        // Apply any state that arrived during scene load before we subscribed.
        if (_net != null && _net.LastBattleState != null)
        {
            OnBattleUpdate(_net.LastBattleState);
        }

        _net?.SendBattleReady();
        SetStatus("等待对手就绪...");
        Debug.Log("[OnlineBattle] visual controller started, BattleReady sent");
    }

    private void OnDestroy()
    {
        if (this == _boot)
        {
            return;
        }

        if (_active == this)
        {
            _active = null;
        }

        AllowPlayerInput = false;
        UnbindNetwork();
        if (_hudGo != null)
        {
            Destroy(_hudGo);
        }
    }

    private void PrepareLocalSystems()
    {
        var tm = FindAnyObjectByType<TurnManager>();
        if (tm != null)
        {
            tm.enabled = false;
        }

        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            var n = t.name;
            if (n == "DeckButton" || n == "MenuButton")
            {
                t.gameObject.SetActive(false);
            }
        }
    }

    private void SpawnFighters()
    {
        if (_spawned)
        {
            return;
        }

        _spawned = true;

        var pm = PlayerManager.Instance;
        if (pm != null)
        {
            pm.InitializePlayer();
            _opponentStats = pm.SpawnOpponentVisual(OnlineSession.OpponentName);
            if (_opponentStats != null)
            {
                _opponentDisplay = _opponentStats.GetComponentInChildren<PlayerDisplay>();
                _opponentDisplay?.BindLocalStats(_opponentStats);
            }
            else
            {
                Debug.LogError("[OnlineBattle] failed to spawn opponent human visual.");
            }
        }
    }

    private void BindNetwork()
    {
        _net = GameNetwork.Instance;
        if (_net == null)
        {
            SetStatus("网络未连接");
            return;
        }

        _net.OnBattleStart += OnBattleUpdate;
        _net.OnBattleState += OnBattleUpdate;
        _net.OnBattleEnd += OnBattleEnd;
        _net.OnError += msg => SetStatus("错误: " + msg);
        _net.OnDisconnected += msg => SetStatus(msg);
    }

    private void UnbindNetwork()
    {
        if (_net == null)
        {
            return;
        }

        _net.OnBattleStart -= OnBattleUpdate;
        _net.OnBattleState -= OnBattleUpdate;
        _net.OnBattleEnd -= OnBattleEnd;
    }

    private void OnBattleUpdate(BattleStateView state)
    {
        if (state == null || state.SelfUid == 0)
        {
            Debug.LogWarning("[OnlineBattle] ignore invalid battle state");
            return;
        }

        if (_net != null && _net.Uid != 0 && state.SelfUid != _net.Uid)
        {
            Debug.LogWarning($"[OnlineBattle] ignore state for other uid self={state.SelfUid} me={_net.Uid}");
            return;
        }

        _state = state;
        _playLocked = false;
        SyncFighterStats();
        RefreshHud();

        // Rejected plays (e.g. energy) must restore hand even if card ids look unchanged.
        bool forceHand = !string.IsNullOrEmpty(state.LastEvent)
            && (state.LastEvent.Contains("能量不足")
                || state.LastEvent.Contains("not enough energy")
                || state.LastEvent.Contains("出牌失败")
                || state.LastEvent.Contains("非法出牌"));

        // Only rebuild hand when content/turn actually changes — avoids killing mid-drag cards.
        if (forceHand || ShouldRebuildHand(state))
        {
            RebuildHand();
            _lastHand = state.Hand != null ? (ushort[])state.Hand.Clone() : null;
            _lastTurnNo = state.TurnNo;
            _lastTurnUid = state.TurnUid;
        }
    }

    private bool ShouldRebuildHand(BattleStateView state)
    {
        if (state.Hand == null)
        {
            return false;
        }

        if (_lastHand == null || _lastHand.Length != state.Hand.Length)
        {
            return true;
        }

        if (_lastTurnNo != state.TurnNo || _lastTurnUid != state.TurnUid)
        {
            return true;
        }

        for (var i = 0; i < state.Hand.Length; i++)
        {
            if (_lastHand[i] != state.Hand[i])
            {
                return true;
            }
        }

        return false;
    }

    private void OnBattleEnd(BattleEndResult result)
    {
        _ended = true;
        AllowPlayerInput = false;
        var win = result.WinnerUid == (_net?.Uid ?? 0);
        SetStatus((win ? "你赢了！ " : "你输了！ ") + result.Message);
    }

    private void SyncFighterStats()
    {
        if (_state == null)
        {
            return;
        }

        // Floating damage / armor break feedback from HP deltas.
        if (_prevSelfHp >= 0)
        {
            int selfLost = _prevSelfHp - _state.SelfHp;
            if (selfLost > 0)
            {
                PlayerStats.Instance?.playerDisplay?.ShowDamagePopup(selfLost);
            }
            else if (_prevSelfArmor > _state.SelfArmor && _state.SelfHp == _prevSelfHp)
            {
                int blocked = _prevSelfArmor - _state.SelfArmor;
                if (blocked > 0)
                {
                    PlayerStats.Instance?.playerDisplay?.ShowDamagePopup(blocked);
                }
            }
        }

        if (_prevOppHp >= 0 && _opponentDisplay != null)
        {
            int oppLost = _prevOppHp - _state.OppHp;
            if (oppLost > 0)
            {
                _opponentDisplay.ShowDamagePopup(oppLost);
                PunchOpponentHit();
            }
            else if (_prevOppArmor > _state.OppArmor && _state.OppHp == _prevOppHp)
            {
                int blocked = _prevOppArmor - _state.OppArmor;
                if (blocked > 0)
                {
                    _opponentDisplay.ShowDamagePopup(blocked);
                    PunchOpponentHit();
                }
            }
        }

        _prevSelfHp = _state.SelfHp;
        _prevOppHp = _state.OppHp;
        _prevSelfArmor = _state.SelfArmor;
        _prevOppArmor = _state.OppArmor;

        var ps = PlayerStats.Instance;
        if (ps != null)
        {
            int energyDelta = 0;
            if (_prevSelfEnergy >= 0)
            {
                energyDelta = _state.SelfEnergy - _prevSelfEnergy;
            }

            if (ps.MaxHealth != _state.SelfMaxHp)
            {
                ps.InitializeStats(_state.SelfMaxHp, 0);
            }

            ps.SetCurrentHealth(_state.SelfHp);
            ps.SetArmor(_state.SelfArmor);
            ps.energy = _state.SelfEnergy;
            ps.initialEnergy = _state.SelfMaxEnergy;
            ps.GainEnergy(0);

            if (energyDelta > 0)
            {
                ps.playerDisplay?.ShowEnergyGainEffect(energyDelta);
            }
            else if (energyDelta < 0)
            {
                ps.playerDisplay?.ShowEnergySpendEffect(-energyDelta);
            }

            _prevSelfEnergy = _state.SelfEnergy;
        }

        if (_opponentStats != null)
        {
            if (_opponentStats.MaxHealth != _state.OppMaxHp)
            {
                _opponentStats.InitializeStats(_state.OppMaxHp, 0);
            }

            _opponentStats.SetCurrentHealth(_state.OppHp);
            _opponentStats.SetArmor(_state.OppArmor);
            _opponentStats.energy = _state.OppEnergy;
            _opponentStats.initialEnergy = _state.OppMaxEnergy;
            _opponentDisplay?.BindLocalStats(_opponentStats);
            _opponentDisplay?.UpdatePlayerUI();
        }

        if (_nameBanner != null)
        {
            _nameBanner.text = $"{_state.SelfName}  VS  {_state.OppName}";
        }
    }

    private void PunchOpponentHit()
    {
        if (_opponentStats == null)
        {
            return;
        }

        var visual = _opponentStats.characterVisualTransform;
        if (visual == null)
        {
            return;
        }

        var rt = visual as RectTransform ?? visual.GetComponent<RectTransform>();
        if (rt == null)
        {
            return;
        }

        DG.Tweening.DOTween.Kill(rt);
        rt.DOPunchScale(Vector3.one * 0.12f, 0.28f, 8, 0.6f);
    }

    private void RebuildHand()
    {
        var hm = HandManager.Instance;
        if (hm == null || hm.cardPrefab == null || hm.handTransform == null || _state?.Hand == null)
        {
            return;
        }

        var existing = new List<GameObject>(hm.CardsInHand);
        foreach (var go in existing)
        {
            hm.RemoveCardFromHandVisualOnly(go);
        }

        for (var i = 0; i < _state.Hand.Length; i++)
        {
            var card = OnlineCardCatalog.Get(_state.Hand[i]);
            if (card == null)
            {
                continue;
            }

            var go = Object.Instantiate(hm.cardPrefab, hm.handTransform, false);
            var display = go.GetComponent<CardDisplay>();
            display?.SetData(card);

            var movement = go.GetComponent<CardMovement>();
            if (movement != null)
            {
                movement.cardData = card;
                movement.isInHand = true;
            }

            var slot = go.GetComponent<OnlineHandSlot>() ?? go.AddComponent<OnlineHandSlot>();
            slot.HandIndex = (byte)i;
            slot.isPlayed = false;

            hm.AddCardToHand(go);
        }
    }

    private void RefreshHud()
    {
        if (_state == null)
        {
            return;
        }

        var myTurn = !_ended && !_playLocked && _state.TurnUid == _state.SelfUid;
        AllowPlayerInput = myTurn;

        if (_eventText != null)
        {
            _eventText.text =
                $"回合 {_state.TurnNo}  |  {(myTurn ? "你的回合" : "对手回合")}\n" +
                $"你 {_state.SelfHp}/{_state.SelfMaxHp}  护甲{_state.SelfArmor}  能{_state.SelfEnergy}  |  " +
                $"敌 {_state.OppHp}/{_state.OppMaxHp}  护甲{_state.OppArmor}\n{_state.LastEvent}";
        }

        SetStatus(myTurn ? "拖出或单击手牌出牌；点结束回合" : "等待对手操作...");
    }

    /// <summary>Called when local client sends a play to avoid double-send until state returns.</summary>
    public static void LockInputUntilState()
    {
        if (_active == null)
        {
            return;
        }

        _active._playLocked = true;
        AllowPlayerInput = false;
        if (_active._unlockRoutine != null)
        {
            _active.StopCoroutine(_active._unlockRoutine);
        }

        _active._unlockRoutine = _active.StartCoroutine(_active.UnlockIfNoState(3.5f));
    }

    private System.Collections.IEnumerator UnlockIfNoState(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        if (_playLocked && !_ended)
        {
            Debug.LogWarning("[OnlineBattle] play lock timed out — unlocking input");
            _playLocked = false;
            RefreshHud();
        }

        _unlockRoutine = null;
    }

    private void BuildLightHud()
    {
        _hudGo = new GameObject("OnlineBattleHud");
        var canvas = _hudGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = _hudGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        _hudGo.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(_hudGo);

        // VS names: top-left, no background box (avoids overlapping center HUD).
        _nameBanner = CreateAnchoredLabel(
            _hudGo.transform,
            "VS",
            new Vector2(0f, 1f),
            new Vector2(18f, -14f),
            new Vector2(520f, 36f),
            22,
            TextAlignmentOptions.Left);

        // Event / status: top-center. Event is multi-line; keep status below it.
        _eventText = CreateAnchoredLabel(
            _hudGo.transform,
            "事件",
            new Vector2(0.5f, 1f),
            new Vector2(0f, -12f),
            new Vector2(820f, 96f),
            18,
            TextAlignmentOptions.Top);

        _status = CreateAnchoredLabel(
            _hudGo.transform,
            "状态",
            new Vector2(0.5f, 1f),
            new Vector2(0f, -118f),
            new Vector2(820f, 36f),
            20,
            TextAlignmentOptions.Center);

        _eventText.raycastTarget = false;
        _status.raycastTarget = false;
        _nameBanner.raycastTarget = false;

        CreateCornerButton(_hudGo.transform, "返回主菜单", new Vector2(1f, 1f), new Vector2(-18f, -14f), () =>
        {
            OnlineSession.Clear();
            UnbindNetwork();
            AllowPlayerInput = false;
            if (_hudGo != null)
            {
                Destroy(_hudGo);
                _hudGo = null;
            }

            Destroy(gameObject);
            if (SceneFlowManager.Instance != null)
            {
                SceneFlowManager.Instance.LoadMainMenu();
            }
            else
            {
                SceneManager.LoadScene("MainMenu");
            }
        });

        ChineseFontBootstrap.ApplyToScene();
    }

    private static TMP_Text CreateAnchoredLabel(
        Transform parent,
        string text,
        Vector2 anchor,
        Vector2 anchoredPos,
        Vector2 size,
        int fontSize,
        TextAlignmentOptions align)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;
        var label = go.AddComponent<TextMeshProUGUI>();
        ChineseFontBootstrap.ApplyChineseFont(label);
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = align;
        label.color = Color.white;
        label.outlineWidth = 0.2f;
        label.outlineColor = new Color32(0, 0, 0, 180);
        return label;
    }

    private void SetStatus(string msg)
    {
        if (_status != null)
        {
            _status.text = msg;
        }

        Debug.Log("[OnlineBattle] " + msg);
    }

    private static TMP_Text CreateLabel(Transform parent, string text, Vector2 pos, int fontSize)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(940f, 36f);
        rect.anchoredPosition = pos;
        var label = go.AddComponent<TextMeshProUGUI>();
        ChineseFontBootstrap.ApplyChineseFont(label);
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }

    private static Button CreateCornerButton(
        Transform parent,
        string label,
        Vector2 anchor,
        Vector2 anchoredPos,
        UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label + "Btn");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.sizeDelta = new Vector2(180f, 48f);
        rect.anchoredPosition = anchoredPos;
        var image = go.AddComponent<Image>();
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
        ChineseFontBootstrap.ApplyChineseFont(text);
        text.text = label;
        text.fontSize = 22f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return button;
    }

    private static Button CreateButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label + "Btn");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(180f, 52f);
        rect.anchoredPosition = pos;
        var image = go.AddComponent<Image>();
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
        ChineseFontBootstrap.ApplyChineseFont(text);
        text.text = label;
        text.fontSize = 22f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return button;
    }
}
