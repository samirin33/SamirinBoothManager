using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace samirin33.SamirinBoothManager.UI.Parts
{
    /// <summary>
    /// ListHeader.uxml。ソート・カテゴリフィルタ・再読み込みを提供する。
    /// </summary>
    public class ListHeader : SBM_UxmlPartElement
    {
        public new class UxmlFactory : UxmlFactory<ListHeader, UxmlTraits> { }
        public new class UxmlTraits : VisualElement.UxmlTraits { }

        public const string SortByName = "名前順";
        public const string SortByRelease = "最新順";
        public const string SortByUpdate = "アップデート順";

        // EnumFlagsField の Everything 相当（enum に All は定義しない）
        public const AssetCategoryFilter DefaultFilterMask =
            AssetCategoryFilter.Avatar
            | AssetCategoryFilter.AvatarGimmick
            | AssetCategoryFilter.AvatarAccessory
            | AssetCategoryFilter.World
            | AssetCategoryFilter.WorldGimmick
            | AssetCategoryFilter._3DModel
            | AssetCategoryFilter.Other;

        readonly DropdownField _sortDropdown;
        readonly EnumFlagsField _filterEnum;
        readonly SBM_Button _reloadButton;

        AssetCategoryFilter _filterMask = DefaultFilterMask;

        public string SortMode =>
            _sortDropdown != null && !string.IsNullOrEmpty(_sortDropdown.value)
                ? _sortDropdown.value
                : SortByName;

        public AssetCategoryFilter FilterMask => _filterMask;

        public event Action SortChanged;
        public event Action FilterChanged;
        public event Action ReloadClicked;

        public ListHeader() : base(nameof(ListHeader))
        {
            style.width = Length.Percent(100);
            style.flexShrink = 0;

            _sortDropdown = this.Q<DropdownField>("SortDropdown");
            _filterEnum = this.Q<EnumFlagsField>("FilterEnum") ?? EnsureFilterEnumField();
            _reloadButton = this.Q<SBM_Button>("ButtonReload");

            if (_sortDropdown != null)
            {
                _sortDropdown.choices = new List<string> { SortByName, SortByRelease, SortByUpdate };
                if (_sortDropdown.index < 0)
                    _sortDropdown.index = 0;

                _sortDropdown.RegisterValueChangedCallback(_ => SortChanged?.Invoke());
            }

            if (_filterEnum != null)
            {
                _filterEnum.value = DefaultFilterMask;
                _filterMask = DefaultFilterMask;
                _filterEnum.RegisterValueChangedCallback(OnFilterChanged);
            }

            if (_reloadButton != null)
                _reloadButton.clicked += () => ReloadClicked?.Invoke();
        }

        void OnFilterChanged(ChangeEvent<Enum> evt)
        {
            _filterMask = evt.newValue != null
                ? (AssetCategoryFilter)(object)evt.newValue
                : AssetCategoryFilter.None;
            FilterChanged?.Invoke();
        }

        EnumFlagsField EnsureFilterEnumField()
        {
            var existing = this.Q<VisualElement>("FilterEnum");
            if (existing == null)
                return null;

            var parent = existing.parent;
            var index = parent != null ? parent.IndexOf(existing) : -1;
            existing.RemoveFromHierarchy();

            var field = new EnumFlagsField(DefaultFilterMask)
            {
                name = "FilterEnum",
            };
            field.AddToClassList("ListHeader");
            field.style.width = 160;

            if (parent != null && index >= 0)
                parent.Insert(index, field);
            else
                this.Q<VisualElement>("Header")?.Add(field);

            return field;
        }
    }
}
