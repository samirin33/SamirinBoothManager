using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class SBM_UIMain : EditorWindow
{
    const string MainUxmlPath = "Assets/samirin33/SamirinBoothManager/UI/SBM_Main.uxml";

    SBM_GridScroll _gridScroll;

    [MenuItem("samirin33/SamirinBoothManager")]
    public static void ShowWindow()
    {
        var window = GetWindow<SBM_UIMain>();
        window.titleContent = new GUIContent("Samirin Booth Manager");
        window.minSize = new Vector2(400, 500);
        window.Show();
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
    }

    void OnDisable()
    {
        _gridScroll?.Stop();
        _gridScroll = null;
    }
}
