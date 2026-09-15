using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;

namespace samirin33.SamirinBoothManager.UI.Parts
{
    /// <summary>
    /// AdditionalInfo.uxml を global::AdditionalInfo の内容で埋める。
    /// ButtonObjectFocus は
    /// - アセットパス: Project 上のアセットを選択
    /// - ヒエラルキーパス: アバター上のセット済みプレハブ配下を Hierarchy で選択
    /// 未セット時（ヒエラルキー）は半透明かつ非反応。
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
        readonly List<AdditionalPathInfo> _paths = new List<AdditionalPathInfo>();
        readonly List<SBM_Button> _extraFocusButtons = new List<SBM_Button>();
        readonly Dictionary<SBM_Button, Action> _focusClickHandlers = new Dictionary<SBM_Button, Action>();

        SamirinBoothAssetInfo _assetInfo;
        Background _imageBackground;
        bool _hasImage;

        /// <summary>Image がクリックされたとき（表示中の画像背景を渡す）。</summary>
        public event Action<Background> ImageClicked;

        public AdditionalInfo() : base(nameof(AdditionalInfo))
        {
            _title = this.Q<Label>("Title");
            _description = this.Q<Label>("Discription");
            _image = this.Q<VisualElement>("Image");
            _focusButton = this.Q<SBM_Button>("ButtonObjectFocus");
            _focusButtonParent = _focusButton?.parent;

            if (_image != null)
                _image.RegisterCallback<ClickEvent>(OnImageClicked);

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
            BindImage(info.image);
            RefreshFocusButtonState();
        }

        void BindImage(Sprite sprite)
        {
            _hasImage = false;
            _imageBackground = default;

            if (_image == null)
                return;

            if (sprite != null)
            {
                _imageBackground = Background.FromSprite(sprite);
                _image.style.backgroundImage = _imageBackground;
                _image.style.display = DisplayStyle.Flex;
                _image.pickingMode = PickingMode.Position;
                _hasImage = true;
            }
            else
            {
                _image.style.display = DisplayStyle.None;
                _image.pickingMode = PickingMode.Ignore;
            }
        }

        void OnImageClicked(ClickEvent evt)
        {
            if (!_hasImage)
                return;

            ImageClicked?.Invoke(_imageBackground);
            evt.StopPropagation();
        }

        void BindPaths(AdditionalPathInfo[] paths)
        {
            _paths.Clear();
            ClearExtraFocusButtons();

            if (paths != null)
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    var pathInfo = paths[i];
                    if (pathInfo != null && !string.IsNullOrWhiteSpace(pathInfo.path))
                        _paths.Add(pathInfo);
                }
            }

            if (_focusButton != null)
            {
                _focusButton.style.display = _paths.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                if (_paths.Count > 0)
                    ConfigureFocusButton(_focusButton, _paths[0]);
            }

            if (_focusButtonParent != null)
                _focusButtonParent.style.overflow = Overflow.Visible;

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
            {
                var button = _extraFocusButtons[i];
                UnregisterFocusClick(button);
                button?.RemoveFromHierarchy();
            }
            _extraFocusButtons.Clear();
        }

        SBM_Button CreateFocusButton()
        {
            return new SBM_Button();
        }

        static void ApplyFocusButtonStyle(SBM_Button button, Color backgroundColor)
        {
            if (button == null)
                return;

            button.BackgroundColor = backgroundColor;
            button.TextScale = 2f;
            button.style.height = 25;
            button.style.width = StyleKeyword.Auto;
            button.style.alignSelf = Align.Stretch;
            button.style.unityTextAlign = TextAnchor.MiddleRight;
            button.style.paddingRight = 10;
            button.style.paddingLeft = 10;
            button.style.marginTop = 4;
            button.style.overflow = Overflow.Visible;
        }

        void ConfigureFocusButton(SBM_Button button, AdditionalPathInfo pathInfo)
        {
            if (button == null || pathInfo == null)
                return;

            ApplyFocusButtonStyle(button, pathInfo.ResolvedButtonColor);
            button.Text = pathInfo.DisplayTitle;
            UnregisterFocusClick(button);
            Action handler = () => OnFocusClicked(pathInfo);
            _focusClickHandlers[button] = handler;
            button.clicked += handler;
        }

        void UnregisterFocusClick(SBM_Button button)
        {
            if (button == null)
                return;

            if (_focusClickHandlers.TryGetValue(button, out var handler))
            {
                button.clicked -= handler;
                _focusClickHandlers.Remove(button);
            }
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

        void RefreshFocusButton(SBM_Button button, AdditionalPathInfo pathInfo)
        {
            if (button == null)
                return;

            var interactive = CanFocus(pathInfo);

            button.style.opacity = interactive ? 1f : DisabledOpacity;
            button.pickingMode = interactive ? PickingMode.Position : PickingMode.Ignore;
            button.SetEnabled(interactive);
        }

        bool CanFocus(AdditionalPathInfo pathInfo)
        {
            if (pathInfo == null || string.IsNullOrWhiteSpace(pathInfo.path))
                return false;

            if (pathInfo.IsAssetPath)
                return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(NormalizePath(pathInfo.path)) != null;

            return ResolveTargetOnAvatar(SBM_Header.CurrentAvatarDescriptor, pathInfo.path) != null;
        }

        void OnFocusClicked(AdditionalPathInfo pathInfo)
        {
            if (pathInfo == null || string.IsNullOrWhiteSpace(pathInfo.path))
                return;

            if (pathInfo.IsAssetPath)
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(NormalizePath(pathInfo.path));
                if (asset == null)
                {
                    RefreshFocusButtonState();
                    return;
                }

                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
                return;
            }

            var target = ResolveTargetOnAvatar(SBM_Header.CurrentAvatarDescriptor, pathInfo.path);
            if (target == null)
            {
                RefreshFocusButtonState();
                return;
            }

            Selection.activeGameObject = target.gameObject;
            EditorGUIUtility.PingObject(target.gameObject);
        }

        static string NormalizePath(string path) => path.Replace('\\', '/');

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
