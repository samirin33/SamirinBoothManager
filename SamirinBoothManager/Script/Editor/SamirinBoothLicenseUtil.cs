using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SamirinBoothInformation 配下のライセンスファイル検索・VN3 形式パース。
/// LicenseViewer (Web) の parser.js と同等の構造を返す。
/// </summary>
public static class SamirinBoothLicenseUtil
{
    public const string InformationFolder = "Assets/samirin33/SamirinBoothInformation";
    public const string Vn3OfficialUrl = "https://www.vn3.org/";
    public const string Vn3TermsUrl = "https://www.vn3.org/terms";

    static readonly Regex ConditionLine = new Regex(
        @"^\s*([A-WX])\s+(.+?)\s*[:：]\s*(.+?)\s*$",
        RegexOptions.Compiled);

    static readonly Dictionary<string, ConditionMeta> ConditionMetas = new Dictionary<string, ConditionMeta>
    {
        ["A"] = new ConditionMeta(1, "個人利用", "個人による利用"),
        ["B"] = new ConditionMeta(1, "法人利用", "法人による利用"),
        ["C"] = new ConditionMeta(2, "ソーシャルプラットフォームへのアップロード", "ソーシャルコミュニケーションプラットフォームへのアップロード"),
        ["D"] = new ConditionMeta(2, "オンラインゲームプラットフォームへのアップロード", "オンラインゲームプラットフォームへのアップロード"),
        ["E"] = new ConditionMeta(2, "オンラインサービス内での第三者への利用の許諾", "オンラインサービス内での第三者への利用の許諾"),
        ["F"] = new ConditionMeta(3, "性的表現", "性的表現への利用"),
        ["G"] = new ConditionMeta(3, "暴力的表現", "暴力的表現への利用"),
        ["H"] = new ConditionMeta(3, "政治活動・宗教活動", "政治活動への利用および、宗教活動への利用"),
        ["I"] = new ConditionMeta(4, "調整", "調整"),
        ["J"] = new ConditionMeta(4, "改変", "改変"),
        ["K"] = new ConditionMeta(4, "他データ改変目的での利用", "他のデータを改変するための利用"),
        ["L"] = new ConditionMeta(4, "調整・改変の外部委託", "調整・改変の外部委託"),
        ["M"] = new ConditionMeta(5, "未改変状態での再配布", "未改変状態での再配布"),
        ["N"] = new ConditionMeta(5, "改変したデータの配布", "改変したデータの配布"),
        ["O"] = new ConditionMeta(6, "映像作品・配信・放送", "映像作品・配信・放送への利用"),
        ["P"] = new ConditionMeta(6, "出版物・電子出版物", "出版物・電子出版物への利用"),
        ["Q"] = new ConditionMeta(6, "有体物（グッズ）", "有体物（グッズ）への利用"),
        ["R"] = new ConditionMeta(6, "ソフトウェアへの組み込み", "製品開発等のためのソフトウェアへの組み込み"),
        ["S"] = new ConditionMeta(7, "メッシュ・ウェイト転用した衣装データの作成", "メッシュやウェイトを転用した衣装データの作成"),
        ["T"] = new ConditionMeta(7, "規格準拠の新たなデータの作成", "メッシュやウェイトを転用しない規格準拠の新たなデータ作成"),
        ["U"] = new ConditionMeta(7, "データをモチーフにした二次的著作物", "データをモチーフにした二次的著作物（いわゆる二次創作）"),
        ["V"] = new ConditionMeta(8, "クレジット表記", "クレジット表記"),
        ["W"] = new ConditionMeta(8, "権利義務の譲渡等", "権利義務の譲渡等"),
        ["X"] = new ConditionMeta(9, "特記事項", "特記事項"),
    };

    public static readonly LicenseGroupInfo[] Groups =
    {
        new LicenseGroupInfo(1, "利用主体"),
        new LicenseGroupInfo(2, "オンラインサービスへのアップロード"),
        new LicenseGroupInfo(3, "センシティブな表現"),
        new LicenseGroupInfo(4, "加工"),
        new LicenseGroupInfo(5, "再配布・配布"),
        new LicenseGroupInfo(6, "メディア・プロダクトへの使用"),
        new LicenseGroupInfo(7, "二次創作"),
        new LicenseGroupInfo(8, "その他"),
        new LicenseGroupInfo(9, "特記事項"),
    };

