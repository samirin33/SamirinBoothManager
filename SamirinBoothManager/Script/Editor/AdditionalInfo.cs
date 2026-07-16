using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;

namespace samirin33.SamirinBoothManager.UI.Parts
{
    /// <summary>
    /// AdditionalInfo.uxml を global::AdditionalInfo の内容で埋める。
    /// ButtonObjectFocus はアバター上のセット済みプレハブ配下の paths を Hierarchy で選択する。
    /// 未セット時は半透明かつ非反応。
    /// </summary>
    public class AdditionalInfo : SBM_UxmlPartElement
    {
        public new class UxmlFactory : UxmlFactory<AdditionalInfo, UxmlTraits> { }
        public new class UxmlTraits : VisualElement.UxmlTraits { }

        const float DisabledOpacity = 0.35f;

        readonly Label _title;
        readonly Label _description;
        readonly VisualElement _image;
        readonly SBM_Button _focusButton;
        readonly VisualElement _focusButtonParent;
        readonly List<string> _paths = new List<string>();
        readonly List<SBM_Button> _extraFocusButtons = new List<SBM_Button>();

        SamirinBoothAssetInfo _assetInfo;

        public AdditionalInfo() : base(nameof(AdditionalInfo))
        {
            _title = this.Q<Label>("Title");
            _description = this.Q<Label>("Discription");
            _image = this.Q<VisualElement>("Image");
            _focusButton = this.Q<SBM_Button>("ButtonObjectFocus");
            _focusButtonParent = _focusButton?.parent;

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            SBM_Header.AvatarDescriptorChanged -= OnAvatarChanged;
            SBM_Header.AvatarDescriptorChanged += OnAvatarChanged;
            RefreshFocusButtonState();
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            SBM_Header.AvatarDescriptorChanged -= OnAvatarChanged;
        }

        void OnAvatarChanged(VRCAvatarDescriptor descriptor)
        {
            RefreshFocusButtonState();
        }

        public void Bind(global::AdditionalInfo info, SamirinBoothAssetInfo assetInfo)
        {
            _assetInfo = assetInfo;

            if (info == null)
                return;

            if (_title != null)
                _title.text = info.title ?? string.Empty;

            if (_description != null)
                _description.text = info.description ?? string.Empty;

            BindPaths(info.paths);

            if (_image != null)
            {
                if (info.image != null)
                {
                    _image.style.backgroundImage = Background.FromSprite(info.image);
                    _image.style.display = DisplayStyle.Flex;
                }
                else
                {
                    _image.style.display = DisplayStyle.None;
                }
            }

            RefreshFocusButtonState();
        }

        void BindPaths(string[] paths)
        {
            _paths.Clear();
            ClearExtraFocusButtons();

            if (paths != null)
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(paths[i]))
                        _paths.Add(paths[i]);
                }
            }

            if (_focusButton != null)
            {
                _focusButton.style.display = _paths.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                if (_paths.Count > 0)
                    ConfigureFocusButton(_focusButton, _paths[0]);
            }

            for (int i = 1; i < _paths.Count; i++)
            {
                var button = CreateFocusButton();
                ConfigureFocusButton(button, _paths[i]);
                _focusButtonParent?.Add(button);
                _extraFocusButtons.Add(button);
            }
        }

        void ClearExtraFocusButtons()
        {
            for (int i = 0; i < _extraFocusButtons.Count; i++)
                _extraFocusButtons[i]?.RemoveFromHierarchy();
            _extraFocusButtons.Clear();
        }

        SBM_Button CreateFocusButton()
        {
            var button = new SBM_Button
            {
                BackgroundColor = new Color(0x45 / 255f, 0x4c / 255f, 0x4f / 255f, 1f),
                TextScale = 2f
            };

            button.style.height = 30;
            button.style.width = Length.Percent(60);
            button.style.unityTextAlign = TextAnchor.MiddleRight;
            button.style.paddingRight = 20;
            button.style.paddingLeft = 20;
            button.style.marginTop = 4;
            return button;
        }

        void ConfigureFocusButton(SBM_Button button, string path)
        {
            if (button == null)
                return;

            button.Text = path;
            button.clicked += () => OnFocusClicked(path);
        }

        void RefreshFocusButtonState()
        {
            RefreshFocusButton(_focusButton, _paths.Count > 0 ? _paths[0] : null);
            for (int i = 0; i < _extraFocusButtons.Count; i++)
                RefreshFocusButton(_extraFocusButtons[i], i + 1 < _paths.Count ? _paths[i + 1] : null);
        }

        /// <summary>アバターへのセット状態変化後に呼ぶ。</summary>
        public void RefreshFocusAvailability()
        {
            RefreshFocusButtonState();
        }

        void RefreshFocusButton(SBM_Button button, string path)
        {
            if (button == null)
                return;

            var interactive = !string.IsNullOrWhiteSpace(path)
                && ResolveTargetOnAvatar(SBM_Header.CurrentAvatarDescriptor, path) != null;

            button.style.opacity = interactive ? 1f : DisabledOpacity;
            button.pickingMode = interactive ? PickingMode.Position : PickingMode.Ignore;
            button.SetEnabled(interactive);
        }

        void OnFocusClicked(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            var target = ResolveTargetOnAvatar(SBM_Header.CurrentAvatarDescriptor, path);
            if (target == null)
            {
                RefreshFocusButtonState();
                return;
            }

            Selection.activeGameObject = target.gameObject;
            EditorGUIUtility.PingObject(target.gameObject);
        }

        /// <summary>
        /// アバター上のセット済みプレハブ配下から、AdditionalInfo.paths の Transform を探す。
        /// </summary>
        Transform ResolveTargetOnAvatar(VRCAvatarDescriptor avatar, string path)
        {
            if (avatar == null || _assetInfo == null || string.IsNullOrWhiteSpace(path))
                return null;

            var prefabRoot = FindAttachedPrefabRoot(avatar, _assetInfo);
            if (prefabRoot == null)
                return null;

            return FindChildByRelativePath(prefabRoot.transform, path);
        }

        static GameObject FindAttachedPrefabRoot(VRCAvatarDescriptor avatar, SamirinBoothAssetInfo info)
        {
            var variations = info?.variations;
            if (avatar == null || variations == null)
                return null;

            for (int i = 0; i < variations.Length; i++)
            {
                var variation = variations[i];
                if (variation == null || string.IsNullOrEmpty(variation.prefabPath))
                    continue;

                var instance = SBM_Header.FindPrefabInstance(avatar, variation.prefabPath);
                if (instance != null)
                    return instance;
            }

            return null;
        }

        static Transform FindChildByRelativePath(Transform root, string relativePath)
        {
            if (root == null || string.IsNullOrWhiteSpace(relativePath))
                return null;

            var normalized = relativePath.Replace('\\', '/').Trim('/');
            if (string.IsNullOrEmpty(normalized))
                return null;

            var found = root.Find(normalized);
            if (found != null)
                return found;

            if (!normalized.Contains("/"))
                return FindChildRecursiveByName(root, normalized);

            return null;
        }

        static Transform FindChildRecursiveByName(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == name)
                    return child;

                var nested = FindChildRecursiveByName(child, name);
                if (nested != null)
                    return nested;
            }

            return null;
        }
    }
}
