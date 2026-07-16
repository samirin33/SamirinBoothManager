using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;

namespace samirin33.SamirinBoothManager.UI.Parts
{
    /// <summary>
    /// SamirinBoothInformation 内の SamirinBoothAssetInfo を一覧表示する。
    /// インポート済みは ImportedContents、未インポートは NotImportedContents に振り分ける。
    /// </summary>
    public class AssetList : SBM_UxmlPartElement
    {
        public new class UxmlFactory : UxmlFactory<AssetList, UxmlTraits> { }
        public new class UxmlTraits : VisualElement.UxmlTraits { }

        const string InformationFolder = "Assets/samirin33/SamirinBoothInformation";

        const string SortByName = "名前順";
        const string SortByRelease = "最新順";
        const string SortByUpdate = "アップデート順";

        readonly VisualElement _importedContents;
        readonly VisualElement _notImportedContents;
        readonly DropdownField _sortDropdown;
        readonly SBM_Button _reloadButton;

        readonly List<SamirinBoothAssetInfo> _infos = new List<SamirinBoothAssetInfo>();

        /// <summary>一覧の AssetElement がクリックされたとき。</summary>
        public event Action<SamirinBoothAssetInfo> AssetClicked;

        public AssetList() : base(nameof(AssetList))
        {
            style.flexGrow = 1;
            style.width = Length.Percent(100);
            style.height = Length.Percent(100);
            style.flexDirection = FlexDirection.Column;

            var body = this.Q<VisualElement>(className: "SBM_Text");
            if (body != null)
            {
                body.style.flexGrow = 1;
                body.style.height = Length.Percent(100);
                body.style.width = Length.Percent(100);
            }

            var scrollView = this.Q<ScrollView>();
            _importedContents = scrollView != null
                ? scrollView.Q<VisualElement>("ImportedContents")
                : null;
            _notImportedContents = scrollView != null
                ? scrollView.Q<VisualElement>("NotImportedContents")
                : null;

            _sortDropdown = this.Q<DropdownField>("SortDropdown");
            _reloadButton = this.Q<SBM_Button>("ButtonReload");

            if (_importedContents == null)
                Debug.LogError("[AssetList] ScrollView/#ImportedContents が見つかりません。");
            if (_notImportedContents == null)
                Debug.LogError("[AssetList] ScrollView/#NotImportedContents が見つかりません。");

            if (_sortDropdown != null)
            {
                _sortDropdown.choices = new List<string> { SortByName, SortByRelease, SortByUpdate };
                if (_sortDropdown.index < 0)
                    _sortDropdown.index = 0;

                _sortDropdown.RegisterValueChangedCallback(_ => RebuildElements());
            }

            if (_reloadButton != null)
                _reloadButton.clicked += OnReloadClicked;

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            SBM_Header.AvatarDescriptorChanged -= OnAvatarDescriptorChanged;
            SBM_Header.AvatarDescriptorChanged += OnAvatarDescriptorChanged;
            ReloadInfosAndRebuild();
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            SBM_Header.AvatarDescriptorChanged -= OnAvatarDescriptorChanged;
        }

        void OnAssetElementClicked(SamirinBoothAssetInfo info)
        {
            AssetClicked?.Invoke(info);
            if (info == null)
                return;

            FindAssetDetails()?.Show(info);
        }

        AssetDetails FindAssetDetails()
        {
            var root = panel?.visualTree;
            if (root == null)
                return null;

            return root.Q<AssetDetails>("AssetDetails")
                ?? root.Q<AssetDetails>();
        }

        void OnAvatarDescriptorChanged(VRCAvatarDescriptor descriptor)
        {
            RefreshAttachedLabels(descriptor);
        }

        async void OnReloadClicked()
        {
            try
            {
                await InfomationChecker.RunManually();
            }
            catch (Exception e)
            {
                Debug.LogError("[AssetList] Reload failed: " + e);
            }

            ReloadInfosAndRebuild();
        }

        public void ReloadInfosAndRebuild()
        {
            LoadInfos();
            RebuildElements();
        }