    public sealed class LicenseEntry
    {
        public string Id;
        public string Product;
        public string Title;
        public string FileName;
        public string AssetPath;
        public string RelativePath;
    }

    public sealed class LicenseDocument
    {
        public string Title;
        public string Product;
        public string Version;
        public string RawText;
        public string AssetPath;
        public LicenseMeta Meta = new LicenseMeta();
        public Dictionary<string, LicenseCondition> Conditions = new Dictionary<string, LicenseCondition>();
    }

    public sealed class LicenseMeta
    {
        public string Target;
        public string RightsHolder;
        public string Contact;
        public string Credit;
        public string Hashtags;
        public string Term;
        public string Version;
        public string SpecialNote;
    }

    public sealed class LicenseCondition
    {
        public string Code;
        public string Label;
        public string Full;
        public int Group;
        public string Value;
        public LicenseStatus Status;
    }

    public sealed class LicenseStatus
    {
        public string Kind;
        public string Label;
    }

    public readonly struct LicenseGroupInfo
    {
        public readonly int Id;
        public readonly string Title;

        public LicenseGroupInfo(int id, string title)
        {
            Id = id;
            Title = title;
        }
    }

    readonly struct ConditionMeta
    {
        public readonly int Group;
        public readonly string Label;
        public readonly string Full;

        public ConditionMeta(int group, string label, string full)
        {
            Group = group;
            Label = label;
            Full = full;
        }
    }

