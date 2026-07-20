using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;

namespace samirin33.SamirinBoothManager.UI.Parts
{
    /// <summary>
    /// AttachPanel.uxml を SamirinBoothAssetInfo のバリエーション情報で埋める。
    /// ButtonSetup の色と文字で状態を表す。
    /// ・AvatarGimmick / AvatarAccessory + AvatarDescriptor あり → アバターへセット（Detach/Change あり）
    /// ・それ以外（AvatarDescriptor None / 他カテゴリ）→ シーンへ直接配置（複数可・解除なし）
    /// ・Other はパネル非表示
    /// </summary>
    public class AttachPanel : SBM_UxmlPartElement
    {
        public new class UxmlFactory : UxmlFactory<AttachPanel, UxmlTraits> { }
        public new class UxmlTraits : VisualElement.UxmlTraits { }

        enum SetupState { Attach, Detach, Change }

        static readonly Color AttachColor = Hex("#439796FF");
        static readonly Color DetachColor = Hex("#CC5A66FF");
        static readonly Color ChangeColor = Hex("#439753FF");

        readonly DropdownField _variationDropdown;
        readonly Label _variationDescription;
        readonly SBM_Button _buttonSetup;

        readonly List<Variation> _validVariations = new List<Variation>();

        SamirinBoothAssetInfo _info;
        bool _isImported;
        SetupState _state = SetupState.Attach;
        bool _useAvatarAttach;

        /// <summary>
        /// Bind 結果としてパネルを表示すべきか（display が Flex か）。
        /// </summary>
        public bool IsContentVisible { get; private set; }

