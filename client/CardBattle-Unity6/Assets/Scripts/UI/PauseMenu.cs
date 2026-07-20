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
        LayoutPauseButtons();
    }

    public void Show()
    {
        if (root) root.SetActive(true);
        Time.timeScale = 0f;
        EnsureBackButton();
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

        var label = go.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = "返回";
            ChineseFontBootstrap.ApplyChineseFont(label);
        }

        var btn = go.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick = new Button.ButtonClickedEvent();
            btn.onClick.AddListener(OnClickBack);
        }
    }

    /// <summary>
    /// Stack 返回 / 重试 / 主菜单 with even gaps (avoids midpoint overlap).
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
