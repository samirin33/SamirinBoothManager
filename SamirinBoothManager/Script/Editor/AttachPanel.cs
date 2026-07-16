using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;

namespace samirin33.SamirinBoothManager.UI.Parts
{
    /// <summary>
    /// AttachPanel.uxml を SamirinBoothAssetInfo のバリエーション情報で埋める。
    /// Attached = 選択中バリエーションがセット済み（Detach）
    /// Change   = 他バリエーションがセット済み（置き換え）
    /// Detached = 未セット（Attach）
    /// </summary>
    public class AttachPanel : SBM_UxmlPartElement
    {
        public new class UxmlFactory : UxmlFactory<AttachPanel, UxmlTraits> { }
        public new class UxmlTraits : VisualElement.UxmlTraits { }

        readonly DropdownField _variationDropdown;
        readonly Label _variationDescription;
        readonly VisualElement _attached;
        readonly VisualElement _detached;
        readonly VisualElement _change;
        readonly SBM_Button _buttonAttach;
        readonly SBM_Button _buttonDetach;
        readonly SBM_Button _buttonChange;

        readonly List<Variation> _validVariations = new List<Variation>();

        SamirinBoothAssetInfo _info;
        bool _isImported;

        public AttachPanel() : base(nameof(AttachPanel))
        {
            _variationDropdown = this.Q<DropdownField>("VariationDropDown");
            _variationDescription = this.Q<Label>("VariationDiscription");
            _attached = this.Q<VisualElement>("Attached");
            _detached = this.Q<VisualElement>("Detached");
            _change = this.Q<VisualElement>("Change");
            _buttonAttach = this.Q<SBM_Button>("ButtonAttach");
            _buttonDetach = this.Q<SBM_Button>("ButtonDetach");
            _buttonChange = this.Q<SBM_Button>("ButtonChange");

            if (_variationDropdown != null)
                _variationDropdown.RegisterValueChangedCallback(OnVariationChanged);

            if (_buttonAttach != null)
                _buttonAttach.clicked += OnAttachClicked;

            if (_buttonDetach != null)
                _buttonDetach.clicked += OnDetachClicked;

            if (_buttonChange != null)
                _buttonChange.clicked += OnChangeClicked;

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            SBM_Header.AvatarDescriptorChanged -= OnAvatarChanged;
            SBM_Header.AvatarDescriptorChanged += OnAvatarChanged;
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            SBM_Header.AvatarDescriptorChanged -= OnAvatarChanged;
        }

        void OnAvatarChanged(VRCAvatarDescriptor descriptor)
        {
            RefreshAttachedState(descriptor);
        }

        public void Bind(SamirinBoothAssetInfo info, bool isImported)
        {
            _info = info;
            _isImported = isImported;

            _validVariations.Clear();
            var choices = new List<string>();
            var variations = info?.variations;
            if (variations != null)
            {
                for (int i = 0; i < variations.Length; i++)
                {
                    var variation = variations[i];
                    if (variation == null || string.IsNullOrEmpty(variation.prefabPath))
                        continue;

                    _validVariations.Add(variation);
                    choices.Add(string.IsNullOrEmpty(variation.variationName)
                        ? $"バリエーション {_validVariations.Count}"
                        : variation.variationName);
                }
            }

            if (_variationDropdown != null)
            {
                _variationDropdown.choices = choices;
                _variationDropdown.index = choices.Count > 0 ? 0 : -1;
                _variationDropdown.SetEnabled(choices.Count > 0);
            }

            var assetName = info?.name ?? "商品";
            if (_buttonAttach != null)
                _buttonAttach.Text = $"{assetName}をアバターにセットする！";
            if (_buttonChange != null)
                _buttonChange.Text = $"{assetName}をこのバリエーションに切り替える！";
            if (_buttonDetach != null)
                _buttonDetach.Text = "セット済み！（クリックで解除）";

            ApplySelectedVariation();
            RefreshAttachedState(SBM_Header.CurrentAvatarDescriptor);
        }

        void OnVariationChanged(ChangeEvent<string> evt)
        {
            ApplySelectedVariation();
            RefreshAttachedState(SBM_Header.CurrentAvatarDescriptor);
        }