        public AttachPanel() : base(nameof(AttachPanel))
        {
            _variationDropdown = this.Q<DropdownField>("VariationDropDown");
            _variationDescription = this.Q<Label>("VariationDiscription");
            _buttonSetup = this.Q<SBM_Button>("ButtonSetup");

            if (_variationDropdown != null)
                _variationDropdown.RegisterValueChangedCallback(OnVariationChanged);

            if (_buttonSetup != null)
                _buttonSetup.clicked += OnSetupClicked;

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
            // バージョン不明（フォルダのみ等）のときは AttachPanel を出さない
            _isImported = isImported
                && SamirinBoothImportUtil.TryGetInstalledVersion(info, out var installed)
                && installed != null;

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

        static bool IsAttachPanelCategory(Category category)
        {
            return category != Category.Other;
        }

        static bool IsAvatarBoundCategory(Category category)
        {
            return category == Category.AvatarGimmick
                || category == Category.AvatarAccessory;
        }

        /// <summary>
        /// アバターへセットするモードか。SDK/Descriptor が無い場合はシーン配置へフォールバック。
        /// </summary>
        static bool ShouldUseAvatarAttach(SamirinBoothAssetInfo info, VRCAvatarDescriptor avatar)
        {
            if (info == null || avatar == null)
                return false;
            return IsAvatarBoundCategory(info.category);
        }

        /// <summary>
        /// 選択中と同じ ID で、選択中以外のバリエーションがアバターに付いているものを返す。
        /// ID が違うバリエーションは共存対象なので無視する。
        /// </summary>
        Variation FindSameIdAttachedVariation(VRCAvatarDescriptor avatar, Variation selected)
        {
            if (avatar == null || selected == null)
                return null;

            for (int i = 0; i < _validVariations.Count; i++)
            {
                var variation = _validVariations[i];
                if (variation == null || variation == selected)
                    continue;
                if (variation.id != selected.id)
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
            var category = _info != null ? _info.category : Category.Other;
            var canShow = _isImported
                && IsAttachPanelCategory(category)
                && _validVariations.Count > 0;
            IsContentVisible = canShow;
            SetDisplay(this, canShow);
            if (!canShow)
                return;

            _useAvatarAttach = ShouldUseAvatarAttach(_info, avatar);

            if (!_useAvatarAttach)
            {
                // シーン配置: 複数可のため常に Attach（解除モードなし）
                _state = SetupState.Attach;
                ApplyButtonState();
                return;
            }

            var selected = GetSelectedVariation();
            var selectedAttached = selected != null
                && !string.IsNullOrEmpty(selected.prefabPath)
                && SBM_Header.AvatarContainsPrefab(avatar, selected.prefabPath);
            var sameIdAttached = FindSameIdAttachedVariation(avatar, selected);

            // 選択中がセット済み → Detach
            // 同じ ID の他バリエーションがセット済み → Change
            // 未セット（異なる ID の共存は可）→ Attach
            if (selectedAttached)
                _state = SetupState.Detach;
            else if (sameIdAttached != null)
                _state = SetupState.Change;
            else
                _state = SetupState.Attach;

            ApplyButtonState();
        }

        void ApplyButtonState()
        {
            if (_buttonSetup == null)
                return;

            var assetName = _info?.name ?? "商品";
            switch (_state)
            {
                case SetupState.Detach:
                    _buttonSetup.BackgroundColor = DetachColor;
                    _buttonSetup.Text = "セット済み！（クリックで解除）";
                    break;

                case SetupState.Change:
                    _buttonSetup.BackgroundColor = ChangeColor;
                    _buttonSetup.Text = $"{assetName}をこのバリエーションに切り替える！";
                    break;

                case SetupState.Attach:
                default:
                    _buttonSetup.BackgroundColor = AttachColor;
                    _buttonSetup.Text = _useAvatarAttach
                        ? $"{assetName}をアバターにセットする！"
                        : $"{assetName}をシーンに配置する！";
                    break;
            }
        }

        void OnSetupClicked()
        {
            switch (_state)
            {
                case SetupState.Detach:
                    OnDetachClicked();
                    break;
                case SetupState.Change:
                    OnChangeClicked();
                    break;
                case SetupState.Attach:
                default:
                    OnAttachClicked();
                    break;
            }
        }

        void OnAttachClicked()
        {
            var selected = GetSelectedVariation();
            if (selected == null || string.IsNullOrEmpty(selected.prefabPath))
                return;

            var avatar = SBM_Header.CurrentAvatarDescriptor;
            var useAvatar = ShouldUseAvatarAttach(_info, avatar);

            // rootAsset が未配置なら、その1つ目のバリエーションで自動配置
            EnsureRootAssetPlaced(avatar, useAvatar);

            GameObject instance;
            if (useAvatar)
            {
                // 同じ ID が既にあれば置き換え、なければ追加（異なる ID は共存）
                var sameId = FindSameIdAttachedVariation(avatar, selected);
                instance = sameId != null
                    ? SBM_Header.ReplacePrefabOnAvatar(avatar, sameId.prefabPath, selected.prefabPath)
                    : SBM_Header.AttachPrefabToAvatar(avatar, selected.prefabPath);
            }
            else
            {
                // AvatarDescriptor None / 非アバター向けカテゴリ / SDK 未使用時はシーンへ
                instance = SBM_Header.InstantiatePrefabInScene(selected.prefabPath);
            }

            if (instance == null)
                return;

            AfterHierarchyChanged(avatar, instance);
        }

        void OnDetachClicked()
        {
            var avatar = SBM_Header.CurrentAvatarDescriptor;
            var selected = GetSelectedVariation();
            if (selected == null || string.IsNullOrEmpty(selected.prefabPath))
                return;

            var useAvatar = ShouldUseAvatarAttach(_info, avatar);
            if (useAvatar)
            {
                if (avatar == null)
                    return;
                if (!SBM_Header.DetachPrefabFromAvatar(avatar, selected.prefabPath))
                    return;
            }
            else
            {
                if (!SBM_Header.DetachPrefabFromScene(selected.prefabPath))
                    return;
            }

            // このアセットを rootAsset としている依存アセットも解除（自身の rootAsset は残す）
            DetachAssetsThatReferenceAsRoot(_info, avatar, useAvatar);

            AfterHierarchyChanged(avatar, null);
        }

        void OnChangeClicked()
        {
            var avatar = SBM_Header.CurrentAvatarDescriptor;
            var selected = GetSelectedVariation();
            if (avatar == null || selected == null || string.IsNullOrEmpty(selected.prefabPath))
                return;

            EnsureRootAssetPlaced(avatar, useAvatarAttach: true);

            var sameId = FindSameIdAttachedVariation(avatar, selected);
            var oldPath = sameId?.prefabPath;
            var instance = SBM_Header.ReplacePrefabOnAvatar(avatar, oldPath, selected.prefabPath);
            if (instance == null)
                return;

            AfterHierarchyChanged(avatar, instance);
        }

        /// <summary>
        /// rootAsset が未配置なら、1つ目の有効バリエーションで配置する。既にあれば何もしない。
        /// </summary>
        void EnsureRootAssetPlaced(VRCAvatarDescriptor avatar, bool useAvatarAttach)
        {
            var root = _info?.rootAsset;
            if (root == null || root == _info)
                return;

            if (IsAssetAnyVariationPlaced(root, avatar, useAvatarAttach))
                return;

            var first = GetFirstValidVariation(root);
            if (first == null || string.IsNullOrEmpty(first.prefabPath))
                return;

            if (useAvatarAttach)
                SBM_Header.AttachPrefabToAvatar(avatar, first.prefabPath);
            else
                SBM_Header.InstantiatePrefabInScene(first.prefabPath);
        }

        /// <summary>
        /// このアセットを rootAsset として参照しているアセットの、配置済みバリエーションをすべて解除する。
        /// </summary>
        static void DetachAssetsThatReferenceAsRoot(
            SamirinBoothAssetInfo rootInfo,
            VRCAvatarDescriptor avatar,
            bool useAvatarAttach)
        {
            if (rootInfo == null)
                return;

            var dependents = FindAssetsThatReferenceRoot(rootInfo);
            for (int i = 0; i < dependents.Count; i++)
            {
                var dependent = dependents[i];
                if (dependent == null || dependent == rootInfo)
                    continue;

                DetachAllPlacedVariations(dependent, avatar, useAvatarAttach);
            }
        }

        static List<SamirinBoothAssetInfo> FindAssetsThatReferenceRoot(SamirinBoothAssetInfo rootInfo)
        {
            var results = new List<SamirinBoothAssetInfo>();
            if (rootInfo == null)
                return results;

            var guids = AssetDatabase.FindAssets(
                "t:SamirinBoothAssetInfo",
                new[] { "Assets/samirin33/SamirinBoothInformation" });
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var info = AssetDatabase.LoadAssetAtPath<SamirinBoothAssetInfo>(path);
                if (info == null || info.rootAsset != rootInfo)
                    continue;
                results.Add(info);
            }

            return results;
        }

        static void DetachAllPlacedVariations(
            SamirinBoothAssetInfo info,
            VRCAvatarDescriptor avatar,
            bool useAvatarAttach)
        {
            var variations = info?.variations;
            if (variations == null)
                return;

            for (int i = 0; i < variations.Length; i++)
            {
                var variation = variations[i];
                if (variation == null || string.IsNullOrEmpty(variation.prefabPath))
                    continue;

                if (useAvatarAttach)
                {
                    if (avatar != null && SBM_Header.AvatarContainsPrefab(avatar, variation.prefabPath))
                        SBM_Header.DetachPrefabFromAvatar(avatar, variation.prefabPath);
                }
                else if (SBM_Header.SceneContainsPrefab(variation.prefabPath))
                {
                    SBM_Header.DetachPrefabFromScene(variation.prefabPath);
                }
            }
        }

        static bool IsAssetAnyVariationPlaced(
            SamirinBoothAssetInfo info,
            VRCAvatarDescriptor avatar,
            bool useAvatarAttach)
        {
            var variations = info?.variations;
            if (variations == null)
                return false;

            for (int i = 0; i < variations.Length; i++)
            {
                var variation = variations[i];
                if (variation == null || string.IsNullOrEmpty(variation.prefabPath))
                    continue;

                if (useAvatarAttach)
                {
                    if (avatar != null && SBM_Header.AvatarContainsPrefab(avatar, variation.prefabPath))
                        return true;
                }
                else if (SBM_Header.SceneContainsPrefab(variation.prefabPath))
                {
                    return true;
                }
            }

            return false;
        }

        static Variation GetFirstValidVariation(SamirinBoothAssetInfo info)
        {
            var variations = info?.variations;
            if (variations == null)
                return null;

            for (int i = 0; i < variations.Length; i++)
            {
                var variation = variations[i];
                if (variation != null && !string.IsNullOrEmpty(variation.prefabPath))
                    return variation;
            }

            return null;
        }

        void AfterHierarchyChanged(VRCAvatarDescriptor avatar, GameObject instance)
        {
            if (avatar != null)
                EditorUtility.SetDirty(avatar.gameObject);

            if (instance != null)
                Selection.activeGameObject = instance;

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

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            return color;
        }
    }
}
