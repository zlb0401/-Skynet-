using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Uses Noto Sans SC (Simplified Chinese) as TMP fallback / card text font.
/// </summary>
public static class ChineseFontBootstrap
{
    private static TMP_FontAsset _zhFont;
    private static readonly HashSet<int> _patchedFonts = new HashSet<int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        EnsureFont();
        SceneManager.sceneLoaded += (_, __) => ApplyToScene();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AfterFirstScene()
    {
        ApplyToScene();
    }

    public static TMP_FontAsset EnsureFont()
    {
        if (_zhFont != null)
        {
            return _zhFont;
        }

        // Prefer Simplified Chinese Noto; fallback to cuyuan
        var font = Resources.Load<Font>("Fonts/NotoSansSC-Regular");
        if (font == null)
        {
            font = Resources.Load<Font>("Fonts/cuyuan");
        }

        if (font == null)
        {
            Debug.LogWarning("[ChineseFont] No Chinese font found in Resources/Fonts.");
            return null;
        }

        _zhFont = TMP_FontAsset.CreateFontAsset(font);
        if (_zhFont != null)
        {
            _zhFont.name = "NotoSansSC_DynamicSDF";
        }

        return _zhFont;
    }

    public static void ApplyChineseFont(TMP_Text text)
    {
        if (text == null)
        {
            return;
        }

        var zh = EnsureFont();
        if (zh == null)
        {
            return;
        }

        text.font = zh;
    }

    public static void ApplyToScene()
    {
        var zh = EnsureFont();
        if (zh == null)
        {
            return;
        }

        var texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in texts)
        {
            if (t == null || t.font == null || t.font == zh)
            {
                continue;
            }

            AttachFallback(t.font, zh);
        }
    }

    private static void AttachFallback(TMP_FontAsset primary, TMP_FontAsset zh)
    {
        if (primary == null || zh == null || primary == zh)
        {
            return;
        }

        var id = primary.GetInstanceID();
        if (_patchedFonts.Contains(id))
        {
            return;
        }

        if (primary.fallbackFontAssetTable == null)
        {
            primary.fallbackFontAssetTable = new List<TMP_FontAsset>();
        }

        if (!primary.fallbackFontAssetTable.Contains(zh))
        {
            primary.fallbackFontAssetTable.Add(zh);
        }

        _patchedFonts.Add(id);
    }
}
