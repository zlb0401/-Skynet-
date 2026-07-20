using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject root; // the panel root GO

    private const float ButtonGap = 120f;

    private void Awake()
    {
        if (root) root.SetActive(false);
    }

    private void Start()
    {
        EnsureBackButton();
        ApplyButtonLabels();
        LayoutPauseButtons();
    }

    public void Show()
    {
        if (root) root.SetActive(true);
        Time.timeScale = 0f;
        EnsureBackButton();
        ApplyButtonLabels();
        LayoutPauseButtons();
    }

    public void Hide()
    {
        if (root) root.SetActive(false);
        Time.timeScale = 1f;
    }

    /// <summary>Menu button only opens; use 返回 to close.</summary>
    public void Toggle()
    {
        Show();
    }

    public void OnClickBack() => Hide();

    public void OnClickMainMenu()
    {
        Time.timeScale = 1f;
        AudioManager.Instance?.StopMusic();
        SceneFlowManager.Instance?.LoadMainMenu();
    }

    public void OnClickRetry()
    {
        AudioManager.Instance?.StopMusic();
        SceneFlowManager.Instance?.LoadRetry();
    }

    private void EnsureBackButton()
    {
        if (root == null)
        {
            return;
        }

        if (root.transform.Find("BackButton") != null)
        {
            return;
        }

        var retry = root.transform.Find("RetryButton") as RectTransform;
        if (retry == null)
        {
            return;
        }

        // Clone Retry so sprite / colors / TMP style match exactly.
        var go = Instantiate(retry.gameObject, root.transform);
        go.name = "BackButton";
        go.transform.SetSiblingIndex(retry.GetSiblingIndex());

        var btn = go.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick = new Button.ButtonClickedEvent();
            btn.onClick.AddListener(OnClickBack);
        }
    }

    /// <summary>
    /// Keep Back label style identical to Retry (do not swap ChineseFontBootstrap font).
    /// </summary>
    private void ApplyButtonLabels()
    {
        if (root == null)
        {
            return;
        }

        var retry = root.transform.Find("RetryButton");
        var back = root.transform.Find("BackButton");
        var mainMenu = root.transform.Find("MainMenuButton");

        var retryLabel = retry != null ? retry.GetComponentInChildren<TMP_Text>(true) : null;
        if (retryLabel != null)
        {
            retryLabel.text = "重开";
        }

        var backLabel = back != null ? back.GetComponentInChildren<TMP_Text>(true) : null;
        if (backLabel != null && retryLabel != null)
        {
            CopyTmpStyle(retryLabel, backLabel);
            backLabel.text = "返回";
        }

        var mainLabel = mainMenu != null ? mainMenu.GetComponentInChildren<TMP_Text>(true) : null;
        if (mainLabel != null && retryLabel != null)
        {
            // Keep 主菜单 wording; only align font metrics with retry if drifted.
            var text = string.IsNullOrEmpty(mainLabel.text) ? "主菜单" : mainLabel.text;
            CopyTmpStyle(retryLabel, mainLabel);
            mainLabel.text = text;
        }
    }

    private static void CopyTmpStyle(TMP_Text from, TMP_Text to)
    {
        if (from == null || to == null || from == to)
        {
            return;
        }

        to.font = from.font;
        to.fontSharedMaterial = from.fontSharedMaterial;
        to.fontSize = from.fontSize;
        to.fontStyle = from.fontStyle;
        to.color = from.color;
        to.alignment = from.alignment;
        to.enableAutoSizing = from.enableAutoSizing;
        to.characterSpacing = from.characterSpacing;
        to.wordSpacing = from.wordSpacing;
        to.lineSpacing = from.lineSpacing;
        to.margin = from.margin;
        to.enableWordWrapping = from.enableWordWrapping;
        to.overflowMode = from.overflowMode;
    }

    /// <summary>
    /// Stack 返回 / 重开 / 主菜单 with even gaps (avoids midpoint overlap).
    /// </summary>
    private void LayoutPauseButtons()
    {
        if (root == null)
        {
            return;
        }

        var back = root.transform.Find("BackButton") as RectTransform;
        var retry = root.transform.Find("RetryButton") as RectTransform;
        var mainMenu = root.transform.Find("MainMenuButton") as RectTransform;
        if (retry == null || mainMenu == null)
        {
            return;
        }

        float x = retry.anchoredPosition.x;
        var size = retry.sizeDelta;

        // Center the three-button stack around the original retry position.
        float midY = retry.anchoredPosition.y;
        float topY = midY + ButtonGap;
        float bottomY = midY - ButtonGap;

        if (back != null)
        {
            back.sizeDelta = size;
            back.anchorMin = retry.anchorMin;
            back.anchorMax = retry.anchorMax;
            back.pivot = retry.pivot;
            back.anchoredPosition = new Vector2(x, topY);
            back.localScale = Vector3.one;
        }

        retry.sizeDelta = size;
        retry.anchoredPosition = new Vector2(x, midY);

        mainMenu.sizeDelta = size;
        mainMenu.anchorMin = retry.anchorMin;
        mainMenu.anchorMax = retry.anchorMax;
        mainMenu.pivot = retry.pivot;
        mainMenu.anchoredPosition = new Vector2(x, bottomY);
        mainMenu.localScale = Vector3.one;
    }
}
