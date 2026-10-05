using System;
using System.Collections.Generic;
using System.Reflection;
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
        readonly VisualElement _variationCallout;
        readonly Label _variationDescription;
        readonly SBM_Button _buttonSetup;

        readonly List<Variation> _validVariations = new List<Variation>();
        readonly HashSet<string> _presentPrefabPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        SamirinBoothAssetInfo _info;
        bool _isImported;
        SetupState _state = SetupState.Attach;
        bool _useAvatarAttach;
        bool _suppressVariationCallback;
        bool _calloutDismissed;
        int _appliedDropdownIndex = -1;
        int _dropdownBindGeneration;

        /// <summary>
        /// Bind 結果としてパネルを表示すべきか（display が Flex か）。
        /// </summary>
        public bool IsContentVisible { get; private set; }

        /// <summary>
        /// プレハブがシーン／アバターへ新規配置（または差し替え）されたときに発火する。
        /// </summary>
        public event Action<GameObject> PrefabInstancePlaced;

        public AttachPanel() : base(nameof(AttachPanel))
        {
            _variationDropdown = this.Q<DropdownField>("VariationDropDown");
            _variationCallout = this.Q<VisualElement>("VariationAvailableCallout");
            _variationDescription = this.Q<Label>("VariationDiscription");
            _buttonSetup = this.Q<SBM_Button>("ButtonSetup");

            if (_variationDropdown != null)
                _variationDropdown.RegisterValueChangedCallback(OnVariationChanged);

            if (_buttonSetup != null)
                _buttonSetup.clicked += OnSetupClicked;

            SetDisplay(_variationCallout, false);
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
                    if (!HasSufficientPrefabs(variation))
                        continue;

                    _validVariations.Add(variation);
                    choices.Add(string.IsNullOrEmpty(variation.variationName)
                        ? $"バリエーション {_validVariations.Count}"
                        : variation.variationName);
                }
            }

            var avatar = SBM_Header.CurrentAvatarDescriptor;
            var useAvatar = ShouldUseAvatarAttach(_info, avatar);
            _calloutDismissed = IsCalloutDismissed(_info);
            if (_variationDropdown != null)
                ApplyDropdownIndexWithoutDismiss(choices, IndexOfInitialVariation(_validVariations, avatar, useAvatar));

            UpdateVariationCallout();
            ApplySelectedVariation();
            RefreshAttachedState(SBM_Header.CurrentAvatarDescriptor);
        }

        /// <summary>
        /// 優先度や設置済み判定による選択では、吹き出しの非表示フラグを立てない。
        /// </summary>
        void ApplyDropdownIndexWithoutDismiss(List<string> choices, int index)
        {
            if (_variationDropdown == null)
                return;

            _appliedDropdownIndex = index;
            _suppressVariationCallback = true;
            _variationDropdown.UnregisterValueChangedCallback(OnVariationChanged);
            _variationDropdown.choices = choices;
            _variationDropdown.index = index;
            _variationDropdown.SetEnabled(choices != null && choices.Count > 0);
            _variationDropdown.RegisterValueChangedCallback(OnVariationChanged);

            int generation = ++_dropdownBindGeneration;
            EditorApplication.delayCall += () =>
            {
                if (generation == _dropdownBindGeneration)
                    _suppressVariationCallback = false;
            };
        }

        void OnVariationChanged(ChangeEvent<string> evt)
        {
            int index = _variationDropdown != null ? _variationDropdown.index : -1;
            bool programmatic = _suppressVariationCallback || index == _appliedDropdownIndex;
            if (!programmatic)
            {
                _appliedDropdownIndex = index;
                _calloutDismissed = true;
                SetCalloutDismissed(_info, true);
                UpdateVariationCallout();
            }

            ApplySelectedVariation();
            RefreshAttachedState(SBM_Header.CurrentAvatarDescriptor);
        }

        void UpdateVariationCallout()
        {
            SetDisplay(_variationCallout, !_calloutDismissed && _validVariations.Count > 1);
        }

        /// <summary>
        /// バリエーション変更で隠した吹き出しを、再度表示できる状態に戻す。
        /// </summary>
        public void ResetCalloutForDebug()
        {
            _calloutDismissed = false;
            _suppressVariationCallback = false;
            SetCalloutDismissed(_info, false);
            UpdateVariationCallout();
        }

        const string CalloutPrefPrefix = "samirin33.SBM.VariationCalloutDismissed.";

        static string CalloutPrefKey(SamirinBoothAssetInfo info)
        {
            if (info == null)
                return null;

            string id = null;
            var path = AssetDatabase.GetAssetPath(info);
            if (!string.IsNullOrEmpty(path))
                id = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(id))
                id = (info.folderName ?? string.Empty) + "/" + (info.name ?? string.Empty);

            return CalloutPrefPrefix + id;
        }

        static bool IsCalloutDismissed(SamirinBoothAssetInfo info)
        {
            var key = CalloutPrefKey(info);
            return !string.IsNullOrEmpty(key) && EditorPrefs.GetBool(key, false);
        }

        static void SetCalloutDismissed(SamirinBoothAssetInfo info, bool dismissed)
        {
            var key = CalloutPrefKey(info);
            if (string.IsNullOrEmpty(key))
                return;

            if (dismissed)
                EditorPrefs.SetBool(key, true);
            else
                EditorPrefs.DeleteKey(key);
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
            int activeIndex = IndexOfPlacedVariation(_validVariations, avatar, _useAvatarAttach);
            int selectedIndex = _variationDropdown != null ? _variationDropdown.index : -1;
            var active = activeIndex >= 0 && activeIndex < _validVariations.Count
                ? _validVariations[activeIndex]
                : null;

            // 必要なプレファブが揃っているバリエーションが選択中 → Detach
            // 同じ ID の別バリエーションが設置済み → Change（追加プレファブの付け外しを含む）
            // 未設置 → Attach
            if (activeIndex >= 0 && activeIndex == selectedIndex)
                _state = SetupState.Detach;
            else if (active != null && selected != null && active.id == selected.id)
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
            var instance = PlaceVariation(avatar, selected, useAvatar);
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
            if (useAvatar && avatar == null)
                return;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Detach Booth Variation");

            bool removed;
            if (useAvatar)
                removed = SBM_Header.DetachPrefabFromAvatar(avatar, selected.prefabPath);
            else
                removed = SBM_Header.DetachPrefabFromScene(selected.prefabPath);

            bool removedExtras = DetachSimultaneousPrefabs(avatar, selected, useAvatar, preservePath: null);
            if (!removed && !removedExtras)
            {
                Undo.CollapseUndoOperations(undoGroup);
                return;
            }
            Undo.CollapseUndoOperations(undoGroup);
            AfterHierarchyChanged(avatar, null);
        }

        void OnChangeClicked()
        {
            var avatar = SBM_Header.CurrentAvatarDescriptor;
            var selected = GetSelectedVariation();
            if (avatar == null || selected == null || string.IsNullOrEmpty(selected.prefabPath))
                return;

            var instance = PlaceVariation(avatar, selected, useAvatar: true);
            if (instance == null)
                return;

            AfterHierarchyChanged(avatar, instance);
        }

        /// <summary>
        /// 選択バリエーションの本体を配置し、同時配置プレハブも置く。
        /// 同じ ID が既にある場合は本体を差し替え、切り替える前の同時配置プレハブを削除する。
        /// </summary>
        GameObject PlaceVariation(VRCAvatarDescriptor avatar, Variation selected, bool useAvatar)
        {
            if (selected == null || string.IsNullOrEmpty(selected.prefabPath))
                return null;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Place Booth Variation");

            Variation previous = FindPlacedSameIdVariation(selected, avatar, useAvatar);

            GameObject instance;
            if (useAvatar)
            {
                var sameMain = previous != null && PathsEqual(previous.prefabPath, selected.prefabPath);
                if (sameMain || (previous == null && SBM_Header.AvatarContainsPrefab(avatar, selected.prefabPath)))
                {
                    // 本体が同じなら残し、追加プレファブだけ付け替える
                    instance = SBM_Header.FindPrefabInstance(avatar, selected.prefabPath);
                }
                else if (previous != null)
                {
                    instance = SBM_Header.ReplacePrefabOnAvatar(avatar, previous.prefabPath, selected.prefabPath);
                }
                else
                {
                    instance = SBM_Header.AttachPrefabToAvatar(avatar, selected.prefabPath);
                }
            }
            else
            {
                instance = SBM_Header.InstantiatePrefabInScene(selected.prefabPath);
            }

            if (instance == null)
            {
                Undo.CollapseUndoOperations(undoGroup);
                return null;
            }

            // 選択中が使わない、別バリエーションの同時配置プレファブを削除してから足す
            DetachForeignSimultaneousPrefabs(avatar, selected, useAvatar);
            AttachSimultaneousPrefabs(avatar, selected, useAvatar);
            Undo.CollapseUndoOperations(undoGroup);
            return instance;
        }

        static void AttachSimultaneousPrefabs(
            VRCAvatarDescriptor avatar,
            Variation variation,
            bool useAvatar)
        {
            var paths = variation?.simultaneousPrefabPaths;
            if (paths == null)
                return;

            for (int i = 0; i < paths.Length; i++)
            {
                var path = paths[i];
                if (string.IsNullOrEmpty(path) || PathsEqual(path, variation.prefabPath))
                    continue;

                if (useAvatar)
                    SBM_Header.AttachPrefabToAvatar(avatar, path);
                else
                    SBM_Header.InstantiatePrefabInScene(path);
            }
        }

        /// <summary>
        /// 同じ ID の別バリエーションが使っている同時配置プレファブのうち、
        /// 選択中のバリエーションでは使わないものを削除する。
        /// </summary>
        void DetachForeignSimultaneousPrefabs(
            VRCAvatarDescriptor avatar,
            Variation selected,
            bool useAvatar)
        {
            if (selected == null)
                return;

            for (int i = 0; i < _validVariations.Count; i++)
            {
                var variation = _validVariations[i];
                if (variation == null || variation == selected || variation.id != selected.id)
                    continue;

                var paths = variation.simultaneousPrefabPaths;
                if (paths == null)
                    continue;

                for (int p = 0; p < paths.Length; p++)
                {
                    var path = paths[p];
                    if (string.IsNullOrEmpty(path) || VariationUsesPrefab(selected, path))
                        continue;

                    if (useAvatar)
                    {
                        if (avatar != null)
                            SBM_Header.DetachPrefabFromAvatar(avatar, path);
                    }
                    else
                    {
                        SBM_Header.DetachPrefabFromScene(path);
                    }
                }
            }
        }

        Variation FindPlacedSameIdVariation(Variation selected, VRCAvatarDescriptor avatar, bool useAvatar)
        {
            if (selected == null)
                return null;

            int index = IndexOfPlacedVariation(_validVariations, avatar, useAvatar);
            if (index < 0 || index >= _validVariations.Count)
                return null;

            var placed = _validVariations[index];
            if (placed == null || placed == selected || placed.id != selected.id)
                return null;

            return placed;
        }

        static bool VariationUsesPrefab(Variation variation, string path)
        {
            if (variation == null || string.IsNullOrEmpty(path))
                return false;
            if (PathsEqual(variation.prefabPath, path))
                return true;

            var extras = variation.simultaneousPrefabPaths;
            if (extras == null)
                return false;

            for (int i = 0; i < extras.Length; i++)
            {
                if (PathsEqual(extras[i], path))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 同時配置プレハブを削除する。preservePath と同じパスは、差し替え後の本体を消さないために残す。
        /// </summary>
        static bool DetachSimultaneousPrefabs(
            VRCAvatarDescriptor avatar,
            Variation variation,
            bool useAvatar,
            string preservePath)
        {
            var paths = variation?.simultaneousPrefabPaths;
            if (paths == null)
                return false;

            bool removed = false;
            for (int i = 0; i < paths.Length; i++)
            {
                var path = paths[i];
                if (string.IsNullOrEmpty(path) || PathsEqual(path, preservePath))
                    continue;

                if (useAvatar)
                {
                    if (avatar != null && SBM_Header.DetachPrefabFromAvatar(avatar, path))
                        removed = true;
                }
                else if (SBM_Header.DetachPrefabFromScene(path))
                {
                    removed = true;
                }
            }

            return removed;
        }

        /// <summary>
        /// 本体と同時配置プレファブがすべてプロジェクト上に存在するときだけ選択候補にする。
        /// </summary>
        static bool HasSufficientPrefabs(Variation variation)
        {
            if (variation == null || !PrefabAssetExists(variation.prefabPath))
                return false;

            var extras = variation.simultaneousPrefabPaths;
            if (extras == null || extras.Length == 0)
                return true;

            for (int i = 0; i < extras.Length; i++)
            {
                if (!PrefabAssetExists(extras[i]))
                    return false;
            }

            return true;
        }

        static bool PrefabAssetExists(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            return AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        }

        /// <summary>
        /// 設置済みの組み合わせに一致するバリエーションを選ぶ。無いときだけ priority を使う。
        /// </summary>
        int IndexOfInitialVariation(List<Variation> variations, VRCAvatarDescriptor avatar, bool useAvatar)
        {
            int placed = IndexOfPlacedVariation(variations, avatar, useAvatar);
            if (placed >= 0)
                return placed;

            return IndexOfHighestPriority(variations);
        }

        /// <summary>
        /// 今置いてあるプレファブ実体から、対応するバリエーションを決める。
        /// 本体と同時配置がすべてあり、同じ ID の他バリエーション専用プレファブが無いものを採用する。
        /// 複数あるときは、必要なプレファブが多い方を選ぶ。priority は使わない。
        /// </summary>
        int IndexOfPlacedVariation(List<Variation> variations, VRCAvatarDescriptor avatar, bool useAvatar)
        {
            if (variations == null || variations.Count == 0)
                return -1;

            SBM_Header.CollectPrefabInstanceRootPaths(_presentPrefabPaths, avatar, useAvatar);
            if (_presentPrefabPaths.Count == 0)
                return -1;

            int exactIndex = -1;
            int exactSize = -1;
            for (int i = 0; i < variations.Count; i++)
            {
                if (!IsExactPlacedVariation(variations, i))
                    continue;

                int size = CountRequiredPrefabs(variations[i]);
                if (size <= exactSize)
                    continue;

                exactIndex = i;
                exactSize = size;
            }

            if (exactIndex >= 0)
                return exactIndex;

            int bestIndex = -1;
            int bestPresent = 0;
            int bestMissing = int.MaxValue;
            int bestForeign = int.MaxValue;
            for (int i = 0; i < variations.Count; i++)
            {
                MeasurePlacedVariation(variations, i, out int present, out int missing, out int foreign);
                if (present <= 0)
                    continue;

                bool better = bestIndex < 0
                    || present > bestPresent
                    || (present == bestPresent && missing < bestMissing)
                    || (present == bestPresent && missing == bestMissing && foreign < bestForeign);
                if (!better)
                    continue;

                bestIndex = i;
                bestPresent = present;
                bestMissing = missing;
                bestForeign = foreign;
            }

            return bestIndex;
        }

        bool IsExactPlacedVariation(List<Variation> variations, int index)
        {
            var variation = variations[index];
            if (!AllRequiredPrefabsPresent(variation))
                return false;

            for (int i = 0; i < variations.Count; i++)
            {
                var other = variations[i];
                if (other == null || other == variation || other.id != variation.id)
                    continue;
                if (HasPresentPrefabUnusedBy(variation, other))
                    return false;
            }

            return true;
        }

        bool AllRequiredPrefabsPresent(Variation variation)
        {
            if (variation == null || string.IsNullOrEmpty(variation.prefabPath))
                return false;
            if (!IsPresentPrefab(variation.prefabPath))
                return false;

            var extras = variation.simultaneousPrefabPaths;
            if (extras == null)
                return true;

            for (int i = 0; i < extras.Length; i++)
            {
                if (string.IsNullOrEmpty(extras[i]) || PathsEqual(extras[i], variation.prefabPath))
                    continue;
                if (!IsPresentPrefab(extras[i]))
                    return false;
            }

            return true;
        }

        bool HasPresentPrefabUnusedBy(Variation variation, Variation other)
        {
            if (other == null)
                return false;

            if (!string.IsNullOrEmpty(other.prefabPath)
                && !VariationUsesPrefab(variation, other.prefabPath)
                && IsPresentPrefab(other.prefabPath))
                return true;

            var extras = other.simultaneousPrefabPaths;
            if (extras == null)
                return false;

            for (int i = 0; i < extras.Length; i++)
            {
                if (string.IsNullOrEmpty(extras[i]) || VariationUsesPrefab(variation, extras[i]))
                    continue;
                if (IsPresentPrefab(extras[i]))
                    return true;
            }

            return false;
        }

        void MeasurePlacedVariation(
            List<Variation> variations,
            int index,
            out int present,
            out int missing,
            out int foreign)
        {
            present = 0;
            missing = 0;
            foreign = 0;

            var variation = variations[index];
            if (variation == null)
            {
                missing = 1;
                return;
            }

            CountRequiredPresence(variation.prefabPath, ref present, ref missing);
            var extras = variation.simultaneousPrefabPaths;
            if (extras != null)
            {
                for (int i = 0; i < extras.Length; i++)
                {
                    if (PathsEqual(extras[i], variation.prefabPath))
                        continue;
                    CountRequiredPresence(extras[i], ref present, ref missing);
                }
            }

            for (int i = 0; i < variations.Count; i++)
            {
                var other = variations[i];
                if (other == null || other == variation || other.id != variation.id)
                    continue;
                if (HasPresentPrefabUnusedBy(variation, other))
                    foreign++;
            }
        }

        void CountRequiredPresence(string path, ref int present, ref int missing)
        {
            if (string.IsNullOrEmpty(path))
            {
                missing++;
                return;
            }

            if (IsPresentPrefab(path))
                present++;
            else
                missing++;
        }

        static int CountRequiredPrefabs(Variation variation)
        {
            if (variation == null || string.IsNullOrEmpty(variation.prefabPath))
                return 0;

            int count = 1;
            var extras = variation.simultaneousPrefabPaths;
            if (extras == null)
                return count;

            for (int i = 0; i < extras.Length; i++)
            {
                if (string.IsNullOrEmpty(extras[i]) || PathsEqual(extras[i], variation.prefabPath))
                    continue;
                count++;
            }

            return count;
        }

        bool IsPresentPrefab(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            return _presentPrefabPaths.Contains(path.Replace('\\', '/'));
        }

        /// <summary>
        /// 初回表示で選ぶインデックス。priority が大きいものを優先し、同値なら一覧の先を選ぶ。
        /// </summary>
        static int IndexOfHighestPriority(List<Variation> variations)
        {
            if (variations == null || variations.Count == 0)
                return -1;

            int bestIndex = 0;
            int bestPriority = variations[0] != null ? variations[0].priority : int.MinValue;
            for (int i = 1; i < variations.Count; i++)
            {
                var variation = variations[i];
                if (variation == null)
                    continue;
                if (variation.priority > bestPriority)
                {
                    bestIndex = i;
                    bestPriority = variation.priority;
                }
            }

            return bestIndex;
        }

        static bool PathsEqual(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return false;

            return string.Equals(
                a.Replace('\\', '/'),
                b.Replace('\\', '/'),
                StringComparison.OrdinalIgnoreCase);
        }

        void AfterHierarchyChanged(VRCAvatarDescriptor avatar, GameObject instance)
        {
            if (avatar != null)
                EditorUtility.SetDirty(avatar.gameObject);

            if (instance != null)
            {
                Selection.activeGameObject = instance;
                PrefabInstancePlaced?.Invoke(instance);
                TryCheckPlacedHierarchy(instance);
            }

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

        /// <summary>
        /// PackageVersionCheckerService が無い環境でもコンパイル・実行できるよう、リフレクションで呼ぶ。
        /// </summary>
        static void TryCheckPlacedHierarchy(GameObject root)
        {
            if (root == null)
                return;

            const string typeName = "Samirin33.NDMF.Components.Editor.PackageVersionCheckerService";
            Type type = null;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(typeName);
                if (type != null)
                    break;
            }

            if (type == null)
                return;

            var method = type.GetMethod(
                "CheckPlacedHierarchy",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(GameObject) },
                null);
            if (method == null)
                return;

            method.Invoke(null, new object[] { root });
        }
    }
}
