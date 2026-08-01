using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// SamirinBoothInformation 配下のライセンスを表示するウィンドウ。
/// レイアウトは LicenseViewer (Web) を参考にする。
/// </summary>
public class SBM_LicenseViewer : EditorWindow
{
    const string StylePath = "Assets/samirin33/SamirinBoothManager/UI/Style/SBM_LicenseViewer.uss";
    static readonly Regex UrlRegex = new Regex(
        @"https?://[^\s<>""']+",
        RegexOptions.Compiled);

    VisualElement _breadcrumb;
    Label _status;
    VisualElement _app;
    string _pendingAssetPath;
    string _pendingTitle;

    public static void ShowForAsset(SamirinBoothAssetInfo info)
    {
        if (info == null)
            return;

        if (!SamirinBoothLicenseUtil.TryFindLicensePath(info, out var path))
        {
            EditorUtility.DisplayDialog(
                "ライセンス",
                "このアイテムのライセンスファイルが見つかりませんでした。",
                "OK");
            return;
        }

        OpenWindow(path, info.name);
    }

    public static void ShowIndex()
    {
        OpenWindow(null, null);
    }

    static void OpenWindow(string assetPath, string title)
    {
        var window = GetWindow<SBM_LicenseViewer>(utility: false);
        window.titleContent = new GUIContent("商品ライセンス");
        window.minSize = new Vector2(520, 640);
        window._pendingAssetPath = assetPath;
        window._pendingTitle = title;
        window.Show();
        window.Focus();
        window.ApplyPending();
    }

    public void CreateGUI()
    {
        rootVisualElement.Clear();

        var fontAsset = SamirinBoothFontUtil.EnsureFontAsset();

        var root = new VisualElement();
        root.AddToClassList("lv-root");
        root.style.flexGrow = 1;
        if (fontAsset != null)
            root.style.unityFontDefinition = new StyleFontDefinition(fontAsset);
        rootVisualElement.Add(root);

        var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
        if (style != null)
            root.styleSheets.Add(style);

        root.Add(BuildTopBar());

        var shell = new VisualElement();
        shell.AddToClassList("lv-shell");
        shell.style.flexGrow = 1;
        root.Add(shell);

        var metaRow = new VisualElement();
        metaRow.AddToClassList("lv-meta-row");
        shell.Add(metaRow);

        _breadcrumb = new VisualElement();
        _breadcrumb.AddToClassList("lv-breadcrumb");
        metaRow.Add(_breadcrumb);

        _status = new Label();
        _status.AddToClassList("lv-status");
        metaRow.Add(_status);

        var surface = new VisualElement();
        surface.AddToClassList("lv-surface");
        surface.style.flexGrow = 1;
        shell.Add(surface);

        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.AddToClassList("lv-scroll");
        scroll.style.flexGrow = 1;
        surface.Add(scroll);

        _app = new VisualElement();
        _app.style.flexGrow = 1;
        scroll.Add(_app);

        ApplyPending();
    }

    void ApplyPending()
    {
        if (_app == null)
            return;

        if (!string.IsNullOrEmpty(_pendingAssetPath))
        {
            ShowLicense(_pendingAssetPath, _pendingTitle);
            _pendingAssetPath = null;
            _pendingTitle = null;
            return;
        }

        ShowIndexView();
    }

