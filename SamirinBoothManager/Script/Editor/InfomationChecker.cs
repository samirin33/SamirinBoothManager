using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// GitHub から Booth 情報 / Manager を取得し、必要に応じて更新する Editor ユーティリティ。
/// </summary>
[InitializeOnLoad]
public static class InfomationChecker
{
    const string RepoOwner = "samirin33";
    const string RepoName = "SamirinBoothManager";
    const string Branch = "main";
    const string ZipUrl = "https://github.com/" + RepoOwner + "/" + RepoName + "/archive/refs/heads/" + Branch + ".zip";

    const string InformationAssetPath = "Assets/samirin33/SamirinBoothInformation";
    const string ManagerAssetPath = "Assets/samirin33/SamirinBoothManager";
    const string PackageAssetInfoPath = ManagerAssetPath + "/PackageAssetInfo.json";
    const string ManagerAssetInfoPath = InformationAssetPath + "/Manger.asset";

    const string PrefsAutoCheckKey = "samirin33.InfomationChecker.AutoCheckEnabled";
    const string PrefsSessionCheckedKey = "samirin33.InfomationChecker.SessionChecked";

    static bool _isRunning;

    public static bool IsRunning => _isRunning;

    public static bool AutoCheckEnabled
    {
        get => EditorPrefs.GetBool(PrefsAutoCheckKey, true);
        set => EditorPrefs.SetBool(PrefsAutoCheckKey, value);
    }

    static InfomationChecker()
    {
        EditorApplication.delayCall += OnEditorReady;
    }

