using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;
using MyProjectF.Assets.Scripts.Player;

public class PlayerDisplay : MonoBehaviour
{
    private PlayerStats playerStats;

    [Header("UI Elements")]
    [SerializeField] private HealthBar playerHealthBar;
    [SerializeField] private TextMeshProUGUI armorText;
    [SerializeField] private TextMeshProUGUI energyText;
    [SerializeField] private Image armorImage;
    [SerializeField] private Image energyImage;
    [SerializeField] private GameObject floatingDamageTextPrefab;
    [SerializeField] private RectTransform textSpawnAnchor;

    private void Awake()
    {
        // Prefab may not have energyImage wired — resolve from EnergyText parent.
        if (energyImage == null && energyText != null)
        {
            energyImage = energyText.transform.parent != null
                ? energyText.transform.parent.GetComponent<Image>()
                : null;
        }
    }

    /// <summary>Bind a specific PlayerStats (self or opponent clone).</summary>
    public void BindLocalStats(PlayerStats stats)
    {
        playerStats = stats;
        UpdatePlayerUI();
    }

    private void Start()
    {
        if (playerStats == null)
        {
            playerStats = PlayerStats.Instance;
        }

        if (playerStats != null)
        {
            UpdatePlayerUI();
        }
        else
        {
            Logger.LogWarning("PlayerStats not found. UI will show fallback values.", this);
            SetFallbackDisplay();
        }
    }

    private void OnEnable()
    {
        PlayerStats.OnStatsChanged += OnAnyStatsChanged;
    }

    private void OnDisable()
    {
        PlayerStats.OnStatsChanged -= OnAnyStatsChanged;
    }

    private void OnAnyStatsChanged()
    {
        // Opponent clone has its own stats; only refresh if bound to the singleton self.
        if (playerStats != null && playerStats.IsOpponentVisual)
        {
            return;
        }

        if (playerStats == null)
        {
            playerStats = PlayerStats.Instance;
        }

        UpdatePlayerUI();
    }

    /// <summary>Updates the UI elements with the current player stats.</summary>
    public void UpdatePlayerUI()
    {
        if (playerStats == null)
        {
            SetFallbackDisplay();
            return;
        }

        if (playerHealthBar != null)
        {
            playerHealthBar.UpdateHealthBar(playerStats.CurrentHealth, playerStats.MaxHealth);
        }
        else
        {
            Logger.LogWarning("[PlayerDisplay] playerHealthBar is not assigned.", this);
        }

        if (armorText != null) armorText.text = $"{playerStats.Armor}";
        if (energyText != null) energyText.text = $"{playerStats.energy}";
    }

    /// <summary>Sets placeholder values when no PlayerStats is available.</summary>
    private void SetFallbackDisplay()
    {
        if (playerHealthBar != null)
            playerHealthBar.UpdateHealthBar(0, 0);

        if (armorText != null) armorText.text = "--";
        if (energyText != null) energyText.text = "--";
    }