    VisualElement BuildTopBar()
    {
        var bar = new VisualElement();
        bar.AddToClassList("lv-top-bar");
        bar.style.flexShrink = 0;

        var leading = new VisualElement();
        leading.AddToClassList("lv-top-leading");
        leading.style.minWidth = 0;
        leading.style.flexShrink = 1;
        leading.style.overflow = Overflow.Hidden;
        bar.Add(leading);

        var mark = new VisualElement();
        mark.AddToClassList("lv-top-mark");
        mark.style.flexShrink = 0;
        mark.Add(MakeLabel("LIC", "lv-top-mark-label"));
        leading.Add(mark);

        var titles = new VisualElement();
        titles.AddToClassList("lv-top-titles");
        titles.style.minWidth = 0;
        titles.style.flexShrink = 1;
        titles.style.overflow = Overflow.Hidden;
        titles.Add(MakeLabel("samirin33's Booth", "lv-top-title"));
        titles.Add(MakeLabel("商品ライセンス一覧", "lv-top-subtitle"));
        leading.Add(titles);

        var actions = new VisualElement();
        actions.AddToClassList("lv-top-actions");
        actions.style.flexGrow = 0;
        actions.style.flexShrink = 0;
        bar.Add(actions);

        actions.Add(MakeButton("一覧", "lv-btn-text", ShowIndexView));
        actions.Add(MakeButton("VN3公式", "lv-btn-tonal", () => Application.OpenURL(SamirinBoothLicenseUtil.Vn3OfficialUrl)));

        return bar;
    }

    void ShowIndexView()
    {
        SetBreadcrumb(new[] { ("Licenses", (Action)null) });
        SetStatus(string.Empty, false);
        titleContent = new GUIContent("商品ライセンス");

        _app.Clear();
        var entries = SamirinBoothLicenseUtil.CollectLicenseEntries();
        if (entries.Count == 0)
        {
            var empty = new VisualElement();
            empty.AddToClassList("lv-empty");
            empty.Add(MakeLabel("License Viewer", "lv-eyebrow"));
            empty.Add(MakeLabel("ライセンス一覧", "lv-headline"));
            empty.Add(MakeLabel("表示できるライセンスファイルがありません！", "lv-body"));
            _app.Add(empty);
            return;
        }

        var header = new VisualElement();
        header.Add(MakeLabel("Samirin Booth License Viewer", "lv-eyebrow"));
        header.Add(MakeLabel("ライセンス一覧", "lv-headline"));
        _app.Add(header);

        var grid = new VisualElement();
        grid.AddToClassList("lv-grid");
        _app.Add(grid);

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var card = new VisualElement();
            card.AddToClassList("lv-card");
            card.pickingMode = PickingMode.Position;
            card.Add(MakeLabel("VN3", "lv-card-kicker"));
            card.Add(MakeLabel(entry.Title ?? entry.Product, "lv-card-title"));
            card.Add(MakeLabel(entry.RelativePath, "lv-card-path"));

            var path = entry.AssetPath;
            var title = entry.Title;
            card.RegisterCallback<ClickEvent>(_ => ShowLicense(path, title));
            grid.Add(card);
        }

