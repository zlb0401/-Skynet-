using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MyProjectF.Assets.Scripts.Cards;

/// <summary>
/// Visual wrapper for a single reward card choice (thumbnail + select button).
/// </summary>
public class RewardCardView : MonoBehaviour
{
    [Header("Thumbnail Prefab & Parent")]
    public Transform thumbnailParent;
    public GameObject cardThumbnailPrefab;

    [Header("UI")]
    public TMP_Text debugLabel;   // optional
    public Button button;         // overlay button on the root

    [HideInInspector] public RewardDefinition def;

    private System.Action<RewardCardView> onChosen;
    private GameObject spawnedThumb;

    public void Setup(RewardDefinition def, System.Action<RewardCardView> onChosen)
    {
        this.def = def;
        this.onChosen = onChosen;

        if (debugLabel) debugLabel.text = def.cardData ? def.cardData.GetDisplayName() : "(null)";

        EnsureRarityBadge(def);

        // Root sizing (safe defaults for a card-like container)
        var rt = GetComponent<RectTransform>();
        if (rt)
        {
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
            rt.sizeDelta = new Vector2(250, 350);
        }

        // Stretch thumbnail parent to fill the card
        if (thumbnailParent is RectTransform tr)
        {
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.pivot = new Vector2(0.5f, 0.5f);
            tr.anchoredPosition = Vector2.zero;
            tr.sizeDelta = Vector2.zero;
            tr.localScale = Vector3.one;
        }

        // Clear previous thumbnail
        if (spawnedThumb) Destroy(spawnedThumb);

        if (cardThumbnailPrefab && thumbnailParent)
        {
            spawnedThumb = Instantiate(cardThumbnailPrefab, thumbnailParent, false);

            // Keep the button on top
            if (button) button.transform.SetAsLastSibling();

            // Disable raycasts on all thumbnail graphics to avoid stealing clicks
            var graphics = spawnedThumb.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++) graphics[i].raycastTarget = false;

            // Stretch the thumbnail to fill the parent
            var thumbRT = spawnedThumb.GetComponent<RectTransform>();
            if (thumbRT)
            {
                thumbRT.anchorMin = Vector2.zero;
                thumbRT.anchorMax = Vector2.one;
                thumbRT.pivot = new Vector2(0.5f, 0.5f);
                thumbRT.anchoredPosition = Vector2.zero;
                thumbRT.sizeDelta = Vector2.zero;
                thumbRT.localScale = Vector3.one;
            }

            // Bind data to CardDisplay (use existing API)
            var display = spawnedThumb.GetComponentInChildren<CardDisplay>(true);
            if (display != null && def.cardData != null)
            {
                display.cardData = def.cardData;
                display.UpdateCardDisplay();
            }
            else
            {
                Logger.LogWarning("[Reward] CardDisplay not found or cardData missing.", this);
            }
        }

        // Full-rect overlay button
        if (button)
        {
            var brt = button.GetComponent<RectTransform>();
            if (brt)
            {
                brt.anchorMin = Vector2.zero;
                brt.anchorMax = Vector2.one;
                brt.pivot = new Vector2(0.5f, 0.5f);
                brt.anchoredPosition = Vector2.zero;
                brt.sizeDelta = Vector2.zero;
            }
            var img = button.GetComponent<Image>();
            if (img) { var c = img.color; c.a = 0f; img.color = c; }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => this.onChosen?.Invoke(this));
            button.interactable = true;
        }
    }

    private void EnsureRarityBadge(RewardDefinition def)
    {
        var existing = transform.Find("RarityBadge");
        if (existing != null)
        {
            Destroy(existing.gameObject);
        }

        if (def?.cardData == null)
        {
            return;
        }

        var go = new GameObject("RarityBadge");
        go.transform.SetParent(transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, 8f);
        rt.sizeDelta = new Vector2(140f, 28f);
        var bg = go.AddComponent<Image>();
        bg.raycastTarget = false;
        var label = go.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 18;
        label.raycastTarget = false;
        string name;
        Color col;
        switch (def.cardData.cardRarity)
        {
            case Card.CardRarity.Uncommon:
                name = "稀有";
                col = new Color(0.35f, 0.75f, 0.45f, 0.92f);
                break;
            case Card.CardRarity.Rare:
                name = "史诗";
                col = new Color(0.35f, 0.55f, 0.95f, 0.92f);
                break;
            case Card.CardRarity.Legendary:
                name = "传说";
                col = new Color(0.95f, 0.7f, 0.2f, 0.92f);
                break;
            default:
                name = "普通";
                col = new Color(0.55f, 0.55f, 0.6f, 0.9f);
                break;
        }

        bg.color = col;
        label.text = name;
        label.color = Color.white;
        ChineseFontBootstrap.ApplyChineseFont(label);
    }

    public void Interactable(bool value)
    {
        if (button) button.interactable = value;
    }
}
