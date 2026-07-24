using MyProjectF.Assets.Scripts.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared card detail overlay (art + name + desc + meta).
/// </summary>
public static class CardDetailPopupUI
{
    private static GameObject _root;

    public static void Show(Card card)
    {
        if (card == null) return;
        Ensure();
        var thumb = _root.transform.Find("Panel/Thumb")?.GetComponent<Image>();
        var title = _root.transform.Find("Panel/Title")?.GetComponent<TMP_Text>();
        var meta = _root.transform.Find("Panel/Meta")?.GetComponent<TMP_Text>();
        var desc = _root.transform.Find("Panel/Desc")?.GetComponent<TMP_Text>();

        if (thumb != null)
        {
            thumb.preserveAspect = false;
            if (card.cardSprite != null)
            {
                thumb.sprite = card.cardSprite;
                thumb.color = Color.white;
            }
            else
            {
                thumb.sprite = null;
                thumb.color = new Color(0.25f, 0.3f, 0.38f, 1f);
            }
        }

        if (title != null)
        {
            title.text = card.GetDisplayName();
            ChineseFontBootstrap.ApplyChineseFont(title);
        }

        if (meta != null)
        {
            var type = card.cardType switch
            {
                Card.CardType.Attack => "攻击",
                Card.CardType.Guard => "防御",
                _ => "战术"
            };
            var rarity = card.cardRarity switch
            {
                Card.CardRarity.Uncommon => "稀有",
                Card.CardRarity.Rare => "史诗",
                Card.CardRarity.Legendary => "传说",
                _ => "普通"
            };
            var level = CardUpgradeCache.GetLevel(card.cardName);
            var copies = CardUpgradeCache.MaxCopiesAllowed(level);
            meta.text = $"{type} · {rarity} · 费用 {card.energyCost} · Lv.{level} · 可带{copies}张";
            ChineseFontBootstrap.ApplyChineseFont(meta);
        }

        if (desc != null)
        {
            desc.text = CardDescriptionUtil.Format(card);
            ChineseFontBootstrap.ApplyChineseFont(desc);
        }

        _root.SetActive(true);
    }

    public static void Hide()
    {
        if (_root != null) _root.SetActive(false);
    }

    private static void Ensure()
    {
        if (_root != null) return;

        _root = new GameObject("CardDetailPopup");
        Object.DontDestroyOnLoad(_root);
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5800;
        var scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        _root.AddComponent<GraphicRaycaster>();

        var dim = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        dim.transform.SetParent(_root.transform, false);
        var drt = dim.GetComponent<RectTransform>();
        drt.anchorMin = Vector2.zero;
        drt.anchorMax = Vector2.one;
        drt.offsetMin = drt.offsetMax = Vector2.zero;
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
        var dimBtn = dim.GetComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(Hide);

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(_root.transform, false);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(720f, 520f);
        panel.GetComponent<Image>().color = new Color(0.10f, 0.13f, 0.18f, 0.98f);

        var thumb = new GameObject("Thumb", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        thumb.transform.SetParent(panel.transform, false);
        var trt = thumb.GetComponent<RectTransform>();
        trt.anchorMin = trt.anchorMax = new Vector2(0f, 0.5f);
        trt.pivot = new Vector2(0f, 0.5f);
        trt.anchoredPosition = new Vector2(36f, 20f);
        trt.sizeDelta = new Vector2(220f, 310f);

        MakeTmp(panel.transform, "Title", "", 34, new Vector2(140f, 180f), new Vector2(400f, 48f), TextAlignmentOptions.Left);
        MakeTmp(panel.transform, "Meta", "", 20, new Vector2(140f, 130f), new Vector2(420f, 40f), TextAlignmentOptions.Left);
        var desc = MakeTmp(panel.transform, "Desc", "", 22, new Vector2(140f, -20f), new Vector2(420f, 220f), TextAlignmentOptions.TopLeft);
        desc.enableWordWrapping = true;

        var close = new GameObject("Close", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        close.transform.SetParent(panel.transform, false);
        var crt = close.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0f);
        crt.anchoredPosition = new Vector2(0f, 36f);
        crt.sizeDelta = new Vector2(160f, 48f);
        close.GetComponent<Image>().color = new Color(0.18f, 0.45f, 0.72f, 1f);
        close.GetComponent<Button>().onClick.AddListener(Hide);
        var cl = MakeTmp(close.transform, "L", "关闭", 22, Vector2.zero, new Vector2(160f, 48f), TextAlignmentOptions.Center);
        var clrt = cl.rectTransform;
        clrt.anchorMin = Vector2.zero;
        clrt.anchorMax = Vector2.one;
        clrt.offsetMin = clrt.offsetMax = Vector2.zero;

        _root.SetActive(false);
    }

    private static TMP_Text MakeTmp(Transform parent, string name, string text, float size, Vector2 pos,
        Vector2 sizeDelta, TextAlignmentOptions align)
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
        tmp.alignment = align;
        tmp.raycastTarget = false;
        ChineseFontBootstrap.ApplyChineseFont(tmp);
        return tmp;
    }
}
