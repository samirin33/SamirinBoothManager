using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace samirin33.SamirinBoothManager.UI.Parts
{
    /// <summary>
    /// ImagePreview.uxml の画像プレビュー表示。
    /// 閉じる / 背景クリックで非表示、前後ボタンで画像切替。
    /// </summary>
    public class ImagePreview : SBM_UxmlPartElement
    {
        public new class UxmlFactory : UxmlFactory<ImagePreview, UxmlTraits> { }
        public new class UxmlTraits : VisualElement.UxmlTraits { }

        const string BackAreaEnable = "BackArea_Enable";
        const string BackAreaDisable = "BackArea_Disable";

        readonly VisualElement _backArea;
        readonly VisualElement _mainImage;
        readonly SBM_Button _buttonClose;
        readonly SBM_Button _buttonBack;
        readonly SBM_Button _buttonNext;

        readonly List<Background> _galleryImages = new List<Background>();
        readonly List<Background> _images = new List<Background>();
        int _index;

        public bool IsOpen { get; private set; }

        public ImagePreview() : base(nameof(ImagePreview))
        {
            style.position = Position.Absolute;
            style.width = Length.Percent(100);
            style.height = Length.Percent(100);

            _backArea = this.Q<VisualElement>("ImagePreviewBackArea");
            _mainImage = this.Q<VisualElement>("MainImage");
            _buttonClose = this.Q<SBM_Button>("ButtomImageClose");
            _buttonBack = this.Q<SBM_Button>("ButtomBack");
            _buttonNext = this.Q<SBM_Button>("ButtomNext");

            // IgnorePointer / MainImage はクリックを透過し、ImagePreviewBackArea が受ける
            ConfigureBackdropPicking();

            if (_buttonClose != null)
                _buttonClose.clicked += Hide;

            if (_buttonBack != null)
                _buttonBack.clicked += ShowPrevious;

            if (_buttonNext != null)
                _buttonNext.clicked += ShowNext;

            if (_backArea != null)
                _backArea.RegisterCallback<ClickEvent>(OnBackAreaClicked);

            ApplyClosedImmediate();
        }

        void ConfigureBackdropPicking()
        {
            this.Query<VisualElement>("IgnorePointer").ForEach(element =>
            {
                element.pickingMode = PickingMode.Ignore;
            });

            if (_mainImage != null)
                _mainImage.pickingMode = PickingMode.Ignore;

            if (_buttonClose != null)
                _buttonClose.pickingMode = PickingMode.Position;
            if (_buttonBack != null)
                _buttonBack.pickingMode = PickingMode.Position;
            if (_buttonNext != null)
                _buttonNext.pickingMode = PickingMode.Position;
        }

        /// <summary>詳細パネルの PR 画像ギャラリーを設定する。</summary>
        public void SetImages(IReadOnlyList<Background> images)
        {
            _galleryImages.Clear();
            if (images != null)
            {
                for (int i = 0; i < images.Count; i++)
                    _galleryImages.Add(images[i]);
            }

            if (!IsOpen)
                return;

            UseGalleryImages();
            if (_index >= _images.Count)
                _index = Mathf.Max(0, _images.Count - 1);
            ApplyCurrentImage();
        }

        /// <summary>ギャラリー画像を index で表示する。</summary>
        public void Show(int index)
        {
            UseGalleryImages();
            if (_images.Count == 0)
                return;

            OpenAt(Mathf.Clamp(index, 0, _images.Count - 1));
        }

        /// <summary>単一画像をプレビュー表示する（ギャラリーは保持）。</summary>
        public void Show(Background background)
        {
            _images.Clear();
            _images.Add(background);
            OpenAt(0);
        }

        public void Hide()
        {
            ApplyClosedImmediate();
        }

        void UseGalleryImages()
        {
            _images.Clear();
            for (int i = 0; i < _galleryImages.Count; i++)
                _images.Add(_galleryImages[i]);
        }

        void OpenAt(int index)
        {
            _index = index;
            ApplyCurrentImage();

            if (IsOpen)
                return;

            IsOpen = true;
            style.display = DisplayStyle.Flex;
            pickingMode = PickingMode.Position;
            if (_backArea != null)
                _backArea.pickingMode = PickingMode.Position;

            SetClassPair(_backArea, BackAreaEnable, BackAreaDisable, true);
        }

        void ShowPrevious()
        {
            if (_images.Count <= 1)
                return;

            _index = (_index - 1 + _images.Count) % _images.Count;
            ApplyCurrentImage();
        }

        void ShowNext()
        {
            if (_images.Count <= 1)
                return;

            _index = (_index + 1) % _images.Count;
            ApplyCurrentImage();
        }

        void ApplyCurrentImage()
        {
            if (_mainImage != null && _images.Count > 0)
                _mainImage.style.backgroundImage = _images[_index];

            UpdateNavButtons();
        }

        void UpdateNavButtons()
        {
            var canNavigate = _images.Count > 1;
            SetDisplay(_buttonBack, canNavigate);
            SetDisplay(_buttonNext, canNavigate);
        }

        void OnBackAreaClicked(ClickEvent evt)
        {
            // ボタン等の子要素クリックでは閉じない（背景そのものだけ）
            if (evt.target != _backArea)
                return;

            Hide();
            evt.StopPropagation();
        }

        void ApplyClosedImmediate()
        {
            IsOpen = false;
            SetClassPair(_backArea, BackAreaEnable, BackAreaDisable, false);
            pickingMode = PickingMode.Ignore;
            if (_backArea != null)
                _backArea.pickingMode = PickingMode.Ignore;
            style.display = DisplayStyle.None;
        }

        static void SetClassPair(VisualElement element, string enableClass, string disableClass, bool enabled)
        {
            if (element == null)
                return;
            element.EnableInClassList(enableClass, enabled);
            element.EnableInClassList(disableClass, !enabled);
        }

        static void SetDisplay(VisualElement element, bool visible)
        {
            if (element == null)
                return;
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
