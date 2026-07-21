using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;

namespace samirin33.SamirinBoothManager.UI.Parts
{
    /// <summary>
    /// AssetElement.uxml を SamirinBoothAssetInfo の内容で埋める要素。
    /// </summary>
    public class AssetElement : SBM_UxmlPartElement
    {
        public new class UxmlFactory : UxmlFactory<AssetElement, UxmlTraits> { }
        public new class UxmlTraits : VisualElement.UxmlTraits { }

        const string FallbackImageGuid = "37a679a53a99c9842a4510b6e49ceec5";
        const long HoverSlideIntervalMs = 1000;

        static readonly Color PatternAColor = Hex("#383838");
        static readonly Color PatternBColor = Hex("#255B7F");
        static readonly Color PatternCColor = Hex("#692D31");

        readonly Label _nameLabel;
        readonly Label _categoryLabel;
        readonly VisualElement _imagesRoot;
        readonly VisualElement _scroll;
        readonly VisualElement _vertionBanner;
        readonly Label _vertionLabel;
        readonly Label _newVertionRemind;
        readonly Label _notImported;
        readonly VisualElement _attachedLabel;
        readonly VisualElement _hoverTarget;

        int _imageCount;
        int _pageIndex = -1;
        int _appliedPageIndex = -1;
        bool _transitionDisabled;
        Color _appliedBannerColor;
        bool _hasAppliedBannerColor;
        IVisualElementScheduledItem _hoverSlideSchedule;
        IVisualElementScheduledItem _restoreTransitionSchedule;

        public SamirinBoothAssetInfo BoundInfo { get; private set; }

        /// <summary>要素クリック時。BoundInfo を渡す。</summary>
        public event Action<SamirinBoothAssetInfo> clicked;

