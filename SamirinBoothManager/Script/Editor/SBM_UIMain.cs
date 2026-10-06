using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using samirin33.SamirinBoothManager.UI.Parts;

public class SBM_UIMain : EditorWindow, IHasCustomMenu
{
    const string MainUxmlPath = "Assets/samirin33/SamirinBoothManager/UI/SBM_Main.uxml";
    /// <summary>SamirinBoothManagerInstaller がインストール完了後にウィンドウを開くためのフラグ。</summary>
    public const string PrefsOpenAfterInstallKey = "samirin33.SamirinBoothManagerInstaller.OpenWindow";

    const string ShowHiddenItemsKey = "samirin33.SBM.ShowHiddenItems";

    SBM_GridScroll _gridScroll;
    SamirinBoothAssetInfo _pendingFocus;

    static bool ShowHiddenItems
    {
        get => SessionState.GetBool(ShowHiddenItemsKey, false);
        set => SessionState.SetBool(ShowHiddenItemsKey, value);
    }

    [InitializeOnLoadMethod]
    static void OpenAfterInstallIfRequested()
    {
        if (!EditorPrefs.GetBool(PrefsOpenAfterInstallKey, false))
            return;

        EditorApplication.delayCall += () =>
        {
            if (!EditorPrefs.GetBool(PrefsOpenAfterInstallKey, false))
                return;
            EditorPrefs.DeleteKey(PrefsOpenAfterInstallKey);
            ShowWindow();
        };
    }

    [MenuItem("samirin33/samirin33's アイテムセンター", false, 500)]
    public static void ShowWindow()
    {
        ShowWindowAndFocus(null);
    }

    public static void ShowWindowAndFocus(SamirinBoothAssetInfo info)
    {
        var window = GetWindow<SBM_UIMain>();
        window.titleContent = new GUIContent("samirin33's アイテムセンター");
        window.minSize = new Vector2(600, 800);
        window._pendingFocus = info;
        window.Show();
        window.Focus();
        window.ApplyPendingFocus();
    }

    public void CreateGUI()
    {
        _gridScroll?.Stop();
        _gridScroll = null;

        SamirinBoothFontUtil.EnsureFontAsset();

        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(MainUxmlPath);
        if (visualTree == null)
        {
            rootVisualElement.Add(new Label($"UXML not found: {MainUxmlPath}"));
            return;
        }

        visualTree.CloneTree(rootVisualElement);
        SamirinBoothFontUtil.ApplySbmTextFonts(rootVisualElement);

        ApplyShowHiddenItems();
        _gridScroll = SBM_GridScroll.Attach(rootVisualElement);
        _gridScroll?.Start();
        ApplyPendingFocus();
    }

    void ApplyPendingFocus()
    {
        if (_pendingFocus == null)
            return;

        var details = rootVisualElement.Q<AssetDetails>("AssetDetails")
            ?? rootVisualElement.Q<AssetDetails>();
        if (details == null)
            return;

        details.Show(_pendingFocus);
        _pendingFocus = null;
    }

    void OnDisable()
    {
        _gridScroll?.Stop();
        _gridScroll = null;
    }

    public void AddItemsToMenu(GenericMenu menu)
    {
        menu.AddItem(new GUIContent("非表示のアイテムを表示する"), ShowHiddenItems, ToggleShowHiddenItems);
        menu.AddItem(new GUIContent("イベントフラグをリセット"), false, ResetVariationCallouts);
    }

    void ToggleShowHiddenItems()
    {
        ShowHiddenItems = !ShowHiddenItems;
        ApplyShowHiddenItems();
    }

    void ApplyShowHiddenItems()
    {
        if (rootVisualElement == null)
            return;

        bool showHidden = ShowHiddenItems;
        rootVisualElement.Query<AssetList>().ForEach(list => list.SetIncludeHiddenItems(showHidden));
    }

    void ResetVariationCallouts()
    {
        if (rootVisualElement == null)
            return;

        int count = 0;
        rootVisualElement.Query<AttachPanel>().ForEach(panel =>
        {
            panel.ResetCalloutForDebug();
            count++;
        });
        Debug.Log($"[SBM] バリエーション吹き出しの表示フラグをリセットしました。対象パネル: {count}");
    }
}