    public static List<LicenseEntry> CollectLicenseEntries()
    {
        var results = new List<LicenseEntry>();
        if (!AssetDatabase.IsValidFolder(InformationFolder))
            return results;

        var guids = AssetDatabase.FindAssets("t:TextAsset", new[] { InformationFolder });
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i])?.Replace("\\", "/");
            if (string.IsNullOrEmpty(path) || !IsLicenseFilePath(path))
                continue;
            if (!seen.Add(path))
                continue;

            var fileName = Path.GetFileName(path);
            var product = Path.GetFileName(Path.GetDirectoryName(path)) ?? string.Empty;
            if (string.Equals(product, "Script", StringComparison.OrdinalIgnoreCase)
                || string.Equals(product, "SamirinBoothInformation", StringComparison.OrdinalIgnoreCase))
                continue;

            var relative = path.StartsWith(InformationFolder + "/", StringComparison.OrdinalIgnoreCase)
                ? path.Substring(InformationFolder.Length + 1)
                : path;

            results.Add(new LicenseEntry
            {
                Id = product,
                Product = product,
                Title = ResolveProductTitle(product),
                FileName = fileName,
                AssetPath = path,
                RelativePath = relative,
            });
        }

        results.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase));
        return results;
    }

    public static bool TryFindLicensePath(SamirinBoothAssetInfo info, out string assetPath)
    {
        assetPath = null;
        if (info == null)
            return false;

        foreach (var folder in EnumerateCandidateFolders(info))
        {
            if (TryFindLicenseInFolder(folder, out assetPath))
                return true;
        }

        return false;
    }

    public static bool TryLoadDocument(SamirinBoothAssetInfo info, out LicenseDocument document)
    {
        document = null;
        if (!TryFindLicensePath(info, out var path))
            return false;

        return TryLoadDocumentAtPath(path, info.name, out document);
    }

    public static bool TryLoadDocumentAtPath(string assetPath, string fallbackTitle, out LicenseDocument document)
    {
        document = null;
        if (string.IsNullOrEmpty(assetPath))
            return false;

        var text = ReadText(assetPath);
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var product = Path.GetFileName(Path.GetDirectoryName(assetPath)) ?? string.Empty;
        document = ParseLicenseText(text, product, fallbackTitle ?? product);
        document.AssetPath = assetPath;
        return true;
    }

    public static LicenseDocument ParseLicenseText(string rawText, string product, string title)
    {
        var text = (rawText ?? string.Empty).TrimStart('\uFEFF');
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var meta = new LicenseMeta();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            TryAssignMeta(line, "【許諾対象データ】", ref meta.Target);
            TryAssignMeta(line, "【権利者】", ref meta.RightsHolder);
            TryAssignMeta(line, "【問い合わせ先】", ref meta.Contact);
            TryAssignMeta(line, "【クレジット表記】", ref meta.Credit);
            TryAssignMeta(line, "【推奨ハッシュタグ】", ref meta.Hashtags);
            TryAssignMeta(line, "【利用規約バージョン】", ref meta.Version);
        }

        meta.Term = ExtractBlock(
            lines,
            line => line.StartsWith("【許諾期間】", StringComparison.Ordinal),
            line => line.StartsWith("【", StringComparison.Ordinal)
                || line.StartsWith("───", StringComparison.Ordinal)
                || line.StartsWith("═══", StringComparison.Ordinal)
                || line.StartsWith("【個別条件】", StringComparison.Ordinal)
                || line.StartsWith("【X ", StringComparison.Ordinal));

        meta.SpecialNote = ExtractBlock(
            lines,
            line => line.StartsWith("【X 特記事項】", StringComparison.Ordinal),
            line => line.StartsWith("───", StringComparison.Ordinal)
                || line.StartsWith("═══", StringComparison.Ordinal)
                || line.StartsWith("本記載のほか", StringComparison.Ordinal)
                || line.StartsWith("【", StringComparison.Ordinal));

        var conditions = new Dictionary<string, LicenseCondition>();
        for (int i = 0; i < lines.Length; i++)
        {
            var match = ConditionLine.Match(lines[i]);
            if (!match.Success)
                continue;

            var code = match.Groups[1].Value;
            var shortLabel = match.Groups[2].Value.Trim();
            var value = match.Groups[3].Value.Trim();
            var known = ConditionMetas.TryGetValue(code, out var cm)
                ? cm
                : new ConditionMeta(0, shortLabel, shortLabel);

            conditions[code] = new LicenseCondition
            {
                Code = code,
                Label = known.Label,
                Full = known.Full,
                Group = known.Group,
                Value = value,
                Status = ClassifyStatus(value),
            };
        }

        if (!conditions.ContainsKey("X") && !string.IsNullOrWhiteSpace(meta.SpecialNote))
        {
            var x = ConditionMetas["X"];
            conditions["X"] = new LicenseCondition
            {
                Code = "X",
                Label = x.Label,
                Full = x.Full,
                Group = 9,
                Value = meta.SpecialNote,
                Status = new LicenseStatus { Kind = "other", Label = meta.SpecialNote },
            };
        }

        var titleGuess = !string.IsNullOrWhiteSpace(title)
            ? title
            : (!string.IsNullOrWhiteSpace(meta.Target) && meta.Target != "—"
                ? meta.Target
                : (product ?? "利用規約"));

        return new LicenseDocument
        {
            Title = titleGuess,
            Product = product ?? string.Empty,
            Version = string.IsNullOrWhiteSpace(meta.Version) ? "1.10" : meta.Version,
            Meta = meta,
            Conditions = conditions,
            RawText = text,
        };
    }

    public static LicenseStatus ClassifyStatus(string value)
    {
        var text = (value ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(text) || text == "—" || text == "-" || text == "ー")
            return new LicenseStatus { Kind = "empty", Label = "—" };

        if (Regex.IsMatch(text, @"^許可") && !Regex.IsMatch(text, @"不許可|許可しません|許可しない"))
            return new LicenseStatus { Kind = "allow", Label = text };

        if (Regex.IsMatch(text, @"不許可|許可しません|許可しない|禁止"))
            return new LicenseStatus { Kind = "deny", Label = text };

        if (Regex.IsMatch(text, @"必要|要（|要$|^要"))
            return new LicenseStatus { Kind = "required", Label = text };

        if (text.Contains("不要"))
            return new LicenseStatus { Kind = "optional", Label = text };

        if (Regex.IsMatch(text, @"問い合わせ|要確認|個別"))
            return new LicenseStatus { Kind = "ask", Label = text };

        return new LicenseStatus { Kind = "other", Label = text };
    }

    static IEnumerable<string> EnumerateCandidateFolders(SamirinBoothAssetInfo info)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string Try(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                return null;
            var normalized = folder.Replace("\\", "/").TrimEnd('/');
            return seen.Add(normalized) ? normalized : null;
        }

        var infoPath = AssetDatabase.GetAssetPath(info)?.Replace("\\", "/");
        if (!string.IsNullOrEmpty(infoPath))
        {
            var parent = Path.GetDirectoryName(infoPath)?.Replace("\\", "/");
            var path = Try(parent);
            if (path != null)
                yield return path;
        }

        if (!string.IsNullOrWhiteSpace(info.folderName))
        {
            var path = Try(InformationFolder + "/" + info.folderName.Trim());
            if (path != null)
                yield return path;
        }

        if (!string.IsNullOrWhiteSpace(info.name))
        {
            var path = Try(InformationFolder + "/" + info.name.Trim());
            if (path != null)
                yield return path;
        }
    }

    static bool TryFindLicenseInFolder(string folder, out string assetPath)
    {
        assetPath = null;
        if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
            return false;

        var preferred = folder + "/VN3License.txt";
        if (File.Exists(ToAbsolute(preferred)))
        {
            assetPath = preferred;
            return true;
        }

        var guids = AssetDatabase.FindAssets("t:TextAsset", new[] { folder });
        for (int i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i])?.Replace("\\", "/");
            if (string.IsNullOrEmpty(path))
                continue;

            // 直下のみ
            var parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            if (!string.Equals(parent, folder, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!IsLicenseFilePath(path))
                continue;

            assetPath = path;
            return true;
        }

        return false;
    }

    static bool IsLicenseFilePath(string path)
    {
        if (!path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            return false;

        var name = Path.GetFileNameWithoutExtension(path);
        return name.IndexOf("License", StringComparison.OrdinalIgnoreCase) >= 0
            || string.Equals(name, "VN3License", StringComparison.OrdinalIgnoreCase);
    }

    static string ResolveProductTitle(string product)
    {
        if (string.IsNullOrEmpty(product))
            return product;

        var infoPath = $"{InformationFolder}/{product}/Info_{product}.asset";
        var info = AssetDatabase.LoadAssetAtPath<SamirinBoothAssetInfo>(infoPath);
        if (info != null && !string.IsNullOrWhiteSpace(info.name))
            return info.name;

        var guids = AssetDatabase.FindAssets("t:SamirinBoothAssetInfo", new[] { $"{InformationFolder}/{product}" });
        for (int i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var loaded = AssetDatabase.LoadAssetAtPath<SamirinBoothAssetInfo>(path);
            if (loaded != null && !string.IsNullOrWhiteSpace(loaded.name))
                return loaded.name;
        }

        return product;
    }

    static string ReadText(string assetPath)
    {
        var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
        if (textAsset != null && !string.IsNullOrEmpty(textAsset.text))
            return textAsset.text;

        var absolute = ToAbsolute(assetPath);
        if (File.Exists(absolute))
        {
            try
            {
                return File.ReadAllText(absolute);
            }
            catch
            {
                // ignore
            }
        }

        return null;
    }

    static string ToAbsolute(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
    }

    static void TryAssignMeta(string line, string prefix, ref string field)
    {
        if (!line.StartsWith(prefix, StringComparison.Ordinal))
            return;
        field = line.Substring(prefix.Length).Trim();
    }

    static string ExtractBlock(string[] lines, Func<string, bool> isStart, Func<string, bool> isEnd)
    {
        var start = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (isStart(lines[i]))
            {
                start = i;
                break;
            }
        }

        if (start < 0)
            return string.Empty;

        var collected = new List<string>();
        for (int i = start + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (isEnd(line))
                break;
            collected.Add(line);
        }

        return string.Join("\n", collected).Trim();
    }
}