        // SetStatus($"{entries.Count} 件のライセンスを検出", false);
    }

    void ShowLicense(string assetPath, string fallbackTitle)
    {
        SetBreadcrumb(new[]
        {
            ("Licenses", (Action)ShowIndexView),
            (fallbackTitle ?? "License", (Action)null),
        });

        _app.Clear();
        if (!SamirinBoothLicenseUtil.TryLoadDocumentAtPath(assetPath, fallbackTitle, out var doc))
        {
            var empty = new VisualElement();
            empty.AddToClassList("lv-empty");
            empty.Add(MakeLabel("Error", "lv-eyebrow"));
            empty.Add(MakeLabel("読み込みに失敗しました", "lv-headline"));
            empty.Add(MakeLabel("ライセンスファイルを取得できませんでした。", "lv-body"));
            _app.Add(empty);
            SetStatus("読み込み失敗", true);
            return;
        }

        titleContent = new GUIContent($"{doc.Title} 利用規約");
        SetStatus(assetPath, false);
        _app.Add(BuildDocument(doc));
    }

    VisualElement BuildDocument(SamirinBoothLicenseUtil.LicenseDocument doc)
    {
        var root = new VisualElement();

        var header = new VisualElement();
        header.Add(MakeLabel("VN3 License based terms", "lv-eyebrow"));
        header.Add(MakeLabel(doc.Title, "lv-headline"));
        root.Add(header);

        var summary = new VisualElement();
        summary.AddToClassList("lv-section");
        summary.Add(MakeLabel("許諾範囲の簡易一覧", "lv-section-title"));
        summary.Add(BuildSummary(doc));
        root.Add(summary);

        var meta = new VisualElement();
        meta.AddToClassList("lv-section");
        meta.Add(MakeLabel("権利者情報・表記", "lv-section-title"));
        meta.Add(BuildMetaPanel(doc));
        root.Add(meta);

        var raw = new VisualElement();
        raw.AddToClassList("lv-section");
        raw.Add(MakeLabel("利用規約本文（収録テキスト）", "lv-section-title"));

        var note = new VisualElement();
        note.Add(MakeLabel(
            $"本データは VN3ライセンス（Ver.{doc.Version}）に準拠します。基本条項の詳細は VN3公式を参照してください。",
            "lv-body"));
        var links = new VisualElement();
        links.style.flexDirection = FlexDirection.Row;
        links.style.flexWrap = Wrap.Wrap;
        links.Add(MakeLinkLabel("VN3公式の本文・解説", SamirinBoothLicenseUtil.Vn3TermsUrl));
        links.Add(MakeLabel("  /  ", "lv-body"));
        links.Add(MakeLinkLabel("vn3.org", SamirinBoothLicenseUtil.Vn3OfficialUrl));
        note.Add(links);
        raw.Add(note);

        var rawLabel = MakeLabel(doc.RawText, "lv-raw");
        raw.Add(rawLabel);
        root.Add(raw);

        return root;
    }

    VisualElement BuildSummary(SamirinBoothLicenseUtil.LicenseDocument doc)
    {
        var container = new VisualElement();
        var groups = SamirinBoothLicenseUtil.Groups;

        for (int g = 0; g < groups.Length; g++)
        {
            var group = groups[g];
            var rows = new List<SamirinBoothLicenseUtil.LicenseCondition>();
            foreach (var pair in doc.Conditions)
            {
                if (pair.Value != null && pair.Value.Group == group.Id)
                    rows.Add(pair.Value);
            }

            if (rows.Count == 0)
                continue;

            rows.Sort((a, b) => string.CompareOrdinal(a.Code, b.Code));

            var section = new VisualElement();
            section.AddToClassList("lv-summary-group");

            var title = MakeLabel($"{group.Id}. {group.Title}", "lv-group-title");
            section.Add(title);

            for (int i = 0; i < rows.Count; i++)
            {
                var rowData = rows[i];
                var row = new VisualElement();
                row.AddToClassList("lv-row");

                row.Add(MakeLabel(rowData.Code, "lv-code"));
                row.Add(MakeLabel(rowData.Full ?? rowData.Label, "lv-cond-label"));

                var valueHost = new VisualElement();
                valueHost.AddToClassList("lv-cond-value");
                if (group.Id == 9)
                {
                    valueHost.Add(MakeLabel(rowData.Value, "lv-note-block"));
                }
                else
                {
                    var status = rowData.Status ?? new SamirinBoothLicenseUtil.LicenseStatus
                    {
                        Kind = "other",
                        Label = rowData.Value,
                    };
                    var chip = MakeLabel(status.Label, "lv-status-chip", $"lv-status-{status.Kind}");
                    chip.style.whiteSpace = WhiteSpace.NoWrap;
                    chip.style.flexShrink = 0;
                    valueHost.Add(chip);
                }

                row.Add(valueHost);
                section.Add(row);
            }

            container.Add(section);
        }

        return container;
    }

    VisualElement BuildMetaPanel(SamirinBoothLicenseUtil.LicenseDocument doc)
    {
        var grid = new VisualElement();
        grid.AddToClassList("lv-meta-grid");

        AddMetaItem(grid, "許諾対象データ", doc.Meta.Target, false);
        AddMetaItem(grid, "権利者", doc.Meta.RightsHolder, false);
        AddMetaItem(grid, "問い合わせ先", doc.Meta.Contact, false);
        AddMetaItem(grid, "クレジット表記", doc.Meta.Credit, false);
        AddMetaItem(grid, "推奨ハッシュタグ", doc.Meta.Hashtags, false);
        AddMetaItem(grid, "利用規約バージョン", string.IsNullOrWhiteSpace(doc.Meta.Version) ? doc.Version : doc.Meta.Version, false);

        if (!string.IsNullOrWhiteSpace(doc.Meta.Term))
            AddMetaItem(grid, "許諾期間および許諾の変更等", doc.Meta.Term, true);

        return grid;
    }

    void AddMetaItem(VisualElement grid, string label, string value, bool wide)
    {
        var item = new VisualElement();
        item.AddToClassList("lv-meta-item");
        if (wide)
            item.AddToClassList("lv-meta-item-wide");

        item.Add(MakeLabel(label, "lv-meta-dt"));

        var text = (value ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(text) || text == "—" || text == "-" || text == "ー")
        {
            var muted = MakeLabel("—", "lv-meta-dd", "lv-muted");
            item.Add(muted);
        }
        else
        {
            item.Add(BuildLinkedText(text, "lv-meta-dd"));
        }

        grid.Add(item);
    }

    VisualElement BuildLinkedText(string text, params string[] classNames)
    {
        var host = new VisualElement();
        host.style.flexDirection = FlexDirection.Row;
        host.style.flexWrap = Wrap.Wrap;

        var matches = UrlRegex.Matches(text);
        if (matches.Count == 0)
            return MakeLabel(text, classNames);

        var index = 0;
        foreach (Match match in matches)
        {
            if (match.Index > index)
            {
                var before = text.Substring(index, match.Index - index);
                host.Add(MakeLabel(before, classNames));
            }

            var url = match.Value;
            host.Add(MakeLinkLabel(url, url, classNames));
            index = match.Index + match.Length;
        }

        if (index < text.Length)
            host.Add(MakeLabel(text.Substring(index), classNames));

        return host;
    }

    void SetBreadcrumb((string label, Action onClick)[] parts)
    {
        if (_breadcrumb == null)
            return;

        _breadcrumb.Clear();
        for (int i = 0; i < parts.Length; i++)
        {
            if (i > 0)
                _breadcrumb.Add(MakeLabel("/", "lv-crumb", "lv-crumb-sep"));

            var part = parts[i];
            if (part.onClick != null && i < parts.Length - 1)
            {
                var link = MakeLabel(part.label, "lv-crumb", "lv-crumb-link");
                link.pickingMode = PickingMode.Position;
                var action = part.onClick;
                link.RegisterCallback<ClickEvent>(_ => action());
                _breadcrumb.Add(link);
            }
            else
            {
                _breadcrumb.Add(MakeLabel(part.label, "lv-crumb"));
            }
        }
    }

    void SetStatus(string text, bool isError)
    {
        if (_status == null)
            return;

        _status.text = text ?? string.Empty;
        _status.EnableInClassList("lv-status-error", isError);
    }

    static Label MakeLabel(string text, params string[] classNames)
    {
        var label = new Label(text ?? string.Empty);
        for (int i = 0; i < classNames.Length; i++)
            label.AddToClassList(classNames[i]);
        return label;
    }

    static Label MakeLinkLabel(string text, string url, params string[] classNames)
    {
        var label = MakeLabel(text, classNames);
        label.AddToClassList("lv-link");
        label.pickingMode = PickingMode.Position;
        label.RegisterCallback<ClickEvent>(_ =>
        {
            if (!string.IsNullOrWhiteSpace(url))
                Application.OpenURL(url);
        });
        return label;
    }

    static Button MakeButton(string text, string variantClass, Action onClick)
    {
        var button = new Button(() => onClick?.Invoke()) { text = text };
        button.AddToClassList("lv-btn");
        if (!string.IsNullOrEmpty(variantClass))
            button.AddToClassList(variantClass);
        button.style.flexGrow = 0;
        button.style.flexShrink = 0;
        return button;
    }
}
