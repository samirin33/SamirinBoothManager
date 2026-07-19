using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace samirin33.SamirinBoothManager.UI.Parts
{
    /// <summary>
    /// UpdateAssetList.uxml。更新があるアセットを一覧表示する。
    /// </summary>
    public class UpdateAssetList : SBM_UxmlPartElement
    {
        public new class UxmlFactory : UxmlFactory<UpdateAssetList, UxmlTraits> { }
        public new class UxmlTraits : VisualElement.UxmlTraits { }

        const string BoothUrl = "https://samirin33-vrc.booth.pm/";
        const string XUrl = "https://x.com/samirin33_VRC";
        const string MainXUrl = "https://x.com/samirin33";

        readonly ListHeader _listHeader;
        readonly VisualElement _updateContents;
        readonly VisualElement _updateFound;
        readonly VisualElement _updateNotFound;
        readonly Toggle _ignoreToggle;
        readonly Label _updateCheckConfig;
        readonly VisualElement _logoArea;
        readonly SBM_Button _boothButton;
        readonly SBM_Button _xButton;

        readonly List<SamirinBoothAssetInfo> _sourceInfos = new List<SamirinBoothAssetInfo>();
        readonly List<SamirinBoothAssetInfo> _displayedInfos = new List<SamirinBoothAssetInfo>();

        /// <summary>無視対象。開いた時点の更新アセット全体。</summary>
        public IReadOnlyList<SamirinBoothAssetInfo> BoundInfos => _sourceInfos;

        public bool ShouldIgnoreCurrentVersions =>
            _ignoreToggle != null && _ignoreToggle.value;

        public UpdateAssetList() : base(nameof(UpdateAssetList))
        {
            style.flexGrow = 1;
            style.width = Length.Percent(100);
            style.height = Length.Percent(100);

            var body = this.Q<VisualElement>(className: "SBM_Text");
            if (body != null)
            {
                body.style.flexGrow = 1;
                body.style.height = Length.Percent(100);
                body.style.width = Length.Percent(100);
            }

            _listHeader = this.Q<ListHeader>();
            _updateContents = this.Q<VisualElement>("UpdateContents");
            _updateFound = this.Q<VisualElement>("UpdateFound");
            _updateNotFound = this.Q<VisualElement>("UpdateNotFound");
            _ignoreToggle = this.Q<Toggle>("IgnoreToggle") ?? this.Q<Toggle>();
            _updateCheckConfig = this.Q<Label>("UpdateCheckConfig");
            _logoArea = this.Q<VisualElement>("LogoArea");
            _boothButton = this.Q<SBM_Button>("Button_Booth");
            _xButton = this.Q<SBM_Button>("Button_X");

            if (_listHeader != null)
            {
                _listHeader.SortChanged += RebuildElements;
                _listHeader.FilterChanged += RebuildElements;
                _listHeader.ReloadClicked += OnReloadClicked;
            }

            if (_updateCheckConfig != null)
            {
                _updateCheckConfig.pickingMode = PickingMode.Position;
                _updateCheckConfig.style.unityTextAlign = TextAnchor.MiddleLeft;
                _updateCheckConfig.RegisterCallback<ClickEvent>(OnUpdateCheckConfigClicked);
            }

            if (_logoArea != null)
            {
                _logoArea.pickingMode = PickingMode.Position;
                _logoArea.RegisterCallback<ClickEvent>(evt =>
                {
                    Application.OpenURL(MainXUrl);
                    evt.StopPropagation();
                });
            }

            if (_boothButton != null)
                _boothButton.clicked += () => Application.OpenURL(BoothUrl);

            if (_xButton != null)
                _xButton.clicked += () => Application.OpenURL(XUrl);

            ApplyEmptyState(hasItems: false);
        }

        void OnUpdateCheckConfigClicked(ClickEvent evt)
        {
            SettingsService.OpenUserPreferences(InformationCheckerPreferences.PreferencesPath);
            evt.StopPropagation();
        }

        public void Bind(IReadOnlyList<SamirinBoothAssetInfo> infos)
        {
            _sourceInfos.Clear();
            if (infos != null)
            {
                for (int i = 0; i < infos.Count; i++)
                {
                    if (infos[i] != null)
                        _sourceInfos.Add(infos[i]);
                }
            }

            RebuildElements();
        }

        void OnReloadClicked()
        {
            _ = ReloadAsync();
        }

        async System.Threading.Tasks.Task ReloadAsync()
        {
            if (InformationChecker.IsRunning)
                return;

            try
            {
                await InformationChecker.RunUpdateAsync(showDialogs: false, showRemindWindow: false);
            }
            catch (Exception e)
            {
                Debug.LogError("[UpdateAssetList] Reload failed: " + e);
            }

            Bind(SamirinBoothUpdateUtil.CollectOutdatedAssets()
                ?? new List<SamirinBoothAssetInfo>());
        }

        void RebuildElements()
        {
            _displayedInfos.Clear();
            _updateContents?.Clear();

            var filtered = new List<SamirinBoothAssetInfo>();
            for (int i = 0; i < _sourceInfos.Count; i++)
            {
                var info = _sourceInfos[i];
                if (info == null || !PassesFilter(info.category))
                    continue;
                filtered.Add(info);
            }

            SortInfos(filtered, _listHeader != null ? _listHeader.SortMode : ListHeader.SortByName);

            if (_updateContents != null)
            {
                for (int i = 0; i < filtered.Count; i++)
                {
                    var info = filtered[i];
                    _displayedInfos.Add(info);

                    var element = new AssetElement();
                    element.AddToClassList("SBM_AssetElement");
                    element.Bind(info);
                    element.clicked += OnAssetClicked;
                    _updateContents.Add(element);
                }
            }

            ApplyEmptyState(filtered.Count > 0);
        }

        void ApplyEmptyState(bool hasItems)
        {
            SetDisplay(_updateFound, hasItems);
            SetDisplay(_updateNotFound, !hasItems);
            SetDisplay(_updateContents, hasItems);
        }

        static void SetDisplay(VisualElement element, bool visible)
        {
            if (element == null)
                return;
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
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

        void OnAssetClicked(SamirinBoothAssetInfo info)
        {
            if (info == null)
                return;

            if (!string.IsNullOrWhiteSpace(info.url))
            {
                Application.OpenURL(info.url);
                return;
            }

            SBM_UIMain.ShowWindowAndFocus(info);
        }
    }
}