    public void ShowArmorGainEffect()
    {
        if (armorImage == null) return;

        armorImage.rectTransform.DOKill();
        armorImage.rectTransform.DOPunchScale(Vector3.one * 0.25f, 0.3f, 8, 1.0f);

        if (armorText != null)
        {
            armorText.rectTransform.DOKill();
            armorText.rectTransform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 6, 0.8f);

            Color originalTextColor = armorText.color;
            armorText.DOColor(Color.cyan, 0.1f)
                     .SetLoops(2, LoopType.Yoyo)
                     .OnComplete(() => armorText.color = originalTextColor);
        }
    }

    /// <summary>Pulse energy icon/text when energy increases (e.g. 专注).</summary>
    public void ShowEnergyGainEffect(int amount = 1)
    {
        PunchEnergyVisual(new Color(0.45f, 1f, 0.55f), amount > 0 ? ("+" + amount) : "+");
    }

    /// <summary>Pulse energy icon/text when energy is spent or blocked.</summary>
    public void ShowEnergySpendEffect(int amount = 0)
    {
        PunchEnergyVisual(new Color(1f, 0.55f, 0.35f), amount > 0 ? ("-" + amount) : null);
    }

    /// <summary>Shake to show the card cannot be afforded.</summary>
    public void ShowEnergyDeniedEffect()
    {
        if (energyText != null)
        {
            energyText.rectTransform.DOKill();
            energyText.rectTransform.DOShakeAnchorPos(0.35f, 10f, 20, 90f, false, true);
            var original = energyText.color;
            energyText.DOColor(new Color(1f, 0.3f, 0.3f), 0.1f)
                .SetLoops(2, LoopType.Yoyo)
                .OnComplete(() => energyText.color = original);
        }

        if (energyImage != null)
        {
            energyImage.rectTransform.DOKill();
            energyImage.rectTransform.DOPunchScale(Vector3.one * 0.2f, 0.25f, 8, 0.8f);
        }
    }

    private void PunchEnergyVisual(Color flash, string floatingLabel)
    {
        if (energyImage != null)
        {
            energyImage.rectTransform.DOKill();
            energyImage.rectTransform.DOPunchScale(Vector3.one * 0.35f, 0.35f, 10, 1f);
        }

        if (energyText != null)
        {
            energyText.rectTransform.DOKill();
            energyText.rectTransform.DOPunchScale(Vector3.one * 0.35f, 0.35f, 8, 0.9f);
            var original = energyText.color;
            energyText.DOColor(flash, 0.12f)
                .SetLoops(2, LoopType.Yoyo)
                .OnComplete(() => energyText.color = original);
        }

        if (!string.IsNullOrEmpty(floatingLabel) && energyText != null)
        {
            SpawnEnergyFloater(floatingLabel, flash);
        }
    }

    private void SpawnEnergyFloater(string label, Color color)
    {
        var go = new GameObject("EnergyFloater");
        var parent = energyText.transform.parent != null ? energyText.transform.parent : energyText.transform;
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(36f, 24f);
        rect.sizeDelta = new Vector2(80f, 40f);
        var text = go.AddComponent<TextMeshProUGUI>();
        ChineseFontBootstrap.ApplyChineseFont(text);
        text.text = label;
        text.fontSize = 28f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.raycastTarget = false;

        var seq = DOTween.Sequence();
        seq.Append(rect.DOAnchorPosY(rect.anchoredPosition.y + 50f, 0.7f).SetEase(Ease.OutCubic));
        seq.Join(text.DOFade(0f, 0.7f));
        seq.OnComplete(() => Destroy(go));
    }

    /// <summary>Displays a floating damage text popup at the anchor position.</summary>
    public void ShowDamagePopup(int damage)
    {
        if (floatingDamageTextPrefab == null || textSpawnAnchor == null) return;

        GameObject go = Instantiate(floatingDamageTextPrefab, textSpawnAnchor);
        RectTransform rect = go.GetComponent<RectTransform>();
        CanvasGroup group = go.GetComponent<CanvasGroup>();
        TMP_Text text = go.GetComponentInChildren<TMP_Text>();

        if (text != null)
        {
            text.text = damage > 0 ? ("-" + damage) : damage.ToString();
            text.color = Color.white;
            ChineseFontBootstrap.ApplyChineseFont(text);
        }

        rect.localScale = Vector3.one * 1.6f;

        Sequence seq = DOTween.Sequence();

        seq.Append(rect.DOShakeScale(0.1f, 0.2f, 10));
        if (text != null)
        {
            seq.Append(text.DOColor(Color.white, 0.05f));
            seq.Append(text.DOColor(new Color32(0xFF, 0x7A, 0x7A, 255), 0.2f));
        }

        seq.Append(rect.DOScale(0.8f, 0.6f).SetEase(Ease.InOutQuad))
           .Join(rect.DOAnchorPosY(rect.anchoredPosition.y + 80f, 0.6f).SetEase(Ease.OutCubic));

        if (group != null)
            seq.Join(group.DOFade(0f, 0.6f));

        seq.AppendCallback(() => Destroy(go));
    }
}
