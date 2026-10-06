using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// VPM リポジトリを見て SamiVRCBlocks パッケージを確認し、新しければ Packages 配下を置き換える。
/// 対象とリポジトリの既定値は SamiVRCBlocksAvatarInstaller の vpai-config.json と同じ。
/// そのファイルは avatar-editor パッケージ内にあるため、パッケージが無い環境では既定値を使う。
/// </summary>
public static class ToolPackageUpdateChecker
{
    const string LogPrefix = "[ToolPackageUpdateChecker]";
    const string DefaultPackageId = "com.github.samirin33.samivrcblocks-avatar";
    const string OptionalConfigRelativePath =
        "Packages/com.github.samirin33.samivrcblocks-avatar-editor/Editor/SamiVRCBlocksAvatarInstaller/vpai-config.json";

    static readonly string[] DefaultRepoUrls =
    {
        "https://samirin33.github.io/Samirin33VPM/vpm.json",
        "https://vpm.anatawa12.com/vpm.json",
        "https://vpm.nadena.dev/vpm.json"
    };

    static bool _isRunning;

    [MenuItem("samirin33/ツールのアップデートを確認", false, 522)]
    public static async void CheckFromMenu()
    {
        if (_isRunning)
        {
            EditorUtility.DisplayDialog("ツールのアップデート", "更新確認は既に実行中です。", "OK");
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("ツールのアップデート", "再生中は更新確認できません。", "OK");
            return;
        }

        _isRunning = true;
        try
        {
            await CheckAndUpdateAsync();
        }
        catch (Exception e)
        {
            Debug.LogError(LogPrefix + " " + e);
            EditorUtility.DisplayDialog("ツールのアップデート", "更新確認に失敗しました。\n" + e.Message, "OK");
        }
        finally
        {
            _isRunning = false;
            EditorUtility.ClearProgressBar();
        }
    }

    static async Task CheckAndUpdateAsync()
    {
        EditorUtility.DisplayProgressBar("ツールのアップデート", "更新情報を取得中...", 0.15f);

        LoadConfig(out var targets, out var repos);
        var plans = new List<UpdatePlan>();
        foreach (var target in targets)
        {
            EditorUtility.DisplayProgressBar("ツールのアップデート", target.Id + " を確認中...", 0.35f);
            var remote = await FindRemoteVersionAsync(repos, target);
            plans.Add(BuildPlan(target, remote));
        }

        EditorUtility.ClearProgressBar();

        var applicable = new List<UpdatePlan>();
        var summary = new StringBuilder();
        var updateSummary = new StringBuilder();
        bool anyMissingRemote = false;
        bool anyLocalNewer = false;
        foreach (var plan in plans)
        {
            if (plan.Status == UpdateStatus.RemoteMissing)
            {
                anyMissingRemote = true;
                summary.AppendLine(plan.DisplayName + ": VPM にパッケージが見つかりません。");
                continue;
            }

            if (plan.Status == UpdateStatus.UpdateAvailable || plan.Status == UpdateStatus.NotInstalled)
            {
                applicable.Add(plan);
                string change = plan.Status == UpdateStatus.NotInstalled
                    ? "  未インストール → " + plan.RemoteVersionText
                    : "  " + plan.LocalVersionText + " → " + plan.RemoteVersionText;
                summary.AppendLine(plan.DisplayName);
                summary.AppendLine(change);
                updateSummary.AppendLine(plan.DisplayName);
                updateSummary.AppendLine(change);
                continue;
            }

            if (plan.Status == UpdateStatus.LocalNewer)
            {
                anyLocalNewer = true;
                summary.AppendLine(plan.DisplayName);
                summary.AppendLine("  インストール済み " + plan.LocalVersionText + "（VPM " + plan.RemoteVersionText + "）");
            }
            else
            {
                summary.AppendLine(plan.DisplayName + " " + plan.LocalVersionText + "（最新）");
            }
        }

        if (applicable.Count == 0)
        {
            string message = anyMissingRemote
                ? summary.ToString().TrimEnd()
                : anyLocalNewer
                    ? "インストール済みのバージョンの方が新しいため、更新しません。\n\n" + summary.ToString().TrimEnd()
                    : "ツールは最新です。\n\n" + summary.ToString().TrimEnd();
            EditorUtility.DisplayDialog("ツールのアップデート", message, "OK");
            return;
        }

        string confirm = "次のパッケージを更新します！\n\n" + updateSummary.ToString().TrimEnd();
        if (!EditorUtility.DisplayDialog("ツールのアップデート", confirm, "更新する", "キャンセル"))
            return;

        for (int i = 0; i < applicable.Count; i++)
        {
            float progress = 0.5f + (0.4f * i / applicable.Count);
            var plan = applicable[i];
            EditorUtility.DisplayProgressBar("ツールのアップデート", plan.DisplayName + " をダウンロード中...", progress);
            await InstallPackageAsync(plan);
            Debug.Log(LogPrefix + " " + plan.Id + " を " +
                      (string.IsNullOrEmpty(plan.LocalVersionText) ? "新規" : plan.LocalVersionText) +
                      " から " + plan.RemoteVersionText + " に更新しました。");
        }

        EditorUtility.ClearProgressBar();
        EditorUtility.DisplayDialog("ツールのアップデート", "更新しました。\n\n" + updateSummary.ToString().TrimEnd(), "OK");

        Client.Resolve();
        AssetDatabase.Refresh();
    }