    static void OnEditorReady()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        ScheduleAutoCheckIfNeeded();
    }

    public static Task RunManually()
    {
        return RunUpdateAsync(showDialogs: false);
    }

    /// <summary>
    /// 情報アセットの取得 → バージョン比較 → 必要なら Manager 更新。
    /// </summary>
    /// <param name="showRemindWindow">完了後にアップデート一覧を自動表示するか。</param>
    public static async Task RunUpdateAsync(bool showDialogs, bool showRemindWindow = true)
    {
        if (_isRunning)
        {
            if (showDialogs)
                EditorUtility.DisplayDialog("InfomationChecker", "更新処理は既に実行中です。", "OK");
            return;
        }

        _isRunning = true;
        try
        {
            EditorUtility.DisplayProgressBar("InfomationChecker", "リポジトリをダウンロード中...", 0.1f);

            string tempRoot = Path.Combine(Path.GetTempPath(), "SamirinBoothManager_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            string zipPath = Path.Combine(tempRoot, "repo.zip");

            try
            {
                bool downloaded = await DownloadFileAsync(ZipUrl, zipPath);
                if (!downloaded)
                {
                    LogError("GitHub からのダウンロードに失敗しました: " + ZipUrl);
                    if (showDialogs)
                        EditorUtility.DisplayDialog("InfomationChecker", "ダウンロードに失敗しました。\nネットワーク接続を確認してください。", "OK");
                    return;
                }

                EditorUtility.DisplayProgressBar("InfomationChecker", "情報アセットを展開中...", 0.4f);

                string extractRoot = Path.Combine(tempRoot, "extract");
                ZipFile.ExtractToDirectory(zipPath, extractRoot);

                string repoRoot = FindRepoRoot(extractRoot);
                if (string.IsNullOrEmpty(repoRoot))
                {
                    LogError("zip 内にリポジトリルートが見つかりませんでした。");
                    if (showDialogs)
                        EditorUtility.DisplayDialog("InfomationChecker", "アーカイブの解析に失敗しました。", "OK");
                    return;
                }

                string remoteInformation = Path.Combine(repoRoot, "SamirinBoothInformation");
                string remoteManager = Path.Combine(repoRoot, "SamirinBoothManager");

                if (!Directory.Exists(remoteInformation))
                {
                    LogError("リモートに SamirinBoothInformation フォルダがありません。");
                    return;
                }

                CopyDirectoryContents(remoteInformation, ToAbsolutePath(InformationAssetPath));
                AssetDatabase.Refresh();

                EditorUtility.DisplayProgressBar("InfomationChecker", "バージョンを確認中...", 0.7f);

                bool shouldUpdateManager = ShouldUpdateManager();
                if (shouldUpdateManager)
                {
                    if (!Directory.Exists(remoteManager))
                    {
                        LogError("リモートに SamirinBoothManager フォルダがありません。");
                        return;
                    }

                    EditorUtility.DisplayProgressBar("InfomationChecker", "Manager を更新中...", 0.85f);
                    CopyDirectoryContents(remoteManager, ToAbsolutePath(ManagerAssetPath));
                    AssetDatabase.Refresh();
                    Debug.Log("[InfomationChecker] SamirinBoothManager を更新しました。");
                }
                else
                {
                    Debug.Log("[InfomationChecker] SamirinBoothManager は最新です。スキップしました。");
                }

                if (showRemindWindow)
                {
                    // 情報更新後、製品アセットに新しいバージョンがあればリマインド表示
                    EditorApplication.delayCall += () => SBM_UpdateRemind.ShowIfNeeded();
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempRoot))
                        Directory.Delete(tempRoot, true);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[InfomationChecker] 一時フォルダの削除に失敗: " + e.Message);
                }
            }
        }
        catch (Exception e)
        {
            LogError("更新処理中に例外が発生しました: " + e);
            if (showDialogs)
                EditorUtility.DisplayDialog("InfomationChecker", "更新処理に失敗しました。\n" + e.Message, "OK");
        }
        finally
        {
            _isRunning = false;
            EditorUtility.ClearProgressBar();
        }
    }

    static bool ShouldUpdateManager()
    {
        string packageInfoAbs = ToAbsolutePath(PackageAssetInfoPath);
        if (!File.Exists(packageInfoAbs))
        {
            Debug.Log("[InfomationChecker] PackageAssetInfo.json が存在しないため Manager を取得します。");
            return true;
        }

        Version localVersion = ReadPackageJsonVersion(packageInfoAbs);
        Version remoteVersion = ReadManagerAssetVersion(ToAbsolutePath(ManagerAssetInfoPath));

        if (remoteVersion == null)
        {
            Debug.LogWarning("[InfomationChecker] Manger.asset のバージョンを読み取れませんでした。Manager 更新をスキップします。");
            return false;
        }

        if (localVersion == null)
        {
            Debug.Log("[InfomationChecker] PackageAssetInfo.json のバージョンを読み取れないため Manager を取得します。");
            return true;
        }

        // Manger.asset（取得した情報）が PackageAssetInfo より新しい場合に更新
        bool newer = remoteVersion > localVersion;
        Debug.Log($"[InfomationChecker] バージョン比較: PackageAssetInfo={localVersion}, Manger.asset={remoteVersion}, update={newer}");
        return newer;
    }

    static Version ReadPackageJsonVersion(string absolutePath)
    {
        try
        {
            string json = File.ReadAllText(absolutePath, Encoding.UTF8);
            Match match = Regex.Match(json, "\"version\"\\s*:\\s*\"([^\"]+)\"");
            if (!match.Success)
                return null;

            return ParseVersion(match.Groups[1].Value);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[InfomationChecker] PackageAssetInfo.json の読み込みに失敗: " + e.Message);
            return null;
        }
    }

    static Version ReadManagerAssetVersion(string absolutePath)
    {
        try
        {
            if (!File.Exists(absolutePath))
            {
                Debug.LogWarning("[InfomationChecker] Manger.asset が見つかりません: " + absolutePath);
                return null;
            }

            string yaml = File.ReadAllText(absolutePath, Encoding.UTF8);
            int major = ReadYamlInt(yaml, "majorVertion");
            int minor = ReadYamlInt(yaml, "minorVertion");
            int patch = ReadYamlInt(yaml, "patchVertion");
            return new Version(major, minor, patch);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[InfomationChecker] Manger.asset の読み込みに失敗: " + e.Message);
            return null;
        }
    }

    static int ReadYamlInt(string yaml, string key)
    {
        Match match = Regex.Match(yaml, @"^\s*" + Regex.Escape(key) + @"\s*:\s*(-?\d+)\s*$", RegexOptions.Multiline);
        if (!match.Success)
            return 0;
        return int.Parse(match.Groups[1].Value);
    }

    static Version ParseVersion(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        text = text.Trim();
        string[] parts = text.Split('.');
        int major = parts.Length > 0 && int.TryParse(parts[0], out int ma) ? ma : 0;
        int minor = parts.Length > 1 && int.TryParse(parts[1], out int mi) ? mi : 0;
        int patch = parts.Length > 2 && int.TryParse(parts[2], out int pa) ? pa : 0;
        return new Version(major, minor, patch);
    }

    static async Task<bool> DownloadFileAsync(string url, string destinationPath)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 120;
            var operation = request.SendWebRequest();

            while (!operation.isDone)
                await Task.Yield();

#if UNITY_2020_2_OR_NEWER
            if (request.result != UnityWebRequest.Result.Success)
#else
            if (request.isNetworkError || request.isHttpError)
