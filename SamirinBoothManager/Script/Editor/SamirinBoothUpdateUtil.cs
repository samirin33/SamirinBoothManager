using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace samirin33.SamirinBoothManager.UI.Parts
{
    /// <summary>
    /// インポート済みアセットのうち、新しいバージョンがあるものを収集する。
    /// </summary>
    static class SamirinBoothUpdateUtil
    {
        const string InformationFolder = "Assets/samirin33/SamirinBoothInformation";
        const string IgnorePrefsPrefix = "samirin33.UpdateRemind.Ignore.";
        const string IgnoreRegistryKey = "samirin33.UpdateRemind.IgnoreRegistry";

        public static List<SamirinBoothAssetInfo> CollectOutdatedAssets(bool includeIgnored = false)
        {
            var results = new List<SamirinBoothAssetInfo>();
            var guids = AssetDatabase.FindAssets("t:SamirinBoothAssetInfo", new[] { InformationFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]).Replace('\\', '/');
                if (path.EndsWith("/Manger.asset", StringComparison.OrdinalIgnoreCase))
                    continue;

                var info = AssetDatabase.LoadAssetAtPath<SamirinBoothAssetInfo>(path);
                if (info == null)
                    continue;

                if (!HasUpdatableCategory(info.category))
                    continue;

                if (!info.updateRemind)
                    continue;

                if (!TryGetLatestVersion(info, out var latest))
                    continue;

                if (!SamirinBoothImportUtil.TryGetInstalledVersion(info, out var installed))
                    continue;

                // バージョン不明、または最新より古いものだけ対象
                if (installed != null && installed >= latest)
                    continue;

                if (!includeIgnored && IsIgnored(info, latest))
                    continue;

                results.Add(info);
            }

            results.Sort((a, b) => string.Compare(a?.name, b?.name, StringComparison.OrdinalIgnoreCase));
            return results;
        }

        public static bool HasUpdatableCategory(Category category)
        {
            return category != Category.Other;
        }

        public static bool TryGetLatestVersion(SamirinBoothAssetInfo info, out Version version)
        {
            version = null;
            if (info == null)
                return false;

            version = new Version(
                Math.Max(0, info.majorVertion),
                Math.Max(0, info.minorVertion),
                Math.Max(0, info.patchVertion));
            return true;
        }

        public static string FormatVersion(Version version)
        {
            if (version == null)
                return "0.0.0";
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }

        public static bool IsIgnored(SamirinBoothAssetInfo info, Version latest)
        {
            if (info == null || string.IsNullOrEmpty(info.folderName) || latest == null)
                return false;

            var ignored = EditorPrefs.GetString(IgnoreKey(info.folderName), string.Empty);
            return string.Equals(ignored, FormatVersion(latest), StringComparison.Ordinal);
        }

        public static void IgnoreLatest(SamirinBoothAssetInfo info)
        {
            if (info == null || string.IsNullOrEmpty(info.folderName))
                return;
            if (!TryGetLatestVersion(info, out var latest))
                return;

            EditorPrefs.SetString(IgnoreKey(info.folderName), FormatVersion(latest));
            RegisterIgnoredFolder(info.folderName);
        }

        public static void IgnoreLatest(IEnumerable<SamirinBoothAssetInfo> infos)
        {
            if (infos == null)
                return;

            foreach (var info in infos)
                IgnoreLatest(info);
        }

        /// <summary>
        /// 無視したアップデート設定をすべて解除する。解除した件数を返す。
        /// </summary>
        public static int ClearAllIgnores()
        {
            var folders = LoadIgnoreRegistry();

            var guids = AssetDatabase.FindAssets("t:SamirinBoothAssetInfo", new[] { InformationFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var info = AssetDatabase.LoadAssetAtPath<SamirinBoothAssetInfo>(path);
                if (info != null && !string.IsNullOrEmpty(info.folderName))
                    folders.Add(info.folderName);
            }

            int cleared = 0;
            foreach (var folderName in folders)
            {
                var key = IgnoreKey(folderName);
                if (!EditorPrefs.HasKey(key))
                    continue;

                EditorPrefs.DeleteKey(key);
                cleared++;
            }

            EditorPrefs.DeleteKey(IgnoreRegistryKey);
            return cleared;
        }

        public static int CountIgnoredEntries()
        {
            var folders = LoadIgnoreRegistry();
            var guids = AssetDatabase.FindAssets("t:SamirinBoothAssetInfo", new[] { InformationFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var info = AssetDatabase.LoadAssetAtPath<SamirinBoothAssetInfo>(path);
                if (info != null && !string.IsNullOrEmpty(info.folderName))
                    folders.Add(info.folderName);
            }

            int count = 0;
            foreach (var folderName in folders)
            {
                if (EditorPrefs.HasKey(IgnoreKey(folderName)))
                    count++;
            }

            return count;
        }

        static void RegisterIgnoredFolder(string folderName)
        {
            var folders = LoadIgnoreRegistry();
            if (!folders.Add(folderName))
                return;

            SaveIgnoreRegistry(folders);
        }

        static HashSet<string> LoadIgnoreRegistry()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var raw = EditorPrefs.GetString(IgnoreRegistryKey, string.Empty);
            if (string.IsNullOrEmpty(raw))
                return result;

            var parts = raw.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
                result.Add(parts[i]);
            return result;
        }

        static void SaveIgnoreRegistry(HashSet<string> folders)
        {
            if (folders == null || folders.Count == 0)
            {
                EditorPrefs.DeleteKey(IgnoreRegistryKey);
                return;
            }

            EditorPrefs.SetString(IgnoreRegistryKey, string.Join("|", folders));
        }

        static string IgnoreKey(string folderName)
        {
            return IgnorePrefsPrefix + folderName;
        }
    }
}