    static UpdatePlan BuildPlan(TargetSpec target, RemoteVersion remote)
    {
        string packageDir = ToAbsolutePath(Path.Combine("Packages", target.Id));
        string localText = ReadInstalledVersionText(packageDir);
        Version localVersion = null;
        bool hasLocal = !string.IsNullOrEmpty(localText) && TryParseStable(localText, out localVersion);

        var plan = new UpdatePlan
        {
            Id = target.Id,
            DisplayName = remote != null && !string.IsNullOrEmpty(remote.DisplayName) ? remote.DisplayName : target.Id,
            LocalVersionText = localText,
            RemoteVersionText = remote != null ? remote.VersionText : null,
            RemoteVersion = remote != null ? remote.Version : null,
            ZipUrl = remote != null ? remote.ZipUrl : null,
            VpmDependencies = remote != null ? remote.VpmDependencies : null
        };

        if (remote == null || remote.Version == null || string.IsNullOrEmpty(remote.ZipUrl))
        {
            plan.Status = UpdateStatus.RemoteMissing;
            return plan;
        }

        if (!Directory.Exists(packageDir) || !hasLocal)
        {
            plan.Status = UpdateStatus.NotInstalled;
            return plan;
        }

        if (remote.Version > localVersion)
            plan.Status = UpdateStatus.UpdateAvailable;
        else if (remote.Version < localVersion)
            plan.Status = UpdateStatus.LocalNewer;
        else
            plan.Status = UpdateStatus.UpToDate;

        return plan;
    }

