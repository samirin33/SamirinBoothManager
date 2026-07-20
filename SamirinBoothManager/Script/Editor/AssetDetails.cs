using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace samirin33.SamirinBoothManager.UI.Parts
{
    /// <summary>
    /// AssetDetails.uxml の表示／非表示と SamirinBoothAssetInfo の反映を行う。
    /// </summary>
    public class AssetDetails : SBM_UxmlPartElement
    {
        public new class UxmlFactory : UxmlFactory<AssetDetails, UxmlTraits> { }
        public new class UxmlTraits : VisualElement.UxmlTraits { }

        const string BackAreaEnable = "BackArea_Enable";
        const string BackAreaDisable = "BackArea_Disable";
        const string DtailGroupEnable = "DtailGroup_Enable";
        const string DtailGroupDisable = "DtailGroup_Disable";
        const string FallbackImageGuid = "37a679a53a99c9842a4510b6e49ceec5";
        const long HideTransitionMs = 750;

        readonly VisualElement _backArea;
        readonly VisualElement _informationsGroup;
        readonly VisualElement _dtailGroup;
        readonly AttachPanel _attachPanel;
        readonly SBM_Button _buttonClose;
        readonly SBM_Button _buttonBooth;

        readonly Label _price;
        readonly Label _name;
        readonly Label _description;
        readonly Label _categoryLabel;
        readonly VisualElement _imageContents;
        readonly VisualElement _prMovie;
        readonly Label _youtubeLink;
        readonly Label _updateDate;
        readonly Label _releaseDate;
        readonly VisualElement _versionInfo;
        readonly VisualElement _hasImportedGroup;
        readonly Label _hasImportedLabel;
        readonly Label _currentVertionLabel;
        readonly VisualElement _currentVersionGroup;
        readonly Label _installedVertionLabel;
        readonly VisualElement _latestVersionGroup;
        readonly Label _latestVertionLabel;
        readonly Label _newVertionRemind;
        readonly VisualElement _additionalInfomations;
        readonly VisualElement _howToSetupInfomations;
        readonly Foldout _pastVersionFoldout;
        readonly VisualElement _pastVersionFoldoutParent;
        int _pastVersionFoldoutIndex = -1;
        readonly VisualElement _pastUpdateInfomations;
        readonly VisualElement _latestUpdateInfomations;
        readonly VisualElement _relatedItems;
        readonly VisualElement _relatedItemsContainer;

        IVisualElementScheduledItem _pending;
        string _boothUrl = string.Empty;
        string _youtubeUrl = string.Empty;

        public SamirinBoothAssetInfo BoundInfo { get; private set; }
        public bool IsOpen { get; private set; }

        public event Action<SamirinBoothAssetInfo> Shown;
        public event Action Hidden;

        public AssetDetails() : base(nameof(AssetDetails))
        {
            style.flexGrow = 1;
            style.width = Length.Percent(100);
            style.height = Length.Percent(100);
            style.position = Position.Absolute;

            _backArea = this.Q<VisualElement>("BackArea");
            _informationsGroup = this.Q<VisualElement>("InformationsGroup");
            _dtailGroup = this.Q<VisualElement>("DtailGroup");
            _attachPanel = this.Q<AttachPanel>("AttachPanel");
            _buttonClose = this.Q<SBM_Button>("ButtonClose");
            _buttonBooth = this.Q<SBM_Button>("ButtonBooth");

            _price = this.Q<Label>("Price");
            _name = this.Q<Label>("Name");
            _description = this.Q<Label>("Discription");
            _categoryLabel = this.Q<Label>("CategoryLabel");
            SamirinBoothCategoryUtil.SetupLabel(_categoryLabel);
            _imageContents = this.Q<VisualElement>("ImageContents");
            _prMovie = this.Q<VisualElement>("PRMovie");
            _youtubeLink = this.Q<Label>("YoutubeLink");
            _updateDate = this.Q<Label>("UpdateDate");
            _releaseDate = this.Q<Label>("ReleaseDate");
            _versionInfo = this.Q<VisualElement>("VersionInfo");
            _hasImportedGroup = this.Q<VisualElement>("HasImopertedGroup");
            _hasImportedLabel = this.Q<Label>("HasImported");
            _currentVertionLabel = this.Q<Label>("CurrentVertion");
            _currentVersionGroup = this.Q<VisualElement>("CurrentVersionGroup");
            _installedVertionLabel = _currentVersionGroup?.Q<Label>("LatestVertion");
            _latestVersionGroup = this.Q<VisualElement>("LatestVersionGroup");
            _latestVertionLabel = _latestVersionGroup?.Q<Label>("LatestVertion");
            _newVertionRemind = this.Q<Label>("NewVertionRemind");
            _additionalInfomations = this.Q<VisualElement>("AdditionalInfomations");
            _howToSetupInfomations = this.Q<VisualElement>("HowToSetupInfomations");
            _pastVersionFoldout = this.Q<Foldout>("PastVarsion");
            _pastVersionFoldoutParent = _pastVersionFoldout?.parent;
            _pastVersionFoldoutIndex = _pastVersionFoldoutParent != null && _pastVersionFoldout != null
                ? _pastVersionFoldoutParent.IndexOf(_pastVersionFoldout)
                : -1;
            _pastUpdateInfomations = this.Q<VisualElement>("PastUpdateInfomations");
            _latestUpdateInfomations = this.Q<VisualElement>("LatestUpdateInfomations");
            _relatedItems = this.Q<VisualElement>("RelatedItems");
            _relatedItemsContainer = this.Q<VisualElement>("Items");

            if (_pastVersionFoldout != null)
            {
                _pastVersionFoldout.RegisterValueChangedCallback(OnPastVersionFoldoutChanged);
                SetPastUpdateInfomationsVisible(_pastVersionFoldout.value);
            }

            if (_informationsGroup != null)
                _informationsGroup.pickingMode = PickingMode.Ignore;

            if (_buttonClose != null)
                _buttonClose.clicked += Hide;

            if (_buttonBooth != null)
                _buttonBooth.clicked += OnBoothClicked;

            if (_backArea != null)
                _backArea.RegisterCallback<ClickEvent>(OnBackAreaClicked);

            if (_youtubeLink != null)
            {
                _youtubeLink.RegisterCallback<ClickEvent>(OnYoutubeClicked);
                _youtubeLink.style.unityTextAlign = TextAnchor.MiddleLeft;
            }

            ApplyClosedImmediate();
        }

        public void Show(SamirinBoothAssetInfo info)
        {
            BoundInfo = info;

            // 読み込み完了まで詳細本体を隠し、AttachPanel の Enable も保留する
            SetDtailGroupVisible(false);
            SetClassPair(_attachPanel, DtailGroupEnable, DtailGroupDisable, false);

            Bind(info);

            if (!IsOpen)
            {
                IsOpen = true;
                OpenAnimated();
            }
            else
            {
                ScheduleRevealAfterLoad();
            }

            Shown?.Invoke(info);
        }

        public void Hide()
        {
            if (!IsOpen)
                return;

            IsOpen = false;
            CloseAnimated();
            Hidden?.Invoke();
        }

        public void Bind(SamirinBoothAssetInfo info)
        {
            if (info == null)
                return;

            BoundInfo = info;
            _boothUrl = info.url ?? string.Empty;
            _youtubeUrl = info.youtubeUrl ?? string.Empty;

            if (_name != null)
                _name.text = info.name ?? string.Empty;

            if (_description != null)
                _description.text = info.description ?? string.Empty;

            SamirinBoothCategoryUtil.BindLabel(_categoryLabel, info.category);

            if (_price != null)
            {
                var price = string.IsNullOrWhiteSpace(info.price) ? "-" : info.price;
                _price.text = $"価格: {price}";
            }

            if (_releaseDate != null)
                _releaseDate.text = $"公開日 {FormatDate(info.releaseDate)}";

            if (_updateDate != null)
                _updateDate.text = $"最終更新日 {FormatDate(info.updateDate)}";

            BindImages(info.images);
            BindYoutube(info.youtubeUrl);

            var showVersionInfo = info.category != Category.Other;
            SetDisplay(_versionInfo, showVersionInfo);

            var isImported = BindVersionState(info);
            BindAdditionalInfos(info.additionalInfos);
            BindHowToSetupInfos(info.howToSetupInfos);
            BindUpdateInfos(info.updateInfos);
            BindRelatedAssets(info.relatedAssets);

            _attachPanel?.Bind(info, isImported);
        }

        const float DetailImageHeight = 250f;

        void BindImages(Sprite[] images)
        {
            if (_imageContents == null)
                return;

            _imageContents.Clear();

            var added = 0;
            if (images != null)
            {
                for (int i = 0; i < images.Length; i++)
                {
                    if (images[i] == null)
                        continue;

                    _imageContents.Add(CreateDetailImage(images[i]));
                    added++;
                }
            }

            if (added == 0)
            {
                var fallback = LoadFallbackTexture();
                _imageContents.Add(CreateDetailImage(fallback));
            }
        }

        static VisualElement CreateDetailImage(Sprite sprite)
        {
            var aspect = GetSpriteAspectRatio(sprite);
            return CreateDetailImage(Background.FromSprite(sprite), aspect);
        }

        static VisualElement CreateDetailImage(Texture2D texture)
        {
            var aspect = GetTextureAspectRatio(texture);
            return CreateDetailImage(Background.FromTexture2D(texture), aspect);
        }

        static VisualElement CreateDetailImage(Background background, float aspectRatio)
        {
            var image = new VisualElement { name = "Image" };
            image.AddToClassList("DtailImages");
            image.style.height = DetailImageHeight;
            image.style.width = DetailImageHeight * Mathf.Max(0.01f, aspectRatio);
            image.style.flexShrink = 0;
            image.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            image.style.backgroundImage = background;
            return image;
        }

        static float GetSpriteAspectRatio(Sprite sprite)
        {
            if (sprite == null || sprite.rect.height <= 0f)
                return 1f;
            return sprite.rect.width / sprite.rect.height;
        }

        static float GetTextureAspectRatio(Texture2D texture)
        {
            if (texture == null || texture.height <= 0)
                return 1f;
            return (float)texture.width / texture.height;
        }

        void BindYoutube(string url)
        {
            var hasUrl = !string.IsNullOrWhiteSpace(url);
            SetDisplay(_prMovie, hasUrl);
            if (_youtubeLink != null)
                _youtubeLink.text = hasUrl ? url : string.Empty;
        }

        static readonly Color HasImportedColor = new Color(61f / 255f, 86f / 255f, 104f / 255f, 1f);
        static readonly Color NotImportedColor = new Color(0x38 / 255f, 0x38 / 255f, 0x38 / 255f, 1f);

        bool BindVersionState(SamirinBoothAssetInfo info)
        {
            var latest = new Version(
                Math.Max(0, info.majorVertion),
                Math.Max(0, info.minorVertion),
                Math.Max(0, info.patchVertion));
            var latestText = FormatVersion(latest);

            if (_latestVertionLabel != null)
                _latestVertionLabel.text = latestText;

            if (!SamirinBoothImportUtil.TryGetInstalledVersion(info, out var installed))
            {
                ApplyNotImported();
                return false;
            }

            var installedText = SamirinBoothImportUtil.FormatInstalledVersion(installed);

            if (_installedVertionLabel != null)
                _installedVertionLabel.text = installedText;

            if (_hasImportedLabel != null)
                _hasImportedLabel.text = "インポート済み！";

            if (_currentVertionLabel != null)
                _currentVertionLabel.text = installedText;

            if (_hasImportedGroup != null)
                _hasImportedGroup.style.backgroundColor = HasImportedColor;

            SetDisplay(_hasImportedGroup, true);
            SetDisplay(_hasImportedLabel, true);
            SetDisplay(_currentVertionLabel, false);
            SetDisplay(_currentVersionGroup, true);
            SetDisplay(_latestVersionGroup, true);
            // 不明バージョンは比較不能のため更新あり扱い（updateRemind がオフなら非表示）
            SetDisplay(_newVertionRemind,
                info.updateRemind && (installed == null || installed < latest));
            return true;
        }

        void ApplyNotImported()
        {
            if (_installedVertionLabel != null)
                _installedVertionLabel.text = "-";

            if (_hasImportedLabel != null)
                _hasImportedLabel.text = "未インポート";

            if (_currentVertionLabel != null)
                _currentVertionLabel.text = string.Empty;

            if (_hasImportedGroup != null)
                _hasImportedGroup.style.backgroundColor = NotImportedColor;

            SetDisplay(_hasImportedGroup, true);
            SetDisplay(_hasImportedLabel, true);
            SetDisplay(_currentVertionLabel, false);
            SetDisplay(_currentVersionGroup, false);
            SetDisplay(_latestVersionGroup, true);
            SetDisplay(_newVertionRemind, false);
        }

        void BindAdditionalInfos(global::AdditionalInfo[] infos)
        {
            if (_additionalInfomations == null)
                return;

            _additionalInfomations.Clear();

            if (infos == null || infos.Length == 0)
            {
                SetDisplay(_additionalInfomations, false);
                return;
            }

            SetDisplay(_additionalInfomations, true);
            for (int i = 0; i < infos.Length; i++)
            {
                if (infos[i] == null)
                    continue;

                var element = new AdditionalInfo();
                element.Bind(infos[i], BoundInfo);
                _additionalInfomations.Add(element);
            }
        }

        void BindHowToSetupInfos(global::AdditionalInfo[] infos)
        {
            if (_howToSetupInfomations == null)
                return;

            _howToSetupInfomations.Clear();
            var panel = _howToSetupInfomations.parent;

            if (infos == null || infos.Length == 0)
            {
                SetDisplay(panel, false);
                return;
            }

            var added = 0;
            for (int i = 0; i < infos.Length; i++)
            {
                if (infos[i] == null)
                    continue;

                var element = new AdditionalInfo();
                element.Bind(infos[i], BoundInfo);
                _howToSetupInfomations.Add(element);
                added++;
            }

            if (added == 0)
            {
                SetDisplay(panel, false);
                return;
            }

            SetDisplay(panel, true);
            SetDisplay(_howToSetupInfomations, true);
        }

        void BindUpdateInfos(global::UpdateInfo[] infos)
        {
            if (_latestUpdateInfomations == null)
                return;

            _latestUpdateInfomations.Clear();
            _pastUpdateInfomations?.Clear();

            var valid = CollectValidUpdateInfos(infos);
            var historyPanel = _latestUpdateInfomations.parent;

            if (valid.Count == 0)
            {
                SetDisplay(historyPanel, false);
                return;
            }

            SetDisplay(historyPanel, true);
            AddUpdateInfoElement(_latestUpdateInfomations, valid[valid.Count - 1]);

            if (valid.Count <= 1)
            {
                SetPastVersionFoldoutVisible(false);
                SetPastUpdateInfomationsVisible(false);
                return;
            }

            if (_pastVersionFoldout != null)
            {
                _pastVersionFoldout.SetValueWithoutNotify(false);
                SetPastVersionFoldoutVisible(true);
            }

            SetPastUpdateInfomationsVisible(false);

            for (int i = 0; i < valid.Count - 1; i++)
                AddUpdateInfoElement(_pastUpdateInfomations, valid[i]);
        }

        void SetPastVersionFoldoutVisible(bool visible)
        {
            if (_pastVersionFoldout == null || _pastVersionFoldoutParent == null)
                return;

            var inHierarchy = _pastVersionFoldout.parent != null;
            if (visible)
            {
                if (!inHierarchy)
                {
                    var index = Mathf.Clamp(_pastVersionFoldoutIndex, 0, _pastVersionFoldoutParent.childCount);
                    _pastVersionFoldoutParent.Insert(index, _pastVersionFoldout);
                }
            }
            else if (inHierarchy)
            {
                _pastVersionFoldoutIndex = _pastVersionFoldoutParent.IndexOf(_pastVersionFoldout);
                _pastVersionFoldout.RemoveFromHierarchy();
                SetPastUpdateInfomationsVisible(false);
            }
        }

        void SetPastUpdateInfomationsVisible(bool visible)
        {
            SetDisplay(_pastUpdateInfomations, visible);
        }

        static List<global::UpdateInfo> CollectValidUpdateInfos(global::UpdateInfo[] infos)
        {
            var list = new List<global::UpdateInfo>();
            if (infos == null)
                return list;

            for (int i = 0; i < infos.Length; i++)
            {
                if (infos[i] != null)
                    list.Add(infos[i]);
            }

            return list;
        }

        void AddUpdateInfoElement(VisualElement container, global::UpdateInfo info)
        {
            if (container == null || info == null)
                return;

            var element = new UpdateInfo();
            element.Bind(info);
            container.Add(element);
        }

        void OnPastVersionFoldoutChanged(ChangeEvent<bool> evt)
        {
            SetPastUpdateInfomationsVisible(evt.newValue);
        }

        void BindRelatedAssets(SamirinBoothAssetInfo[] related)
        {
            if (_relatedItemsContainer == null)
                return;

            _relatedItemsContainer.Clear();

            if (related == null || related.Length == 0)
            {
                SetDisplay(_relatedItems, false);
                return;
            }

            SetDisplay(_relatedItems, true);
            var avatar = SBM_Header.CurrentAvatarDescriptor;

            for (int i = 0; i < related.Length; i++)
            {
                if (related[i] == null)
                    continue;

                var element = new AssetElement();
                element.AddToClassList("SBM_AssetElement");
                element.Bind(related[i]);
                element.RefreshAttached(avatar);
                element.clicked += OnRelatedAssetClicked;
                _relatedItemsContainer.Add(element);
            }
        }

        void OnRelatedAssetClicked(SamirinBoothAssetInfo info)
        {
            if (info == null)
                return;
            Show(info);
        }

        void OnBoothClicked()
        {
            if (!string.IsNullOrWhiteSpace(_boothUrl))
                Application.OpenURL(_boothUrl);
        }

        void OnYoutubeClicked(ClickEvent evt)
        {
            if (string.IsNullOrWhiteSpace(_youtubeUrl))
                return;
            Application.OpenURL(_youtubeUrl);
            evt.StopPropagation();
        }

        void OnBackAreaClicked(ClickEvent evt)
        {
            Hide();
            evt.StopPropagation();
        }

        void ApplyClosedImmediate()
        {
            CancelPending();
            SetEnabledState(false);
            SetDtailGroupVisible(false);
            style.display = DisplayStyle.None;
            SetPicking(false);
        }

        void OpenAnimated()
        {
            CancelPending();
            SetEnabledState(false);
            SetDtailGroupVisible(false);
            style.display = DisplayStyle.Flex;
            SetPicking(true);
            ScheduleRevealAfterLoad();
        }

        void ScheduleRevealAfterLoad()
        {
            CancelPending();
            _pending = schedule.Execute(() =>
            {
                if (!IsOpen)
                    return;

                // 内部 Bind 完了後に DtailGroup を出し、AttachPanel へ Enable を付与する
                SetDtailGroupVisible(true);
                SetEnabledState(true);
            }).StartingIn(16);
        }

        void CloseAnimated()
        {
            CancelPending();
            SetEnabledState(false);
            SetPicking(false);

            _pending = schedule
                .Execute(() =>
                {
                    if (IsOpen)
                        return;
                    SetDtailGroupVisible(false);
                    style.display = DisplayStyle.None;
                })
                .StartingIn(HideTransitionMs);
        }

        void SetEnabledState(bool enabled)
        {
            SetClassPair(_backArea, BackAreaEnable, BackAreaDisable, enabled);
            SetClassPair(_informationsGroup, DtailGroupEnable, DtailGroupDisable, enabled);
            SetAttachPanelEnabled(enabled);
        }

        /// <summary>
        /// AttachPanel が display:none のまま Enable すると Transition がスキップされ、
        /// 次回表示時のスライドが短く／速く見えるため、表示可能なときだけ Enable する。
        /// </summary>
        void SetAttachPanelEnabled(bool enabled)
        {
            if (_attachPanel == null)
                return;

            var shouldEnable = enabled && _attachPanel.IsContentVisible;
            SetClassPair(_attachPanel, DtailGroupEnable, DtailGroupDisable, shouldEnable);
        }

        void SetDtailGroupVisible(bool visible)
        {
            SetDisplay(_dtailGroup, visible);
        }

        static void SetClassPair(VisualElement element, string enableClass, string disableClass, bool enabled)
        {
            if (element == null)
                return;
            element.EnableInClassList(enableClass, enabled);
            element.EnableInClassList(disableClass, !enabled);
        }

        void SetPicking(bool enabled)
        {
            pickingMode = enabled ? PickingMode.Position : PickingMode.Ignore;
            if (_backArea != null)
                _backArea.pickingMode = enabled ? PickingMode.Position : PickingMode.Ignore;
        }

        void CancelPending()
        {
            _pending?.Pause();
            _pending = null;
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

        static string FormatDate(global::DateTime date)
        {
            if (date == null || date.year <= 0)
                return "----/--/--";
            return $"{date.year:0000}/{date.month:00}/{date.day:00}";
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
    }
}
