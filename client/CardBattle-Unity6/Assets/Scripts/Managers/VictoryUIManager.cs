using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class VictoryUIManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text turnsText;
    [SerializeField] private TMP_Text dealtText;
    [SerializeField] private TMP_Text takenText;

    private void Start()
    {
        try { WalletHudUI.Instance?.SetVisible(false); } catch { /* ignore */ }

        var s = GameSession.Instance;
        if (s != null)
        {
            if (turnsText) turnsText.text = s.turnsTaken.ToString();
            if (dealtText) dealtText.text = s.totalDamageDealt.ToString();
            if (takenText) takenText.text = s.totalDamageTaken.ToString();
        }

        StageRewardClient.ClaimIfNeeded("boss1_clear");
        FixVictoryButtonsChinese();
    }

    /// <summary>
    /// Victory buttons use English sprite plates + tiny Chinese TMP on top — restyle to solid + Chinese only.
    /// </summary>
    private void FixVictoryButtonsChinese()
    {
        RestyleButton(FindButton("MainMenuButton"), "主菜单");
        RestyleButton(FindButton("Quit"), "退出");
    }

    private static Button FindButton(string name)
    {
        var go = GameObject.Find(name);
        return go != null ? go.GetComponent<Button>() : null;
    }

    private static void RestyleButton(Button button, string chineseLabel)
    {
        if (button == null)
        {
            return;
        }

        // Hide every existing label (TMP / legacy Text) so English sprite text isn't duplicated.
        foreach (var t in button.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t.gameObject.name != "CNLabel")
            {
                t.gameObject.SetActive(false);
            }
        }

        foreach (var t in button.GetComponentsInChildren<Text>(true))
        {
            t.gameObject.SetActive(false);
        }

        var img = button.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = null;
            img.type = Image.Type.Simple;
            img.color = new Color(0.12f, 0.16f, 0.22f, 0.95f);
            img.raycastTarget = true;
        }

        var labelTr = button.transform.Find("CNLabel");
        TMP_Text label;
        if (labelTr == null)
        {
            var go = new GameObject("CNLabel", typeof(RectTransform));
            labelTr = go.transform;
            labelTr.SetParent(button.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            label = go.AddComponent<TextMeshProUGUI>();
        }
        else
        {
            label = labelTr.GetComponent<TMP_Text>() ?? labelTr.gameObject.AddComponent<TextMeshProUGUI>();
            labelTr.gameObject.SetActive(true);
        }

        label.text = chineseLabel;
        label.fontSize = 44;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        try { ChineseFontBootstrap.ApplyChineseFont(label); } catch { /* ignore */ }

        button.interactable = true;
        var brt = button.transform as RectTransform;
        if (brt != null)
        {
            brt.localScale = Vector3.one;
        }
    }

    public void OnMainMenu()
    {
        Time.timeScale = 1f;

        var am = AudioManager.Instance;
        if (am != null) am.StopMusic();

        SceneFlowManager.Instance.LoadMainMenu();
    }

    public void OnQuit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
