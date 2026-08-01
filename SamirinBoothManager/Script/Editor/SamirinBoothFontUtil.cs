using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

/// <summary>
/// UI Toolkit 用 FontAsset の生成・適用。
/// -unity-font (Font/OTF) 直指定だと内部で FontAsset 化し、
/// DropdownField 等で m_AtlasTextures の MissingReferenceException が出るため、
/// 永続化された FontAsset を -unity-font-definition 相当で使う。
/// </summary>
public static class SamirinBoothFontUtil
{
    public const string SourceFontPath =
        "Assets/samirin33/SamirinBoothManager/Font/YasashisaGothicBold-V2.otf";
    public const string FontAssetPath =
        "Assets/samirin33/SamirinBoothManager/Font/YasashisaGothicBold-V2 SDF.asset";

    static FontAsset _cached;

    [MenuItem("samirin33/Rebuild UI Font Asset", false, 600)]
    public static void RebuildFromMenu()
    {
        _cached = null;
        if (AssetDatabase.LoadAssetAtPath<FontAsset>(FontAssetPath) != null)
            AssetDatabase.DeleteAsset(FontAssetPath);

        var created = EnsureFontAsset(forceCreate: true);
        EditorUtility.DisplayDialog(
            "Samirin Booth Font",
            created != null
                ? $"FontAsset を生成しました:\n{FontAssetPath}"
                : "FontAsset の生成に失敗しました。ソースフォントを確認してください。",
            "OK");
    }

    public static FontAsset EnsureFontAsset(bool forceCreate = false)
    {
        if (!forceCreate)
        {
            if (_cached != null)
                return _cached;

            _cached = AssetDatabase.LoadAssetAtPath<FontAsset>(FontAssetPath);
            if (_cached != null && HasValidAtlas(_cached))
                return _cached;

            _cached = null;
        }

        var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (sourceFont == null)
        {
            Debug.LogError($"[SBM] Source font not found: {SourceFontPath}");
            return null;
        }

        if (AssetDatabase.LoadAssetAtPath<FontAsset>(FontAssetPath) != null)
            AssetDatabase.DeleteAsset(FontAssetPath);

        var fontAsset = FontAsset.CreateFontAsset(
            sourceFont,
            90,
            9,
            GlyphRenderMode.SDFAA,
            1024,
            1024,
            AtlasPopulationMode.Dynamic,
            true);

        if (fontAsset == null)
        {
            Debug.LogError("[SBM] FontAsset.CreateFontAsset failed.");
            return null;
        }

        fontAsset.name = "YasashisaGothicBold-V2 SDF";
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
                AssetDatabase.AddObjectToAsset(tex, fontAsset);
            }
        }

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(FontAssetPath, ImportAssetOptions.ForceUpdate);

        _cached = AssetDatabase.LoadAssetAtPath<FontAsset>(FontAssetPath);
        return _cached;
    }

    /// <summary>
    /// SBM_Text クラス要素へ FontAsset を適用し、レガシー Font 指定を消す。
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
}
