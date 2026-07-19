using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using samirin33.SamirinBoothManager.UI.Parts;

/// <summary>
/// バージョンチェック後、更新があるアセットを表示するウィンドウ。
/// </summary>
public class SBM_UpdateRemind : EditorWindow
{
    const string UxmlPath = "Assets/samirin33/SamirinBoothManager/UI/SBM_UpdateRemind.uxml";

    SBM_GridScroll _gridScroll;
    UpdateAssetList _updateList;
    List<SamirinBoothAssetInfo> _pendingInfos;

    [MenuItem("samirin33/アップデートの確認", false, 501)]
    public static async void ShowFromMenu()
    {
        // まず現在の情報で開き、続けて最新情報を取得して一覧を更新する
        OpenWindow(SamirinBoothUpdateUtil.CollectOutdatedAssets() ?? new List<SamirinBoothAssetInfo>());

        try
        {
            await InfomationChecker.RunUpdateAsync(showDialogs: false, showRemindWindow: false);
        }
        catch (Exception e)
        {
            Debug.LogError("[SBM_UpdateRemind] 情報確認に失敗: " + e);
        }

        OpenWindow(SamirinBoothUpdateUtil.CollectOutdatedAssets() ?? new List<SamirinBoothAssetInfo>());
    }

    public static void ShowIfNeeded()
    {
        var outdated = SamirinBoothUpdateUtil.CollectOutdatedAssets();
        if (outdated == null || outdated.Count == 0)
            return;

        OpenWindow(outdated);
    }

    public static void Show(List<SamirinBoothAssetInfo> outdated)
    {
        if (outdated == null || outdated.Count == 0)
            return;

        OpenWindow(outdated);
    }

    static void OpenWindow(List<SamirinBoothAssetInfo> outdated)
    {
        var window = GetWindow<SBM_UpdateRemind>(utility: true);
        window.titleContent = new GUIContent("アップデートのお知らせ！");
        window.minSize = new Vector2(420, 480);
        window._pendingInfos = outdated != null
            ? new List<SamirinBoothAssetInfo>(outdated)
            : new List<SamirinBoothAssetInfo>();
        window.Show();
        window.Focus();
        window.ApplyPendingInfos();
    }

    public void CreateGUI()
    {
        _gridScroll?.Stop();
        _gridScroll = null;
        rootVisualElement.Clear();

        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
        if (visualTree == null)
        {
            rootVisualElement.Add(new Label($"UXML not found: {UxmlPath}"));
            return;
        }

        visualTree.CloneTree(rootVisualElement);
        _updateList = rootVisualElement.Q<UpdateAssetList>();
        _gridScroll = SBM_GridScroll.Attach(rootVisualElement);
        _gridScroll?.Start();

        ApplyPendingInfos();
    }

    void ApplyPendingInfos()
    {
        if (_updateList == null || _pendingInfos == null)
            return;

        _updateList.Bind(_pendingInfos);
        _pendingInfos = null;
    }

    void OnDisable()
    {
        if (_updateList != null && _updateList.ShouldIgnoreCurrentVersions)
            SamirinBoothUpdateUtil.IgnoreLatest(_updateList.BoundInfos);

        _gridScroll?.Stop();
        _gridScroll = null;
    }
}
