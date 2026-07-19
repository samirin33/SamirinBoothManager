using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace samirin33.SamirinBoothManager.UI.Parts
{
    /// <summary>
    /// Assets / Packages 双方からインポート済みパッケージ情報を解決する。
    /// フォルダはあるが json が無い場合はインポート済み・バージョン不明とする。
    /// </summary>
    static class SamirinBoothImportUtil
    {
        public static bool IsImported(SamirinBoothAssetInfo info)
        {
            return TryGetInstalledVersion(info, out _);
        }

        /// <summary>
        /// インポート済みなら true。
        /// version が null のときはインポート済みだがバージョン不明。
        /// </summary>
        public static bool TryGetInstalledVersion(SamirinBoothAssetInfo info, out Version version)
        {
            version = null;
            if (info == null || string.IsNullOrEmpty(info.folderName))
                return false;

            var jsonPath = FindPackageInfoAbsolutePath(info.folderName);
            if (!string.IsNullOrEmpty(jsonPath))
            {
                version = ReadVersionFromJson(jsonPath);
                // json はあるが version が読めなくてもインポート済み（不明）
                return true;
            }

            // フォルダのみ存在する → インポート済み・バージョン不明
            if (PackageFolderExists(info.folderName))
                return true;

            return false;
        }

        /// <summary>
        /// Assets/samirin33/{folderName}/PackageAssetInfo.json
        /// または Packages/{folderName}/package.json（および Packages 内の一致ディレクトリ）を探す。
        /// </summary>
        public static string FindPackageInfoAbsolutePath(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName))
                return null;

            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(projectRoot))
                return null;

            var assetsInfo = Path.Combine(projectRoot, "Assets", "samirin33", folderName, "PackageAssetInfo.json");
            if (File.Exists(assetsInfo))
                return assetsInfo;

            var packagesRoot = Path.Combine(projectRoot, "Packages");
            if (!Directory.Exists(packagesRoot))
                return null;

            var directPackage = Path.Combine(packagesRoot, folderName, "package.json");
            if (File.Exists(directPackage))
                return directPackage;

            string matchedByName = null;
            try
            {
                var dirs = Directory.GetDirectories(packagesRoot);
                for (int i = 0; i < dirs.Length; i++)
                {
                    var dir = dirs[i];
                    var dirName = Path.GetFileName(dir);
                    var packageJson = Path.Combine(dir, "package.json");
                    if (!File.Exists(packageJson))
                        continue;

                    if (MatchesFolderName(dirName, folderName))
                        return packageJson;

                    if (matchedByName == null && PackageJsonNameEquals(packageJson, folderName))
                        matchedByName = packageJson;
                }
            }
            catch
            {
                return matchedByName;
            }

            return matchedByName;
        }

        /// <summary>
        /// Assets/samirin33/{folderName} または Packages 内の対応フォルダがあるか。
        /// </summary>
        public static bool PackageFolderExists(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName))
                return false;

            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(projectRoot))
                return false;

            var assetsFolder = Path.Combine(projectRoot, "Assets", "samirin33", folderName);
            if (Directory.Exists(assetsFolder))
                return true;

            var packagesRoot = Path.Combine(projectRoot, "Packages");
            if (!Directory.Exists(packagesRoot))
                return false;

            var directPackage = Path.Combine(packagesRoot, folderName);
            if (Directory.Exists(directPackage))
                return true;

            try
            {
                var dirs = Directory.GetDirectories(packagesRoot);
                for (int i = 0; i < dirs.Length; i++)
                {
                    var dirName = Path.GetFileName(dirs[i]);
                    if (MatchesFolderName(dirName, folderName))
                        return true;

                    var packageJson = Path.Combine(dirs[i], "package.json");
                    if (File.Exists(packageJson) && PackageJsonNameEquals(packageJson, folderName))
                        return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        static bool MatchesFolderName(string dirName, string folderName)
        {
            if (string.Equals(dirName, folderName, StringComparison.OrdinalIgnoreCase))
                return true;

            // folderName が末尾セグメントの場合（例: avatar-editor → com....avatar-editor）
            return dirName.EndsWith("." + folderName, StringComparison.OrdinalIgnoreCase);
        }

        static bool PackageJsonNameEquals(string absolutePath, string folderName)
        {
            try
            {
                var json = File.ReadAllText(absolutePath);
                var match = Regex.Match(json, "\"name\"\\s*:\\s*\"([^\"]+)\"");
                if (!match.Success)
                    return false;

                return string.Equals(match.Groups[1].Value, folderName, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static Version ReadVersionFromJson(string absolutePath)
        {
            try
            {
                var json = File.ReadAllText(absolutePath);
                var match = Regex.Match(json, "\"version\"\\s*:\\s*\"([^\"]+)\"");
                if (!match.Success)
                    return null;

                return ParseVersion(match.Groups[1].Value);
            }
            catch
            {
                return null;
            }
        }

        public static Version ParseVersion(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var parts = text.Trim().Split('.');
            int major = parts.Length > 0 && int.TryParse(parts[0], out var ma) ? ma : 0;
            int minor = parts.Length > 1 && int.TryParse(parts[1], out var mi) ? mi : 0;
            int patch = parts.Length > 2 && int.TryParse(parts[2], out var pa) ? pa : 0;
            return new Version(major, minor, patch);
        }

        public static string FormatInstalledVersion(Version version)
        {
            if (version == null)
                return "不明バージョン";
            return $"ver{version.Major}.{version.Minor}.{version.Build}";
        }
    }
}
