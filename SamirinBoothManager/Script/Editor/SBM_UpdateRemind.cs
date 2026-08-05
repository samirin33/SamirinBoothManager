using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Callbacks;
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

    /// <summary>コンパイルを挟んでもトグル状態を保つ。</summary>
    [SerializeField] bool _ignoreCurrentVersions;

    [MenuItem("samirin33/アップデートの確認", false, 501)]
    public static async void ShowFromMenu()
    {
        // まず現在の情報で開き、続けて最新情報を取得して一覧を更新する
        OpenWindow(SamirinBoothUpdateUtil.CollectOutdatedAssets() ?? new List<SamirinBoothAssetInfo>());

        try
        {
            await InformationChecker.RunUpdateAsync(showDialogs: false, showRemindWindow: false);
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

    /// <summary>
    /// SamirinBoothManager が更新されるとコンパイルが走り、UI と一覧が失われる。
    /// リロード後に開いているウィンドウを作り直す。
    /// </summary>
    [DidReloadScripts]
    static void OnScriptsReloaded()
    {
        ScheduleRebuildOpenWindows();
    }

    static void ScheduleRebuildOpenWindows()
    {
        EditorApplication.delayCall += () =>
        {
            // インポート／コンパイル中は一覧を正しく収集できないため落ち着くまで待つ
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                ScheduleRebuildOpenWindows();
                return;
            }

            RebuildOpenWindows();
        };
    }

    public static void RebuildOpenWindows()
    {
        var windows = Resources.FindObjectsOfTypeAll<SBM_UpdateRemind>();
        for (int i = 0; i < windows.Length; i++)
        {
            if (windows[i] != null)
                windows[i].RebuildGUI();
        }
    }

    /// <summary>
    /// UXML の clone からやり直し、最新の一覧で要素を作り直す。
    /// </summary>
    void RebuildGUI()
    {
        _pendingInfos = null;
        CreateGUI();
        Repaint();
    }

    public void CreateGUI()
    {
        _gridScroll?.Stop();
        _gridScroll = null;
        rootVisualElement.Clear();

        SamirinBoothFontUtil.EnsureFontAsset();

        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
        if (visualTree == null)
        {
            rootVisualElement.Add(new Label($"UXML not found: {UxmlPath}"));
            return;
        }

        visualTree.CloneTree(rootVisualElement);
        SamirinBoothFontUtil.ApplySbmTextFonts(rootVisualElement);
        _updateList = rootVisualElement.Q<UpdateAssetList>();
        _gridScroll = SBM_GridScroll.Attach(rootVisualElement);
        _gridScroll?.Start();

        if (_updateList != null)
            _updateList.ShouldIgnoreCurrentVersions = _ignoreCurrentVersions;

        // ドメインリロードを挟むと _pendingInfos は失われるため、その場合は取り直す
        if (_pendingInfos == null)
            _pendingInfos = SamirinBoothUpdateUtil.CollectOutdatedAssets() ?? new List<SamirinBoothAssetInfo>();

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
        // ドメインリロード直前にも呼ばれるので、ここでトグル状態を退避する
        if (_updateList != null)
            _ignoreCurrentVersions = _updateList.ShouldIgnoreCurrentVersions;

        _gridScroll?.Stop();
        _gridScroll = null;
    }

    // OnDisable はドメインリロードでも呼ばれるため、無視設定はウィンドウを閉じたときだけ確定させる
    void OnDestroy()
    {
        if (_updateList != null && _updateList.ShouldIgnoreCurrentVersions)
            SamirinBoothUpdateUtil.IgnoreLatest(_updateList.BoundInfos);
    }
}
