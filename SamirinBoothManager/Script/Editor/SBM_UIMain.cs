using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using samirin33.SamirinBoothManager.UI.Parts;

public class SBM_UIMain : EditorWindow
{
    const string MainUxmlPath = "Assets/samirin33/SamirinBoothManager/UI/SBM_Main.uxml";
    /// <summary>SamirinBoothManagerInstaller がインストール完了後にウィンドウを開くためのフラグ。</summary>
    public const string PrefsOpenAfterInstallKey = "samirin33.SamirinBoothManagerInstaller.OpenWindow";

    SBM_GridScroll _gridScroll;
    SamirinBoothAssetInfo _pendingFocus;

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

    [MenuItem("samirin33/Samirin's Item Center", false, 500)]
    public static void ShowWindow()
    {
        ShowWindowAndFocus(null);
    }

    public static void ShowWindowAndFocus(SamirinBoothAssetInfo info)
    {
        var window = GetWindow<SBM_UIMain>();
        window.titleContent = new GUIContent("Samirin's Item Center");
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

        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(MainUxmlPath);
        if (visualTree == null)
        {
            rootVisualElement.Add(new Label($"UXML not found: {MainUxmlPath}"));
            return;
        }

        visualTree.CloneTree(rootVisualElement);

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
}
