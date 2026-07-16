using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace samirin33.SamirinBoothManager.UI.Parts
{
    /// <summary>
    /// SBM_Button.uxml を元にしたボタン要素。
    /// UXML 属性: text, icon（Texture2D アセットパス）, text-scale, background-color
    /// C#: Text / Icon / TextScale / BackgroundColor / clicked
    /// </summary>
    public class SBM_Button : SBM_UxmlPartElement
    {
        public new class UxmlFactory : UxmlFactory<SBM_Button, UxmlTraits> { }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            readonly UxmlStringAttributeDescription _text = new UxmlStringAttributeDescription
            {
                name = "text",
                defaultValue = ""
            };

            readonly UxmlStringAttributeDescription _icon = new UxmlStringAttributeDescription
            {
                name = "icon",
                defaultValue = ""
            };

            readonly UxmlFloatAttributeDescription _textScale = new UxmlFloatAttributeDescription
            {
                name = "text-scale",
                defaultValue = 1f
            };

            readonly UxmlColorAttributeDescription _backgroundColor = new UxmlColorAttributeDescription
            {
                name = "background-color",
                defaultValue = DefaultBackgroundColor
            };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);

                var button = (SBM_Button)ve;
                button.Text = _text.GetValueFromBag(bag, cc);
                button.TextScale = _textScale.GetValueFromBag(bag, cc);
                button.BackgroundColor = _backgroundColor.GetValueFromBag(bag, cc);

                var iconPath = _icon.GetValueFromBag(bag, cc);
                button.Icon = string.IsNullOrEmpty(iconPath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
            }
        }

        static readonly Color DefaultBackgroundColor = new Color(21f / 255f, 21f / 255f, 35f / 255f, 1f);

        const float PaddingRatio = 0.12f;
        const float IconOnlyRatio = 0.72f;
        const float IconWithTextRatio = 0.55f;
        const float BaseFontSizeRatio = 0.34f;
        const float DefaultTextScale = 1f;
        const float IconTextGapRatio = 0.08f;

        readonly VisualElement _animationParent;
        readonly VisualElement _background;
        readonly VisualElement _contents;
        readonly VisualElement _buttonIcon;
        readonly Label _buttonText;
        readonly Clickable _clickable;

        string _text = string.Empty;
        Texture2D _icon;
        float _textScale = DefaultTextScale;
        Color _backgroundColor = DefaultBackgroundColor;
        float _lastLayoutWidth = -1f;
        float _lastLayoutHeight = -1f;
        bool _updatingSizes;

        /// <summary>クリック時に呼ばれるイベント。</summary>
        public event Action clicked;

        public string Text
        {
            get => _text;
            set
            {
                _text = value ?? string.Empty;
                ApplyText();
                UpdateContentSizes();
            }
        }

        public Texture2D Icon
        {
            get => _icon;
            set
            {
                _icon = value;
                ApplyIcon();
                UpdateContentSizes();
            }
        }

        /// <summary>文字サイズの倍率。基準サイズに対して掛け算する（1 = 標準）。</summary>
        public float TextScale
        {
            get => _textScale;
            set
            {
                var next = Mathf.Max(0f, value);
                if (Mathf.Approximately(_textScale, next))
                    return;

                _textScale = next;
                UpdateContentSizes();
            }
        }

        /// <summary>ボタン背景色。</summary>
        public Color BackgroundColor
        {
            get => _backgroundColor;
            set
            {
                if (_backgroundColor == value)
                    return;

                _backgroundColor = value;
                ApplyBackgroundColor();
            }
        }

        public SBM_Button() : base(nameof(SBM_Button))
        {
            _animationParent = this.Q<VisualElement>("AnimationParent");
            _background = this.Q<VisualElement>("Background");
            _contents = this.Q<VisualElement>("Contents");
            _buttonIcon = this.Q<VisualElement>("ButtonIcon");
            _buttonText = this.Q<Label>("ButtonText");

            focusable = true;
            pickingMode = PickingMode.Position;

            // ボタンの scale アニメーションが周囲でクリップされないようにする
            style.overflow = Overflow.Visible;
            var buttonRoot = this.Q<VisualElement>("ButtonRoot");
            if (buttonRoot != null)
            {
                buttonRoot.pickingMode = PickingMode.Ignore;
                buttonRoot.style.flexGrow = 1;
            }

            if (_animationParent != null)
                _animationParent.pickingMode = PickingMode.Position;

            if (_background != null)
                _background.pickingMode = PickingMode.Ignore;

            if (_contents != null)
                _contents.pickingMode = PickingMode.Ignore;

            // テンプレートのプレースホルダは属性未指定時に非表示にする
            _text = string.Empty;
            _icon = null;
            ApplyText();
            ApplyIcon();
            ApplyBackgroundColor();

            // Clickable は押下中に対象へ :active を付与する
            var clickTarget = _animationParent ?? this;
            clickTarget.usageHints = UsageHints.DynamicTransform;
            _clickable = new Clickable(InvokeClicked);
            clickTarget.AddManipulator(_clickable);

            RegisterCallback<KeyDownEvent>(OnKeyDown);
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        void InvokeClicked()
        {
            clicked?.Invoke();
        }

        void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Return
                && evt.keyCode != KeyCode.KeypadEnter
                && evt.keyCode != KeyCode.Space)
                return;

            InvokeClicked();
            evt.StopPropagation();
            evt.PreventDefault();
        }

        void OnGeometryChanged(GeometryChangedEvent evt)
        {
            if (_updatingSizes)
                return;

            // scale による見た目変化ではなく、レイアウトサイズ変化のときだけ更新する
            var width = layout.width;
            var height = layout.height;
            if (width <= 0f || height <= 0f)
                return;

            if (Mathf.Abs(width - _lastLayoutWidth) < 0.5f
                && Mathf.Abs(height - _lastLayoutHeight) < 0.5f)
                return;

            _lastLayoutWidth = width;
            _lastLayoutHeight = height;
            UpdateContentSizes(width, height);
        }

        void ApplyText()
        {
            if (_buttonText == null)
                return;

            var hasText = !string.IsNullOrEmpty(_text);
            _buttonText.text = _text;
            _buttonText.style.display = hasText ? DisplayStyle.Flex : DisplayStyle.None;
            _buttonText.style.unityTextAlign = TextAnchor.MiddleCenter;
            _buttonText.style.whiteSpace = WhiteSpace.NoWrap;
            _buttonText.style.overflow = Overflow.Hidden;
            _buttonText.style.textOverflow = TextOverflow.Clip;
        }

        void ApplyIcon()
        {
            if (_buttonIcon == null)
                return;

            if (_icon != null)
            {
                _buttonIcon.style.backgroundImage = Background.FromTexture2D(_icon);
                _buttonIcon.style.display = DisplayStyle.Flex;
                _buttonIcon.style.flexShrink = 0;
                _buttonIcon.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            }
            else
            {
                _buttonIcon.style.backgroundImage = StyleKeyword.None;
                _buttonIcon.style.display = DisplayStyle.None;
            }
        }

        void ApplyBackgroundColor()
        {
            if (_background == null)
                return;

            _background.style.backgroundColor = _backgroundColor;
        }

        void UpdateContentSizes()
        {
            var width = layout.width;
            var height = layout.height;
            if (width <= 0f || height <= 0f)
            {
                width = resolvedStyle.width;
                height = resolvedStyle.height;
            }

            if (float.IsNaN(width) || float.IsNaN(height) || width <= 0f || height <= 0f)
                return;

            _lastLayoutWidth = width;
            _lastLayoutHeight = height;
            UpdateContentSizes(width, height);
        }

        void UpdateContentSizes(float width, float height)
        {
            // 縦横の狭い方を基準にする
            var minSide = Mathf.Min(width, height);
            var padding = Mathf.Min(minSide * PaddingRatio, minSide * 0.35f);
            var inner = Mathf.Max(1f, minSide - padding * 2f);
            var hasText = !string.IsNullOrEmpty(_text);
            var hasIcon = _icon != null;

            _updatingSizes = true;
            try
            {
                if (_contents != null)
                {
                    _contents.style.paddingTop = padding;
                    _contents.style.paddingRight = padding;
                    _contents.style.paddingBottom = padding;
                    _contents.style.paddingLeft = padding;
                    _contents.style.flexDirection = FlexDirection.Row;
                    _contents.style.justifyContent = Justify.Center;
                    _contents.style.alignItems = Align.Center;
                    _contents.style.overflow = Overflow.Visible;
                }

                if (_buttonIcon != null)
                {
                    if (hasIcon)
                    {
                        var iconSize = inner * (hasText ? IconWithTextRatio : IconOnlyRatio);
                        _buttonIcon.style.width = iconSize;
                        _buttonIcon.style.height = iconSize;
                        _buttonIcon.style.minWidth = iconSize;
                        _buttonIcon.style.minHeight = iconSize;
                        _buttonIcon.style.maxWidth = iconSize;
                        _buttonIcon.style.maxHeight = iconSize;
                        _buttonIcon.style.flexShrink = 0;
                        _buttonIcon.style.marginRight = hasText ? minSide * IconTextGapRatio : 0f;
                    }
                    else
                    {
                        _buttonIcon.style.marginRight = 0f;
                    }
                }

                if (_buttonText != null && hasText)
                {
                    // ラベルは内容サイズを基準にし、強制の % 指定でレイアウトを崩さない
                    var fontSize = Mathf.Max(1f, inner * BaseFontSizeRatio * _textScale);
                    fontSize = Mathf.Min(fontSize, inner);
                    _buttonText.style.fontSize = fontSize;
                    _buttonText.style.width = StyleKeyword.Auto;
                    _buttonText.style.height = StyleKeyword.Auto;
                    _buttonText.style.minWidth = StyleKeyword.Auto;
                    _buttonText.style.minHeight = StyleKeyword.Auto;
                    _buttonText.style.maxWidth = Length.Percent(100);
                    _buttonText.style.maxHeight = Length.Percent(100);
                    _buttonText.style.flexGrow = hasIcon ? 1 : 0;
                    _buttonText.style.flexShrink = 1;
                    _buttonText.style.unityTextAlign = TextAnchor.MiddleCenter;
                    _buttonText.style.whiteSpace = WhiteSpace.NoWrap;
                }
            }
            finally
            {
                _updatingSizes = false;
            }
        }
    }
}