    static async Task InstallPackageAsync(UpdatePlan plan)
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "SamirinToolUpdate_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        string staging = null;
        string backup = null;
        try
        {
            string zipPath = Path.Combine(tempRoot, "package.zip");
            if (!await DownloadFileAsync(plan.ZipUrl, zipPath))
                throw new InvalidOperationException("パッケージのダウンロードに失敗しました: " + plan.ZipUrl);

            string extractRoot = Path.Combine(tempRoot, "extract");
            ZipFile.ExtractToDirectory(zipPath, extractRoot);
            string packageRoot = FindPackageRoot(extractRoot, plan.Id);
            if (string.IsNullOrEmpty(packageRoot))
                throw new InvalidOperationException("zip 内に " + plan.Id + " が見つかりません。");

            string dest = ToAbsolutePath(Path.Combine("Packages", plan.Id));
            staging = dest + ".update-staging";
            backup = dest + ".update-backup";
            DeleteDirectoryIfExists(staging);
            CopyDirectory(packageRoot, staging);

            bool hadDest = Directory.Exists(dest);
            if (hadDest)
            {
                DeleteDirectoryIfExists(backup);
                Directory.Move(dest, backup);
            }

            try
            {
                Directory.Move(staging, dest);
                staging = null;
                UpdateVpmManifest(plan);
            }
            catch
            {
                if (hadDest && Directory.Exists(backup))
                {
                    DeleteDirectoryIfExists(dest);
                    Directory.Move(backup, dest);
                    backup = null;
                }

                throw;
            }

            if (hadDest)
                DeleteDirectoryIfExists(backup);
            backup = null;
        }
        finally
        {
            DeleteDirectoryIfExists(staging);
            try
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, true);
            }
            catch (Exception e)
            {
                Debug.LogWarning(LogPrefix + " 一時フォルダの削除に失敗: " + e.Message);
            }
        }
    }

    static void UpdateVpmManifest(UpdatePlan plan)
    {
        string manifestPath = ToAbsolutePath(Path.Combine("Packages", "vpm-manifest.json"));
        if (!File.Exists(manifestPath))
            throw new InvalidOperationException("Packages/vpm-manifest.json が見つかりません。");

        string json = File.ReadAllText(manifestPath);
        string updated = UpsertPackageEntries(json, plan.Id, plan.RemoteVersionText, plan.VpmDependencies);
        string tempPath = manifestPath + ".tmp";
        File.WriteAllText(tempPath, updated, new UTF8Encoding(false));
        File.Copy(tempPath, manifestPath, true);
        File.Delete(tempPath);
    }

    /// <summary>
    /// 既存のパッケージ項目を更新し、manifest に無い場合は dependencies と locked を追加する。
    /// </summary>
    static string UpsertPackageEntries(string json, string packageId, string version, JObject vpmDependencies)
    {
        json = ReplacePackageEntries(json, packageId, version, vpmDependencies);
        if (!SectionContainsPackageObject(json, "dependencies", packageId))
            json = InsertPackageEntry(json, "dependencies", FormatDependencyEntry(packageId, version, null));
        if (!SectionContainsPackageObject(json, "locked", packageId))
            json = InsertPackageEntry(json, "locked", FormatDependencyEntry(packageId, version, vpmDependencies));
        return json;
    }

    static string FormatDependencyEntry(string packageId, string version, JObject vpmDependencies)
    {
        var builder = new StringBuilder();
        builder.Append('"').Append(packageId).Append("\": {\n");
        builder.Append("      \"version\": \"").Append(version).Append('"');
        if (vpmDependencies != null)
        {
            builder.Append(",\n      \"dependencies\": ");
            builder.Append(FormatDependencyObject(vpmDependencies));
        }

        builder.Append("\n    }");
        return builder.ToString();
    }

    static string InsertPackageEntry(string json, string sectionName, string entry)
    {
        if (!TryFindRootObjectProperty(json, sectionName, out int openBrace))
            throw new InvalidOperationException("vpm-manifest.json に " + sectionName + " がありません。");

        int close = FindMatchingBrace(json, openBrace);
        int last = close - 1;
        while (last > openBrace && char.IsWhiteSpace(json[last]))
            last--;

        bool empty = json[last] == '{';
        string prefix = empty || json[last] == ',' ? "\n    " : ",\n    ";
        return json.Substring(0, last + 1) + prefix + entry + "\n  " + json.Substring(close);
    }

    static bool SectionContainsPackageObject(string json, string sectionName, string packageId)
    {
        if (!TryFindRootObjectProperty(json, sectionName, out int openBrace))
            return false;

        int close = FindMatchingBrace(json, openBrace);
        string key = "\"" + packageId + "\":";
        int search = openBrace;
        while (search < close)
        {
            int keyIndex = json.IndexOf(key, search, close - search, StringComparison.Ordinal);
            if (keyIndex < 0)
                return false;

            int cursor = keyIndex + key.Length;
            while (cursor < close && char.IsWhiteSpace(json[cursor]))
                cursor++;
            if (cursor < close && json[cursor] == '{')
                return true;

            search = keyIndex + key.Length;
        }

        return false;
    }

    static bool TryFindRootObjectProperty(string json, string propertyName, out int openBrace)
    {
        openBrace = -1;
        int i = 0;
        while (i < json.Length && char.IsWhiteSpace(json[i]))
            i++;
        if (i >= json.Length || json[i] != '{')
            return false;

        i++;
        while (i < json.Length)
        {
            while (i < json.Length && char.IsWhiteSpace(json[i]))
                i++;
            if (i >= json.Length || json[i] == '}')
                return false;
            if (json[i] != '"')
                return false;

            int keyEnd = ReadJsonStringEnd(json, i);
            string key = json.Substring(i + 1, keyEnd - i - 1);
            i = keyEnd + 1;
            while (i < json.Length && char.IsWhiteSpace(json[i]))
                i++;
            if (i < json.Length && json[i] == ':')
                i++;
            while (i < json.Length && char.IsWhiteSpace(json[i]))
                i++;
            if (i >= json.Length)
                return false;

            if (key == propertyName && json[i] == '{')
            {
                openBrace = i;
                return true;
            }

            i = SkipJsonValue(json, i);
            while (i < json.Length && char.IsWhiteSpace(json[i]))
                i++;
            if (i < json.Length && json[i] == ',')
                i++;
        }

        return false;
    }

    static int SkipJsonValue(string json, int index)
    {
        char c = json[index];
        if (c == '{')
            return FindMatchingBrace(json, index) + 1;
        if (c == '[')
            return FindMatchingBracket(json, index) + 1;
        if (c == '"')
            return ReadJsonStringEnd(json, index) + 1;

        while (index < json.Length && json[index] != ',' && json[index] != '}' && json[index] != ']')
            index++;
        return index;
    }

    static int ReadJsonStringEnd(string json, int openQuote)
    {
        for (int i = openQuote + 1; i < json.Length; i++)
        {
            if (json[i] == '\\')
            {
                i++;
                continue;
            }

            if (json[i] == '"')
                return i;
        }

        throw new InvalidOperationException("JSON の文字列が閉じていません。");
    }

    static int FindMatchingBracket(string json, int openIndex)
    {
        int depth = 0;
        bool inString = false;
        for (int i = openIndex; i < json.Length; i++)
        {
            char c = json[i];
            if (inString)
            {
                if (c == '\\')
                {
                    i++;
                    continue;
                }

                if (c == '"')
                    inString = false;
                continue;
            }

            if (c == '"')
            {
                inString = true;
                continue;
            }

            if (c == '[')
                depth++;
            else if (c == ']')
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }

        throw new InvalidOperationException("JSON の括弧が閉じていません。");
    }

    /// <summary>
    /// オブジェクト値になっているパッケージ項目の version を書き換え、
    /// dependencies を持つ項目（locked）だけ依存関係も合わせる。
    /// </summary>
    static string ReplacePackageEntries(string json, string packageId, string version, JObject vpmDependencies)
    {
        string key = "\"" + packageId + "\":";
        var builder = new StringBuilder();
        int search = 0;

        while (search < json.Length)
        {
            int keyIndex = json.IndexOf(key, search, StringComparison.Ordinal);
            if (keyIndex < 0)
            {
                builder.Append(json, search, json.Length - search);
                break;
            }

            int cursor = keyIndex + key.Length;
            while (cursor < json.Length && char.IsWhiteSpace(json[cursor]))
                cursor++;

            if (cursor >= json.Length || json[cursor] != '{')
            {
                builder.Append(json, search, cursor - search);
                search = cursor;
                continue;
            }

            int end = FindMatchingBrace(json, cursor);
            builder.Append(json, search, cursor - search);
            string block = json.Substring(cursor, end - cursor + 1);
            block = ReplaceFirstJsonString(block, "version", version);
            if (BlockHasObjectProperty(block, "dependencies"))
                block = ReplaceObjectProperty(block, "dependencies", FormatDependencyObject(vpmDependencies));
            builder.Append(block);
            search = end + 1;
        }

        return builder.ToString();
    }

    static string ReplaceFirstJsonString(string json, string propertyName, string value)
    {
        string needle = "\"" + propertyName + "\"";
        int key = json.IndexOf(needle, StringComparison.Ordinal);
        if (key < 0)
            return json;

        int colon = json.IndexOf(':', key + needle.Length);
        if (colon < 0)
            return json;

        int openQuote = json.IndexOf('"', colon + 1);
        if (openQuote < 0)
            return json;

        int closeQuote = openQuote + 1;
        while (closeQuote < json.Length)
        {
            if (json[closeQuote] == '\\')
            {
                closeQuote += 2;
                continue;
            }

            if (json[closeQuote] == '"')
                break;
            closeQuote++;
        }

        if (closeQuote >= json.Length)
            return json;

        return json.Substring(0, openQuote + 1) + value + json.Substring(closeQuote);
    }

    static bool BlockHasObjectProperty(string json, string propertyName)
    {
        string needle = "\"" + propertyName + "\"";
        int key = json.IndexOf(needle, StringComparison.Ordinal);
        if (key < 0)
            return false;

        int cursor = key + needle.Length;
        while (cursor < json.Length && char.IsWhiteSpace(json[cursor]))
            cursor++;
        if (cursor < json.Length && json[cursor] == ':')
            cursor++;
        while (cursor < json.Length && char.IsWhiteSpace(json[cursor]))
            cursor++;
        return cursor < json.Length && json[cursor] == '{';
    }

    static string ReplaceObjectProperty(string json, string propertyName, string objectLiteral)
    {
        string needle = "\"" + propertyName + "\"";
        int key = json.IndexOf(needle, StringComparison.Ordinal);
        if (key < 0)
            return json;

        int cursor = key + needle.Length;
        while (cursor < json.Length && char.IsWhiteSpace(json[cursor]))
            cursor++;
        if (cursor < json.Length && json[cursor] == ':')
            cursor++;
        while (cursor < json.Length && char.IsWhiteSpace(json[cursor]))
            cursor++;
        if (cursor >= json.Length || json[cursor] != '{')
            return json;

        int end = FindMatchingBrace(json, cursor);
        return json.Substring(0, cursor) + objectLiteral + json.Substring(end + 1);
    }

    static string FormatDependencyObject(JObject dependencies)
    {
        if (dependencies == null || !dependencies.HasValues)
            return "{}";

        var builder = new StringBuilder();
        builder.Append("{\n");
        var properties = new List<JProperty>(dependencies.Properties());
        for (int i = 0; i < properties.Count; i++)
        {
            builder.Append("        \"");
            builder.Append(properties[i].Name);
            builder.Append("\": \"");
            builder.Append(properties[i].Value.Type == JTokenType.String
                ? (string)properties[i].Value
                : properties[i].Value.ToString());
            builder.Append('"');
            if (i < properties.Count - 1)
                builder.Append(',');
            builder.Append('\n');
        }

        builder.Append("      }");
        return builder.ToString();
    }

    static int FindMatchingBrace(string json, int openIndex)
    {
        int depth = 0;
        bool inString = false;
        for (int i = openIndex; i < json.Length; i++)
        {
            char c = json[i];
            if (inString)
            {
                if (c == '\\')
                {
                    i++;
                    continue;
                }

                if (c == '"')
                    inString = false;
                continue;
            }

            if (c == '"')
            {
                inString = true;
                continue;
            }

            if (c == '{')
                depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }

        throw new InvalidOperationException("JSON の括弧が閉じていません。");
    }

    static void LoadConfig(out List<TargetSpec> targets, out List<string> repos)
    {
        targets = new List<TargetSpec>();
        repos = new List<string>();

        string configPath = ToAbsolutePath(OptionalConfigRelativePath);
        if (!File.Exists(configPath))
        {
            AddDefaultConfig(targets, repos);
            return;
        }

        var json = JObject.Parse(File.ReadAllText(configPath));
        if (json["vpmDependencies"] is JObject dependencies)
        {
            foreach (var property in dependencies.Properties())
            {
                targets.Add(new TargetSpec
                {
                    Id = property.Name,
                    Range = property.Value.Type == JTokenType.String ? (string)property.Value : "x.x.x"
                });
            }
        }

        if (json["vpmRepositories"] is JArray repositories)
        {
            foreach (var item in repositories)
            {
                if (item.Type == JTokenType.String)
                    repos.Add((string)item);
                else if (item["url"] != null)
                    repos.Add((string)item["url"]);
            }
        }

        if (targets.Count == 0 || repos.Count == 0)
            AddDefaultConfig(targets, repos);
    }

    static void AddDefaultConfig(List<TargetSpec> targets, List<string> repos)
    {
        if (targets.Count == 0)
            targets.Add(new TargetSpec { Id = DefaultPackageId, Range = "x.x.x" });

        if (repos.Count == 0)
            repos.AddRange(DefaultRepoUrls);
    }

    static async Task<RemoteVersion> FindRemoteVersionAsync(List<string> repos, TargetSpec target)
    {
        foreach (string repo in repos)
        {
            string text = await DownloadTextAsync(repo);
            if (string.IsNullOrEmpty(text))
            {
                Debug.LogWarning(LogPrefix + " リポジトリを取得できませんでした: " + repo);
                continue;
            }

            JObject root;
            try
            {
                root = JObject.Parse(text);
            }
            catch (Exception e)
            {
                Debug.LogWarning(LogPrefix + " リポジトリの解析に失敗: " + repo + " / " + e.Message);
                continue;
            }

            if (!(root["packages"]?[target.Id] is JObject package))
                continue;

            if (!(package["versions"] is JObject versions))
                continue;

            Version best = null;
            JObject bestObject = null;
            string bestText = null;
            foreach (var property in versions.Properties())
            {
                if (!TryParseStable(property.Name, out var version))
                    continue;
                if (!RangeAllows(target.Range, version))
                    continue;
                if (best != null && version <= best)
                    continue;

                best = version;
                bestText = property.Name;
                bestObject = property.Value as JObject;
            }

            if (bestObject == null)
                return new RemoteVersion();

            return new RemoteVersion
            {
                Version = best,
                VersionText = (string)bestObject["version"] ?? bestText,
                DisplayName = (string)bestObject["displayName"],
                ZipUrl = (string)bestObject["url"],
                VpmDependencies = bestObject["vpmDependencies"] as JObject
            };
        }

        return null;
    }

    static bool RangeAllows(string range, Version version)
    {
        if (string.IsNullOrWhiteSpace(range))
            return true;

        range = range.Trim();
        if (range == "x.x.x" || range == "X.X.X" || range == "*")
            return true;

        if (range.StartsWith(">=", StringComparison.Ordinal))
        {
            if (!TryParseStable(range.Substring(2).Trim(), out var minimum))
                return true;
            return version >= minimum;
        }

        if (TryParseStable(range, out var exact))
            return version == exact;

        return true;
    }

    static bool TryParseStable(string text, out Version version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();
        if (text.IndexOf('-') >= 0)
            return false;

        int plus = text.IndexOf('+');
        if (plus >= 0)
            text = text.Substring(0, plus);

        return Version.TryParse(text, out version);
    }

    static string ReadInstalledVersionText(string packageDir)
    {
        string path = Path.Combine(packageDir, "package.json");
        if (!File.Exists(path))
            return null;

        try
        {
            var json = JObject.Parse(File.ReadAllText(path));
            return (string)json["version"];
        }
        catch (Exception e)
        {
            Debug.LogWarning(LogPrefix + " package.json の読み込みに失敗: " + e.Message);
            return null;
        }
    }

    static string FindPackageRoot(string extractRoot, string packageId)
    {
        if (IsPackageRoot(extractRoot, packageId))
            return extractRoot;

        foreach (string dir in Directory.GetDirectories(extractRoot))
        {
            if (IsPackageRoot(dir, packageId))
                return dir;
        }

        return null;
    }

    static bool IsPackageRoot(string directory, string packageId)
    {
        string path = Path.Combine(directory, "package.json");
        if (!File.Exists(path))
            return false;

        try
        {
            var json = JObject.Parse(File.ReadAllText(path));
            return string.Equals((string)json["name"], packageId, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);
        foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            string relative = file.Substring(sourceDir.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string destFile = Path.Combine(destinationDir, relative);
            string destFolder = Path.GetDirectoryName(destFile);
            if (!string.IsNullOrEmpty(destFolder))
                Directory.CreateDirectory(destFolder);
            File.Copy(file, destFile, true);
        }
    }

    static void DeleteDirectoryIfExists(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return;

        foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);

        Directory.Delete(path, true);
    }

    static async Task<string> DownloadTextAsync(string url)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 120;
            var operation = request.SendWebRequest();
            while (!operation.isDone)
                await Task.Yield();

            if (!IsRequestSuccess(request))
            {
                Debug.LogWarning(LogPrefix + " Download error: " + request.error + " / " + url);
                return null;
            }

            return request.downloadHandler.text;
        }
    }

    static async Task<bool> DownloadFileAsync(string url, string destinationPath)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 120;
            var operation = request.SendWebRequest();
            while (!operation.isDone)
                await Task.Yield();

            if (!IsRequestSuccess(request))
            {
                Debug.LogError(LogPrefix + " Download error: " + request.error + " / " + url);
                return false;
            }

            byte[] data = request.downloadHandler.data;
            if (data == null || data.Length == 0)
                return false;

            File.WriteAllBytes(destinationPath, data);
            return true;
        }
    }

    static bool IsRequestSuccess(UnityWebRequest request)
    {
#if UNITY_2020_2_OR_NEWER
        return request.result == UnityWebRequest.Result.Success;
#else
        return !request.isNetworkError && !request.isHttpError;
#endif
    }

    static string ToAbsolutePath(string relativePath)
    {
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        return Path.GetFullPath(Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    enum UpdateStatus
    {
        UpToDate,
        UpdateAvailable,
        LocalNewer,
        NotInstalled,
        RemoteMissing
    }

    sealed class TargetSpec
    {
        public string Id;
        public string Range;
    }

    sealed class RemoteVersion
    {
        public Version Version;
        public string VersionText;
        public string DisplayName;
        public string ZipUrl;
        public JObject VpmDependencies;
    }

    sealed class UpdatePlan
    {
        public string Id;
        public string DisplayName;
        public UpdateStatus Status;
        public string LocalVersionText;
        public string RemoteVersionText;
        public Version RemoteVersion;
        public string ZipUrl;
        public JObject VpmDependencies;
    }
}