        public void RefreshAttachedLabels(VRCAvatarDescriptor descriptor)
        {
            RefreshAttachedInContainer(_importedContents, descriptor);
            RefreshAttachedInContainer(_notImportedContents, descriptor);
        }

        static void RefreshAttachedInContainer(VisualElement container, VRCAvatarDescriptor descriptor)
        {
            if (container == null)
                return;

            foreach (var child in container.Children())
            {
                if (child is AssetElement element)
                    element.RefreshAttached(descriptor);
            }
        }

        void LoadInfos()
        {
            _infos.Clear();

            var guids = AssetDatabase.FindAssets("t:SamirinBoothAssetInfo", new[] { InformationFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]).Replace('\\', '/');
                if (path.EndsWith("/Manger.asset", StringComparison.OrdinalIgnoreCase))
                    continue;

                var info = AssetDatabase.LoadAssetAtPath<SamirinBoothAssetInfo>(path);
                if (info != null)
                    _infos.Add(info);
            }
        }

        void RebuildElements()
        {
            if (_importedContents == null && _notImportedContents == null)
                return;

            _importedContents?.Clear();
            _notImportedContents?.Clear();

            var sorted = new List<SamirinBoothAssetInfo>(_infos);
            SortInfos(sorted, _sortDropdown != null ? _sortDropdown.value : SortByName);

            var avatar = SBM_Header.CurrentAvatarDescriptor;
            for (int i = 0; i < sorted.Count; i++)
            {
                var info = sorted[i];
                var element = new AssetElement();
                element.AddToClassList("SBM_AssetElement");
                element.Bind(info);
                element.RefreshAttached(avatar);
                element.clicked += OnAssetElementClicked;

                var container = IsImported(info) ? _importedContents : _notImportedContents;
                container?.Add(element);
            }
        }

        static bool IsImported(SamirinBoothAssetInfo info)
        {
            if (info == null || string.IsNullOrEmpty(info.folderName))
                return false;

            var packagePath = $"Assets/samirin33/{info.folderName}/PackageAssetInfo.json";
            var absolutePath = ToAbsolutePath(packagePath);
            if (!File.Exists(absolutePath))
                return false;

            return ReadPackageJsonVersion(absolutePath) != null;
        }

        static Version ReadPackageJsonVersion(string absolutePath)
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

        static Version ParseVersion(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var parts = text.Trim().Split('.');
            int major = parts.Length > 0 && int.TryParse(parts[0], out var ma) ? ma : 0;
            int minor = parts.Length > 1 && int.TryParse(parts[1], out var mi) ? mi : 0;
            int patch = parts.Length > 2 && int.TryParse(parts[2], out var pa) ? pa : 0;
            return new Version(major, minor, patch);
        }

        static string ToAbsolutePath(string assetPath)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        static void SortInfos(List<SamirinBoothAssetInfo> infos, string sortMode)
        {
            switch (sortMode)
            {
                case SortByRelease:
                    infos.Sort((a, b) => CompareDateDesc(a?.releaseDate, b?.releaseDate)
                        .ThenByName(a, b));
                    break;

                case SortByUpdate:
                    infos.Sort((a, b) => CompareDateDesc(a?.updateDate, b?.updateDate)
                        .ThenByName(a, b));
                    break;

                case SortByName:
                default:
                    infos.Sort((a, b) => string.Compare(a?.name, b?.name, StringComparison.OrdinalIgnoreCase));
                    break;
            }
        }

        static int CompareDateDesc(global::DateTime a, global::DateTime b)
        {
            return ToDateKey(b).CompareTo(ToDateKey(a));
        }

        static int ToDateKey(global::DateTime date)
        {
            if (date == null)
                return 0;
            return date.year * 10000 + date.month * 100 + date.day;
        }
    }

    static class AssetListSortExtensions
    {
        public static int ThenByName(this int primary, SamirinBoothAssetInfo a, SamirinBoothAssetInfo b)
        {
            if (primary != 0)
                return primary;
            return string.Compare(a?.name, b?.name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