        void ApplySelectedVariation()
        {
            var variation = GetSelectedVariation();
            if (_variationDescription != null)
                _variationDescription.text = variation?.variationDescription ?? string.Empty;
        }

        Variation GetSelectedVariation()
        {
            if (_variationDropdown == null)
                return null;

            var index = _variationDropdown.index;
            if (index < 0 || index >= _validVariations.Count)
                return null;

            return _validVariations[index];
        }

        /// <summary>
        /// このアセットのバリエーションのうち、選択中以外でアバターに付いているものを返す。
        /// </summary>
        Variation FindOtherAttachedVariation(VRCAvatarDescriptor avatar, Variation selected)
        {
            if (avatar == null)
                return null;

            for (int i = 0; i < _validVariations.Count; i++)
            {
                var variation = _validVariations[i];
                if (variation == null || variation == selected)
                    continue;
                if (string.IsNullOrEmpty(variation.prefabPath))
                    continue;
                if (SBM_Header.AvatarContainsPrefab(avatar, variation.prefabPath))
                    return variation;
            }

            return null;
        }

        void RefreshAttachedState(VRCAvatarDescriptor avatar)
        {
            var canShow = _isImported && avatar != null && _validVariations.Count > 0;
            SetDisplay(this, canShow);
            if (!canShow)
                return;

            var selected = GetSelectedVariation();
            var selectedAttached = selected != null
                && !string.IsNullOrEmpty(selected.prefabPath)
                && SBM_Header.AvatarContainsPrefab(avatar, selected.prefabPath);
            var otherAttached = FindOtherAttachedVariation(avatar, selected);

            // 選択中がセット済み → Detach
            // 他バリエーションがセット済み → Change
            // 未セット → Attach
            SetDisplay(_attached, selectedAttached);
            SetDisplay(_change, !selectedAttached && otherAttached != null);
            SetDisplay(_detached, !selectedAttached && otherAttached == null);
        }

        void OnAttachClicked()
        {
            var avatar = SBM_Header.CurrentAvatarDescriptor;
            var selected = GetSelectedVariation();
            if (avatar == null || selected == null || string.IsNullOrEmpty(selected.prefabPath))
                return;

            var instance = SBM_Header.AttachPrefabToAvatar(avatar, selected.prefabPath);
            if (instance == null)
                return;

            AfterHierarchyChanged(avatar);
        }

        void OnDetachClicked()
        {
            var avatar = SBM_Header.CurrentAvatarDescriptor;
            var selected = GetSelectedVariation();
            if (avatar == null || selected == null || string.IsNullOrEmpty(selected.prefabPath))
                return;

            if (!SBM_Header.DetachPrefabFromAvatar(avatar, selected.prefabPath))
                return;

            AfterHierarchyChanged(avatar);
        }

        void OnChangeClicked()
        {
            var avatar = SBM_Header.CurrentAvatarDescriptor;
            var selected = GetSelectedVariation();
            if (avatar == null || selected == null || string.IsNullOrEmpty(selected.prefabPath))
                return;

            var other = FindOtherAttachedVariation(avatar, selected);
            var oldPath = other?.prefabPath;
            var instance = SBM_Header.ReplacePrefabOnAvatar(avatar, oldPath, selected.prefabPath);
            if (instance == null)
                return;

            AfterHierarchyChanged(avatar);
        }

        void AfterHierarchyChanged(VRCAvatarDescriptor avatar)
        {
            EditorUtility.SetDirty(avatar.gameObject);
            RefreshAttachedState(avatar);
            RefreshAssetListLabels(avatar);
            RefreshAdditionalInfoFocusButtons();
        }

        void RefreshAssetListLabels(VRCAvatarDescriptor avatar)
        {
            var root = panel?.visualTree;
            if (root == null)
                return;

            var list = root.Q<AssetList>();
            list?.RefreshAttachedLabels(avatar);
        }

        void RefreshAdditionalInfoFocusButtons()
        {
            var root = panel?.visualTree;
            if (root == null)
                return;

            root.Query<AdditionalInfo>().ForEach(info => info.RefreshFocusAvailability());
        }

        static void SetDisplay(VisualElement element, bool visible)
        {
            if (element == null)
                return;
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
