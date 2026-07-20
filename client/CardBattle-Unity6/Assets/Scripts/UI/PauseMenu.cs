using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject root; // the panel root GO

    private void Awake()
    {
        if (root) root.SetActive(false);
    }

    private void Start()
    {
        EnsureBackButton();
    }

    public void Show()
    {
        if (root) root.SetActive(true);
        Time.timeScale = 0f;
        EnsureBackButton();
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

        // Prefer cloning RetryButton for matching style.
        var retry = root.transform.Find("RetryButton") as RectTransform;
        GameObject go;
        if (retry != null)
        {
            go = Instantiate(retry.gameObject, root.transform);
            go.name = "BackButton";
            var rt = go.GetComponent<RectTransform>();
            // Place between title and retry if possible.
            var mainMenu = root.transform.Find("MainMenuButton") as RectTransform;
            if (mainMenu != null)
            {
                rt.anchoredPosition = new Vector2(
                    mainMenu.anchoredPosition.x,
                    (retry.anchoredPosition.y + mainMenu.anchoredPosition.y) * 0.5f);
            }
            else
            {
                rt.anchoredPosition = retry.anchoredPosition + new Vector2(0f, 90f);
            }

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
        else
        {
            go = new GameObject("BackButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(root.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(280f, 70f);
            rt.anchoredPosition = new Vector2(0f, -40f);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.25f, 0.45f, 0.7f, 1f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(OnClickBack);

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var tr = textGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<TextMeshProUGUI>();
            ChineseFontBootstrap.ApplyChineseFont(text);
            text.text = "返回";
            text.fontSize = 32f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
        }
    }
}
