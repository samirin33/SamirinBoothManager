using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;

namespace samirin33.SamirinBoothManager.UI.Parts
{
    /// <summary>
    /// アセット一覧のカテゴリフィルタ（複数選択可）。
    /// </summary>
    [Flags]
    public enum AssetCategoryFilter
    {
        None = 0,
        [InspectorName("アバター")]
        Avatar = 1 << 0,
        [InspectorName("アバターギミック")]
        AvatarGimmick = 1 << 1,
        [InspectorName("衣装・アクセサリー")]
        AvatarAccessory = 1 << 2,
        [InspectorName("ワールド")]
        World = 1 << 3,
        [InspectorName("ワールドギミック")]
        WorldGimmick = 1 << 4,
        [InspectorName("3Dモデル")]
        _3DModel = 1 << 5,
        [InspectorName("その他")]
        Other = 1 << 6
    }

    /// <summary>
    /// SamirinBoothInformation 内の SamirinBoothAssetInfo を一覧表示する。
    /// Other は OtherContents、それ以外はインポート済み／未インポートに振り分ける。
    /// </summary>
    public class AssetList : SBM_UxmlPartElement
    {
        public new class UxmlFactory : UxmlFactory<AssetList, UxmlTraits> { }
        public new class UxmlTraits : VisualElement.UxmlTraits { }

        const string InformationFolder = "Assets/samirin33/SamirinBoothInformation";

        readonly ListHeader _listHeader;
        readonly VisualElement _importedContents;
        readonly VisualElement _notImportedContents;
        readonly VisualElement _otherContents;

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

            _listHeader = this.Q<ListHeader>();

            var scrollView = this.Q<ScrollView>();
            _importedContents = scrollView != null
                ? scrollView.Q<VisualElement>("ImportedContents")
                : null;
            _notImportedContents = scrollView != null
                ? scrollView.Q<VisualElement>("NotImportedContents")
                : null;
            _otherContents = scrollView != null
                ? scrollView.Q<VisualElement>("OtherContents")
                : null;

            if (_importedContents == null)
                Debug.LogError("[AssetList] ScrollView/#ImportedContents が見つかりません。");
            if (_notImportedContents == null)
                Debug.LogError("[AssetList] ScrollView/#NotImportedContents が見つかりません。");
            if (_otherContents == null)
                Debug.LogError("[AssetList] ScrollView/#OtherContents が見つかりません。");

            if (_listHeader != null)
            {
                _listHeader.SortChanged += RebuildElements;
                _listHeader.FilterChanged += RebuildElements;
                _listHeader.ReloadClicked += OnReloadClicked;
            }

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
            RefreshAttachedInContainer(_otherContents, descriptor);
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
            if (_importedContents == null && _notImportedContents == null && _otherContents == null)
                return;

            _importedContents?.Clear();
            _notImportedContents?.Clear();
            _otherContents?.Clear();

            var sorted = new List<SamirinBoothAssetInfo>(_infos);
            SortInfos(sorted, _listHeader != null ? _listHeader.SortMode : ListHeader.SortByName);

            var avatar = SBM_Header.CurrentAvatarDescriptor;
            for (int i = 0; i < sorted.Count; i++)
            {
                var info = sorted[i];
                if (info == null || !PassesFilter(info.category))
                    continue;

                var element = new AssetElement();
                element.AddToClassList("SBM_AssetElement");
                element.Bind(info);
                element.RefreshAttached(avatar);
                element.clicked += OnAssetElementClicked;

                ResolveContainer(info)?.Add(element);
            }
        }

        VisualElement ResolveContainer(SamirinBoothAssetInfo info)
        {
            if (IsOtherSection(info.category))
                return _otherContents;

            return IsImported(info) ? _importedContents : _notImportedContents;
        }

        bool PassesFilter(Category category)
        {
            var filterMask = _listHeader != null
                ? _listHeader.FilterMask
                : ListHeader.DefaultFilterMask;

            if (filterMask == AssetCategoryFilter.None)
                return false;

            var flag = ToFilterFlag(category);
            return (filterMask & flag) != 0;
        }

        static AssetCategoryFilter ToFilterFlag(Category category)
        {
            var index = (int)category;
            if (index < 0 || index > 6)
                return AssetCategoryFilter.Other;
            return (AssetCategoryFilter)(1 << index);
        }

        static bool IsOtherSection(Category category)
        {
            return category == Category.Other;
        }

        static bool IsImported(SamirinBoothAssetInfo info)
        {
            return SamirinBoothImportUtil.IsImported(info);
        }

        static void SortInfos(List<SamirinBoothAssetInfo> infos, string sortMode)
        {
            switch (sortMode)
            {
                case ListHeader.SortByRelease:
                    infos.Sort((a, b) => CompareDateDesc(a?.releaseDate, b?.releaseDate)
                        .ThenByName(a, b));
                    break;

                case ListHeader.SortByUpdate:
                    infos.Sort((a, b) => CompareDateDesc(a?.updateDate, b?.updateDate)
                        .ThenByName(a, b));
                    break;

                case ListHeader.SortByName:
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
