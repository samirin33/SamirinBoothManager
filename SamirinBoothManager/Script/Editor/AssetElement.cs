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
    /// AssetElement.uxml を SamirinBoothAssetInfo の内容で埋める要素。
    /// ホバー中は 2 秒ごとに画像を 100% 単位でスライドする。
    /// </summary>
    public class AssetElement : SBM_UxmlPartElement
    {
        public new class UxmlFactory : UxmlFactory<AssetElement, UxmlTraits> { }
        public new class UxmlTraits : VisualElement.UxmlTraits { }

        const string FallbackImageGuid = "37a679a53a99c9842a4510b6e49ceec5";
        const long HoverSlideIntervalMs = 2000;

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

        static readonly string[] CategoryNames =
        {
            "アバター",
            "アバターギミック",
            "衣装・アクセサリー",
            "ワールド",
            "ワールドギミック",
            "3Dモデル",
            "その他"
        };

        static readonly float CategoryHueStart = 0f;       // 赤
        static readonly float CategoryHueEnd = 270f / 360f; // 紫
        static readonly float CategorySaturation = 0.75f;
        static readonly float CategoryValue = 0.90f;
        static readonly float CategoryAlpha = 0.4f;
        const float CategoryFontMax = 12f;
        const float CategoryFontMin = 6f;

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
                _categoryLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                _categoryLabel.style.color = Color.white;
                _categoryLabel.style.overflow = Overflow.Hidden;
                _categoryLabel.style.whiteSpace = WhiteSpace.NoWrap;
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
            BindVersionState(info);
            RefreshAttached(SBM_Header.CurrentAvatarDescriptor);
        }

        void BindCategory(Category category)
        {
            if (_categoryLabel == null)
                return;

            var index = Mathf.Clamp((int)category, 0, CategoryNames.Length - 1);
            _categoryLabel.text = CategoryNames[index];
            _categoryLabel.style.backgroundColor = GetCategoryColor(index);
            FitCategoryFontSize();
        }

        void OnCategoryGeometryChanged(GeometryChangedEvent evt)
        {
            if (Mathf.Abs(evt.newRect.width - evt.oldRect.width) < 0.5f
                && Mathf.Abs(evt.newRect.height - evt.oldRect.height) < 0.5f)
                return;
            FitCategoryFontSize();
        }

        void FitCategoryFontSize()
        {
            if (_categoryLabel == null)
                return;

            var text = _categoryLabel.text;
            if (string.IsNullOrEmpty(text))
                return;

            float width = _categoryLabel.layout.width;
            float height = _categoryLabel.layout.height;
            if (width <= 1f || height <= 1f)
            {
                width = _categoryLabel.resolvedStyle.width;
                height = _categoryLabel.resolvedStyle.height;
            }

            if (float.IsNaN(width) || float.IsNaN(height) || width <= 1f || height <= 1f)
            {
                _categoryLabel.schedule.Execute(FitCategoryFontSize).StartingIn(1);
                return;
            }

            // 余白を少し確保
            float availW = Mathf.Max(1f, width - 4f);
            float availH = Mathf.Max(1f, height - 2f);
            float fontSize = Mathf.Min(CategoryFontMax, availH);

            while (fontSize > CategoryFontMin)
            {
                _categoryLabel.style.fontSize = fontSize;
                var size = _categoryLabel.MeasureTextSize(
                    text,
                    availW,
                    VisualElement.MeasureMode.Undefined,
                    availH,
                    VisualElement.MeasureMode.Undefined);

                if (size.x <= availW && size.y <= availH)
                    break;

                fontSize -= 0.5f;
            }

            _categoryLabel.style.fontSize = Mathf.Max(CategoryFontMin, fontSize);
        }

        static Color GetCategoryColor(int index)
        {
            int max = Mathf.Max(1, CategoryNames.Length - 1);
            float t = Mathf.Clamp01(index / (float)max);
            float hue = Mathf.Lerp(CategoryHueStart, CategoryHueEnd, t);
            var color = Color.HSVToRGB(hue, CategorySaturation, CategoryValue);
            color.a = CategoryAlpha;
            return color;
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
            var packagePath = $"Assets/samirin33/{info.folderName}/PackageAssetInfo.json";
            var absolutePath = ToAbsolutePath(packagePath);
            var infoVersion = new Version(
                Math.Max(0, info.majorVertion),
                Math.Max(0, info.minorVertion),
                Math.Max(0, info.patchVertion));

            if (string.IsNullOrEmpty(info.folderName) || !File.Exists(absolutePath))
            {
                ApplyPatternA();
                return;
            }

            var installed = ReadPackageJsonVersion(absolutePath);
            if (installed == null)
            {
                ApplyPatternA();
                return;
            }

            if (installed < infoVersion)
                ApplyPatternC(installed);
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
            SetVertionText(FormatVersion(installed));
            SetDisplay(_vertionLabel, true);
            SetDisplay(_newVertionRemind, false);
            SetDisplay(_notImported, false);
        }

        void ApplyPatternC(Version installed)
        {
            SetBannerColor(PatternCColor);
            SetVertionText(FormatVersion(installed));
            SetDisplay(_vertionLabel, true);
            SetDisplay(_newVertionRemind, true);
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

        static string FormatVersion(Version version)
        {
            return $"ver{version.Major}.{version.Minor}.{version.Build}";
        }

        static Texture2D LoadFallbackTexture()
        {
            var path = AssetDatabase.GUIDToAssetPath(FallbackImageGuid);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            return color;
        }
    }
}