        public AssetElement() : base(nameof(AssetElement))
        {
            _nameLabel = this.Q<Label>("Name");
            _categoryLabel = this.Q<Label>("CategoryLabel");
            _imagesRoot = this.Q<VisualElement>("Images");
            _scroll = this.Q<VisualElement>("Scroll") ?? _imagesRoot;
            _vertionBanner = this.Q<VisualElement>("VertionBanner");
            _vertionLabel = this.Q<Label>("Vertion");
            _newVertionRemind = this.Q<Label>("NewVertionRemind");
            _notImported = this.Q<Label>("NotImported");
            _attachedLabel = this.Q<VisualElement>("AttachedLabel");
            _hoverTarget = this.Q<VisualElement>("AnimationParent") ?? this;

            SetAttached(false);

            if (_categoryLabel != null)
            {
                SamirinBoothCategoryUtil.SetupLabel(_categoryLabel);
                _categoryLabel.RegisterCallback<GeometryChangedEvent>(OnCategoryGeometryChanged);
            }

            if (_imagesRoot != null)
                _imagesRoot.style.overflow = Overflow.Hidden;

            if (_scroll != null)
                _scroll.usageHints = UsageHints.DynamicTransform;

            focusable = true;
            pickingMode = PickingMode.Position;
            _hoverTarget.pickingMode = PickingMode.Position;
            _hoverTarget.AddManipulator(new Clickable(InvokeClicked));

            _hoverTarget.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            _hoverTarget.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        void InvokeClicked()
        {
            clicked?.Invoke(BoundInfo);
        }

        public void Bind(SamirinBoothAssetInfo info)
        {
            StopHoverSlide(resetToStart: true);

            BoundInfo = info;
            if (info == null)
                return;

            if (_nameLabel != null)
            {
                var title = info.name ?? string.Empty;
                if (_nameLabel.text != title)
                    _nameLabel.text = title;
            }

            BindCategory(info.category);
            BindImages(info.images);

            var showVersionBanner = info.category != Category.Other;
            SetDisplay(_vertionBanner, showVersionBanner);
            if (showVersionBanner)
                BindVersionState(info);

            RefreshAttached(SBM_Header.CurrentAvatarDescriptor);
        }

        void BindCategory(Category category)
        {
            SamirinBoothCategoryUtil.BindLabel(_categoryLabel, category);
        }

        void OnCategoryGeometryChanged(GeometryChangedEvent evt)
        {
            if (Mathf.Abs(evt.newRect.width - evt.oldRect.width) < 0.5f
                && Mathf.Abs(evt.newRect.height - evt.oldRect.height) < 0.5f)
                return;
            SamirinBoothCategoryUtil.FitFontSize(_categoryLabel);
        }

        public void RefreshAttached(VRCAvatarDescriptor avatar)
        {
            if (BoundInfo == null)
            {
                SetAttached(false);
                return;
            }

            var attached = false;
            var variations = BoundInfo.variations;
            if (avatar != null && variations != null)
            {
                for (int i = 0; i < variations.Length; i++)
                {
                    var variation = variations[i];
                    if (variation == null || string.IsNullOrEmpty(variation.prefabPath))
                        continue;

                    if (SBM_Header.AvatarContainsPrefab(avatar, variation.prefabPath))
                    {
                        attached = true;
                        break;
                    }
                }
            }

            SetAttached(attached);
        }

        public void SetAttached(bool attached)
        {
            SetDisplay(_attachedLabel, attached);
        }

        void BindImages(Sprite[] images)
        {
            if (_scroll == null)
                return;

            _scroll.Clear();
            _imageCount = 0;
            _appliedPageIndex = -1;
            _pageIndex = 0;

            if (images != null)
            {
                for (int i = 0; i < images.Length; i++)
                {
                    if (images[i] == null)
                        continue;

                    _scroll.Add(CreateImageElement(Background.FromSprite(images[i]), _imageCount));
                    _imageCount++;
                }
            }

            if (_imageCount == 0)
            {
                _scroll.Add(CreateImageElement(Background.FromTexture2D(LoadFallbackTexture()), 0));
                _imageCount = 1;
            }

            ApplyScrollTranslate(animate: false);
        }

        static VisualElement CreateImageElement(Background background, int index)
        {
            var image = new VisualElement { name = "Image" };
            // 各画像はビューポートと同じサイズのまま、100% 間隔で横に並べる
            image.style.position = Position.Absolute;
            image.style.left = Length.Percent(index * 100f);
            image.style.top = 0;
            image.style.width = Length.Percent(100);
            image.style.height = Length.Percent(100);
            image.style.flexGrow = 0;
            image.style.flexShrink = 0;
            image.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
            image.style.backgroundImage = background;
            return image;
        }

        void OnPointerEnter(PointerEnterEvent evt)
        {
            if (_imageCount <= 1 || _scroll == null)
                return;

            StopHoverSlide(resetToStart: false);
            // ホバー開始から 2 秒後に最初の移動、以降 2 秒間隔
            _hoverSlideSchedule = schedule
                .Execute(AdvanceHoverSlide)
                .StartingIn(HoverSlideIntervalMs)
                .Every(HoverSlideIntervalMs);
        }

        void OnPointerLeave(PointerLeaveEvent evt)
        {
            StopHoverSlide(resetToStart: true);
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            _restoreTransitionSchedule?.Pause();
            _restoreTransitionSchedule = null;
            StopHoverSlide(resetToStart: false);
        }

        void AdvanceHoverSlide()
        {
            if (_imageCount <= 1 || _scroll == null)
                return;

            _pageIndex++;
            // 最大枚数の次 → 0% に戻す（0% ~ Length*100% の範囲、100% 単位スナップ）
            if (_pageIndex >= _imageCount)
                _pageIndex = 0;

            ApplyScrollTranslate(animate: true);
        }

        void StopHoverSlide(bool resetToStart)
        {
            if (_hoverSlideSchedule != null)
            {
                _hoverSlideSchedule.Pause();
                _hoverSlideSchedule = null;
            }

            if (!resetToStart || _pageIndex == 0)
                return;

            _pageIndex = 0;
            ApplyScrollTranslate(animate: true);
        }

        void ApplyScrollTranslate(bool animate)
        {
            if (_scroll == null || _pageIndex == _appliedPageIndex)
                return;

            if (!animate)
            {
                SetTransitionEnabled(false);
                _scroll.style.translate = new Translate(Length.Percent(-_pageIndex * 100f), 0);
                _appliedPageIndex = _pageIndex;
                _restoreTransitionSchedule?.Pause();
                _restoreTransitionSchedule = _scroll.schedule
                    .Execute(() => SetTransitionEnabled(true))
                    .StartingIn(1);
                return;
            }

            SetTransitionEnabled(true);
            _scroll.style.translate = new Translate(Length.Percent(-_pageIndex * 100f), 0);
            _appliedPageIndex = _pageIndex;
        }

        void SetTransitionEnabled(bool enabled)
        {
            if (_scroll == null)
                return;

            var shouldDisable = !enabled;
            if (_transitionDisabled == shouldDisable)
                return;

            _transitionDisabled = shouldDisable;

            if (enabled)
            {
                // UXML の transition 設定に戻す（インライン上書きを解除）
                _scroll.style.transitionDuration = StyleKeyword.Null;
            }
            else
            {
                _scroll.style.transitionDuration = new List<TimeValue> { new TimeValue(0f) };
            }
        }

        void BindVersionState(SamirinBoothAssetInfo info)
        {
            var infoVersion = new Version(
                Math.Max(0, info.majorVertion),
                Math.Max(0, info.minorVertion),
                Math.Max(0, info.patchVertion));

            if (!SamirinBoothImportUtil.TryGetInstalledVersion(info, out var installed))
            {
                ApplyPatternA();
                return;
            }

            // バージョン不明、または最新より古い → 更新あり表示
            if (installed == null || installed < infoVersion)
                ApplyPatternC(installed, info.updateRemind);
            else
                ApplyPatternB(installed);
        }

        void ApplyPatternA()
        {
            SetBannerColor(PatternAColor);
            SetDisplay(_vertionLabel, false);
            SetDisplay(_newVertionRemind, false);
            SetDisplay(_notImported, true);
        }

        void ApplyPatternB(Version installed)
        {
            SetBannerColor(PatternBColor);
            SetVertionText(SamirinBoothImportUtil.FormatInstalledVersion(installed));
            SetDisplay(_vertionLabel, true);
            SetDisplay(_newVertionRemind, false);
            SetDisplay(_notImported, false);
        }

        void ApplyPatternC(Version installed, bool showUpdateRemind)
        {
            SetBannerColor(PatternCColor);
            SetVertionText(SamirinBoothImportUtil.FormatInstalledVersion(installed));
            SetDisplay(_vertionLabel, true);
            SetDisplay(_newVertionRemind, showUpdateRemind);
            SetDisplay(_notImported, false);
        }

        void SetBannerColor(Color color)
        {
            if (_vertionBanner == null)
                return;
            if (_hasAppliedBannerColor && _appliedBannerColor == color)
                return;

            _vertionBanner.style.backgroundColor = color;
            _appliedBannerColor = color;
            _hasAppliedBannerColor = true;
        }

        void SetVertionText(string text)
        {
            if (_vertionLabel == null || _vertionLabel.text == text)
                return;
            _vertionLabel.text = text;
        }

        static void SetDisplay(VisualElement element, bool visible)
        {
            if (element == null)
                return;

            var next = visible ? DisplayStyle.Flex : DisplayStyle.None;
            var current = element.style.display;
            if (current.keyword != StyleKeyword.Null
                && current.keyword != StyleKeyword.Initial
                && current.value == next)
                return;

            element.style.display = next;
        }

        static Texture2D LoadFallbackTexture()
        {
            var path = AssetDatabase.GUIDToAssetPath(FallbackImageGuid);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            return color;
        }
    }
}