#endif
            {
                Debug.LogError("[InfomationChecker] Download error: " + request.error);
                return false;
            }

            byte[] data = request.downloadHandler.data;
            if (data == null || data.Length == 0)
                return false;

            File.WriteAllBytes(destinationPath, data);
            return true;
        }
    }

    static string FindRepoRoot(string extractRoot)
    {
        if (!Directory.Exists(extractRoot))
            return null;

        string[] dirs = Directory.GetDirectories(extractRoot);
        foreach (string dir in dirs)
        {
            if (Directory.Exists(Path.Combine(dir, "SamirinBoothInformation")) ||
                Directory.Exists(Path.Combine(dir, "SamirinBoothManager")))
                return dir;
        }

        if (Directory.Exists(Path.Combine(extractRoot, "SamirinBoothInformation")) ||
            Directory.Exists(Path.Combine(extractRoot, "SamirinBoothManager")))
            return extractRoot;

        return null;
    }

    static void CopyDirectoryContents(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            string relative = file.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string destFile = Path.Combine(destinationDir, relative);
            string destFolder = Path.GetDirectoryName(destFile);
            if (!string.IsNullOrEmpty(destFolder))
                Directory.CreateDirectory(destFolder);

            File.Copy(file, destFile, true);
        }
    }

    static string ToAbsolutePath(string assetPath)
    {
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
    }

    static void LogError(string message)
    {
        Debug.LogError("[InfomationChecker] " + message);
    }

    internal static void ScheduleAutoCheckIfNeeded()
    {
        if (!AutoCheckEnabled || _isRunning)
            return;

        if (SessionState.GetBool(PrefsSessionCheckedKey, false))
            return;

        SessionState.SetBool(PrefsSessionCheckedKey, true);
        EditorApplication.delayCall += () =>
        {
            if (!AutoCheckEnabled || _isRunning)
                return;
            _ = RunUpdateAsync(showDialogs: false);
        };
    }
}

/// <summary>
/// 初回インポート時（InfomationChecker / InformationImporter が追加・再インポートされたとき）にチェックする。
/// </summary>
class InfomationCheckerImportTrigger : AssetPostprocessor
{
    static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        for (int i = 0; i < importedAssets.Length; i++)
        {
            string path = importedAssets[i].Replace('\\', '/');
            if (path.EndsWith("InfomationChecker.cs", StringComparison.OrdinalIgnoreCase) ||
                path.IndexOf("/InformationImporter/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                InfomationChecker.ScheduleAutoCheckIfNeeded();
                return;
            }
        }
    }
}

/// <summary>
/// Preferences に自動更新の ON/OFF を追加する。
/// </summary>
static class InfomationCheckerPreferences
{
    public const string PreferencesPath = "Preferences/samirin33 Information Importer";

    [SettingsProvider]
    public static SettingsProvider CreatePreferencesProvider()
    {
        var provider = new SettingsProvider(PreferencesPath, SettingsScope.User)
        {
            label = "samirin33 Information Importer",
            guiHandler = _ =>
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Booth Information / Manager 自動取得", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "有効時: Unity 起動時・InformationImporter の初回インポート時に GitHub から情報を取得し、必要なら Manager を更新します。",
                    MessageType.Info);

                bool enabled = InfomationChecker.AutoCheckEnabled;
                bool next = EditorGUILayout.ToggleLeft("起動時 / インポート時に自動チェックする", enabled);
                if (next != enabled)
                    InfomationChecker.AutoCheckEnabled = next;

                EditorGUILayout.Space();
                using (new EditorGUI.DisabledScope(InfomationChecker.IsRunning))
                {
                    if (GUILayout.Button("今すぐチェックを実行", GUILayout.Height(28)))
                        InfomationChecker.RunManually();
                }

                EditorGUILayout.Space(12);
                EditorGUILayout.LabelField("アップデート通知", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "アップデートのお知らせで「このバージョンを無視する」を選んだ設定を解除できます。",
                    MessageType.Info);

                int ignoredCount = samirin33.SamirinBoothManager.UI.Parts.SamirinBoothUpdateUtil.CountIgnoredEntries();
                EditorGUILayout.LabelField("無視中のアップデート", ignoredCount > 0 ? $"{ignoredCount} 件" : "なし");

                using (new EditorGUI.DisabledScope(ignoredCount <= 0))
                {
                    if (GUILayout.Button("アップデートの無視をすべて解除", GUILayout.Height(28)))
                    {
                        int cleared = samirin33.SamirinBoothManager.UI.Parts.SamirinBoothUpdateUtil.ClearAllIgnores();
                        EditorUtility.DisplayDialog(
                            "アップデート通知",
                            cleared > 0
                                ? $"{cleared} 件の無視設定を解除しました。"
                                : "解除する無視設定はありませんでした。",
                            "OK");
                    }
                }
            },
            keywords = new HashSet<string>(new[]
            {
                "samirin33", "Booth", "Information", "Manager", "Update", "GitHub", "無視", "Ignore"
            })
        };
        return provider;
    }
}
