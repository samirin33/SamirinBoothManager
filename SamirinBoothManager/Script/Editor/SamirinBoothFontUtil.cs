using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

/// <summary>
/// UI Toolkit 用 FontAsset の生成・適用。
/// SDF ではなく Dynamic（必要文字を都度追加）+ SMOOTH_HINTED で表示する。
/// -unity-font (Font/OTF) 直指定は DropdownField 等で Atlas Missing になりやすいため、
/// 永続化された Dynamic FontAsset を unityFontDefinition で使う。
/// </summary>
public static class SamirinBoothFontUtil
{
    public const string SourceFontPath =
        "Assets/samirin33/SamirinBoothManager/Font/YasashisaGothicBold-V2.otf";
    public const string FontAssetPath =
        "Assets/samirin33/SamirinBoothManager/Font/YasashisaGothicBold-V2 Dynamic.asset";
    const string LegacySdfFontAssetPath =
        "Assets/samirin33/SamirinBoothManager/Font/YasashisaGothicBold-V2 SDF.asset";

    /// <summary>生成設定を変えたら上げる。不一致なら自動再生成する。</summary>
    const int SettingsVersion = 3;
    const string SettingsVersionKey = "samirin33.SamirinBoothManager.FontAssetSettingsVersion";

    // Dynamic ビットマップ系。UI の 12〜30px 程度を想定したサンプリング
    const int SamplingPointSize = 64;
    const int AtlasPadding = 5;
    const int AtlasWidth = 2048;
    const int AtlasHeight = 2048;
    const GlyphRenderMode RenderMode = GlyphRenderMode.SMOOTH_HINTED;

    static FontAsset _cached;

    [MenuItem("samirin33/Rebuild UI Font Asset", false, 600)]
    public static void RebuildFromMenu()
    {
        _cached = null;
        EditorPrefs.DeleteKey(SettingsVersionKey);
        DeleteAssetIfExists(FontAssetPath);
        DeleteAssetIfExists(LegacySdfFontAssetPath);

        var created = EnsureFontAsset(forceCreate: true);
        EditorUtility.DisplayDialog(
            "Samirin Booth Font",
            created != null
                ? $"Dynamic FontAsset を生成しました:\n{FontAssetPath}\n\nMode: Dynamic / SMOOTH_HINTED / Sample: {SamplingPointSize}"
                : "FontAsset の生成に失敗しました。ソースフォントを確認してください。",
            "OK");
    }

    public static FontAsset EnsureFontAsset(bool forceCreate = false)
    {
        if (!forceCreate && EditorPrefs.GetInt(SettingsVersionKey, 0) != SettingsVersion)
            forceCreate = true;

        if (!forceCreate)
        {
            if (_cached != null)
                return _cached;

            _cached = AssetDatabase.LoadAssetAtPath<FontAsset>(FontAssetPath);
            if (_cached != null && HasValidAtlas(_cached) && IsDynamicBitmapAsset(_cached))
                return _cached;

            _cached = null;
            forceCreate = true;
        }

        var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (sourceFont == null)
        {
            Debug.LogError($"[SBM] Source font not found: {SourceFontPath}");
            return null;
        }

        DeleteAssetIfExists(FontAssetPath);
        DeleteAssetIfExists(LegacySdfFontAssetPath);

        var fontAsset = FontAsset.CreateFontAsset(
            sourceFont,
            SamplingPointSize,
            AtlasPadding,
            RenderMode,
            AtlasWidth,
            AtlasHeight,
            AtlasPopulationMode.Dynamic,
            true);

        if (fontAsset == null)
        {
            Debug.LogError("[SBM] FontAsset.CreateFontAsset failed.");
            return null;
        }

        fontAsset.name = "YasashisaGothicBold-V2 Dynamic";
        AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

        if (fontAsset.material != null)
        {
            fontAsset.material.name = fontAsset.name + " Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        if (fontAsset.atlasTextures != null)
        {
            for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
            {
                var tex = fontAsset.atlasTextures[i];
                if (tex == null)
                    continue;
                tex.name = fontAsset.name + " Atlas " + i;
                tex.filterMode = FilterMode.Bilinear;
                tex.anisoLevel = 0;
                AssetDatabase.AddObjectToAsset(tex, fontAsset);
            }
        }

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(FontAssetPath, ImportAssetOptions.ForceUpdate);

        EditorPrefs.SetInt(SettingsVersionKey, SettingsVersion);
        _cached = AssetDatabase.LoadAssetAtPath<FontAsset>(FontAssetPath);
        return _cached;
    }

    /// <summary>
    /// SBM_Text クラス要素へ Dynamic FontAsset を適用し、レガシー Font 指定を消す。
    /// </summary>
    public static void ApplySbmTextFonts(VisualElement root)
    {
        if (root == null)
            return;

        var fontAsset = EnsureFontAsset();
        if (fontAsset == null)
            return;

        var definition = new StyleFontDefinition(fontAsset);
        root.Query(className: "SBM_Text").ForEach(ve =>
        {
            ve.style.unityFontDefinition = definition;
            ve.style.unityFont = new StyleFont(StyleKeyword.None);
        });
    }

    static bool IsDynamicBitmapAsset(FontAsset fontAsset)
    {
        if (fontAsset == null)
            return false;

        // 旧 SDF アセットを誤って使い続けない
        if (fontAsset.atlasPopulationMode != AtlasPopulationMode.Dynamic)
            return false;

        var path = AssetDatabase.GetAssetPath(fontAsset);
        return string.Equals(path, FontAssetPath, System.StringComparison.OrdinalIgnoreCase);
    }

    static bool HasValidAtlas(FontAsset fontAsset)
    {
        if (fontAsset == null || fontAsset.atlasTextures == null || fontAsset.atlasTextures.Length == 0)
            return false;

        for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
        {
            if (fontAsset.atlasTextures[i] == null)
                return false;
        }

        return true;
    }

    static void DeleteAssetIfExists(string assetPath)
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(assetPath) != null)
            AssetDatabase.DeleteAsset(assetPath);
    }
}
