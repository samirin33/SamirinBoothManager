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
        const string SupportYesGuid = "1547f2a2b2e48b44980e676c904e490c";
        const string SupportNoGuid = "cdaf3579735123a4a94a6f7a35aa7931";
        const string InformationFolder = "Assets/samirin33/SamirinBoothInformation";
        const string ItemAnalysisFileName = "ItemAnalysis.txt";
        const long HideTransitionMs = 750;

        readonly VisualElement _backArea;
        readonly VisualElement _informationsGroup;
        readonly VisualElement _dtailGroup;
        readonly AttachPanel _attachPanel;
        readonly ImagePreview _imagePreview;
        readonly SBM_Button _buttonClose;
        readonly SBM_Button _buttonBooth;

        readonly Label _price;
        readonly Label _name;
        readonly Label _description;
        readonly Label _categoryLabel;
        readonly VisualElement _prContents;
        readonly VisualElement _prImages;
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
        readonly VisualElement _platformInfo;
        readonly VisualElement _additionalInfoGroup;
        readonly VisualElement _additionalInfomations;
        readonly VisualElement _howToGroup;
        readonly VisualElement _howToSetupInfomations;
        readonly Foldout _pastVersionFoldout;
        readonly VisualElement _pastVersionFoldoutParent;
        int _pastVersionFoldoutIndex = -1;
        readonly VisualElement _pastUpdateInfomations;
        readonly VisualElement _latestUpdateInfomations;
        readonly VisualElement _updateInfoGroup;
        readonly VisualElement _analysisInfoGroup;
        readonly VisualElement _analysisInfo;
        readonly Label _analysisInfoText;
        readonly VisualElement _licenseGroup;
        readonly SBM_Button _buttonLicense;
        readonly VisualElement _relatedItems;
        readonly VisualElement _relatedItemsContainer;

        IVisualElementScheduledItem _pending;
        string _boothUrl = string.Empty;
        string _youtubeUrl = string.Empty;
        bool _hasPrImages;

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
            _imagePreview = this.Q<ImagePreview>("ImagePreview");
            _buttonClose = this.Q<SBM_Button>("ButtonClose");
            _buttonBooth = this.Q<SBM_Button>("ButtonBooth");

            _price = this.Q<Label>("Price");
            _name = this.Q<Label>("Name");
            _description = this.Q<Label>("Discription");
            _categoryLabel = this.Q<Label>("CategoryLabel");
            SamirinBoothCategoryUtil.SetupLabel(_categoryLabel);
            _imageContents = this.Q<VisualElement>("ImageContents");
            _prImages = this.Q<VisualElement>("PRImages");
            _prContents = this.Q<VisualElement>("PRContents")
                ?? _prImages?.parent;
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
            _platformInfo = this.Q<VisualElement>("PlatformInfo");
            _additionalInfomations = this.Q<VisualElement>("AdditionalInfomations");
            _additionalInfoGroup = this.Q<VisualElement>("AdditionalInfoGroup")
                ?? _additionalInfomations?.parent;
            _howToSetupInfomations = this.Q<VisualElement>("HowToSetupInfomations");
            _howToGroup = this.Q<VisualElement>("HowToGroup")
                ?? _howToSetupInfomations?.parent;
            _pastVersionFoldout = this.Q<Foldout>("PastVarsion");
            _pastVersionFoldoutParent = _pastVersionFoldout?.parent;
            _pastVersionFoldoutIndex = _pastVersionFoldoutParent != null && _pastVersionFoldout != null
                ? _pastVersionFoldoutParent.IndexOf(_pastVersionFoldout)
                : -1;
            _pastUpdateInfomations = this.Q<VisualElement>("PastUpdateInfomations");
            _latestUpdateInfomations = this.Q<VisualElement>("LatestUpdateInfomations");
            _updateInfoGroup = this.Q<VisualElement>("UpdateInfoGroup")
                ?? _latestUpdateInfomations?.parent;
            _analysisInfoGroup = this.Q<VisualElement>("AnalysisInfoGroup");
            _analysisInfo = this.Q<VisualElement>("AnalysisInfo");
            _analysisInfoText = this.Q<Label>("AnalysisInfoText")
                ?? _analysisInfo?.Q<Label>("AnalysisInfoText");
            _licenseGroup = this.Q<VisualElement>("LicenseGroup");
            _buttonLicense = this.Q<SBM_Button>("ButtonLicense")
                ?? _licenseGroup?.Q<SBM_Button>("ButtonLicense");
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

            if (_buttonLicense != null)
                _buttonLicense.clicked += OnLicenseClicked;

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
            _imagePreview?.Hide();
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
            BindPlatformInfo(info.platformInfo);
            BindAdditionalInfos(info.additionalInfos);
            BindHowToSetupInfos(info.howToSetupInfos);
            BindUpdateInfos(info.updateInfos);
            BindAnalysisInfo(info);
            BindLicenseGroup(info);
            BindRelatedAssets(info.relatedAssets);

            _attachPanel?.Bind(info, isImported);
        }

        const float DetailImageHeight = 250f;

        void BindImages(Sprite[] images)
        {
            if (_imageContents == null && _prContents == null && _prImages == null)
                return;

            _imageContents?.Clear();
            _imagePreview?.Hide();

            var backgrounds = new List<Background>();
            if (images != null && _imageContents != null)
            {
                for (int i = 0; i < images.Length; i++)
                {
                    if (images[i] == null)
                        continue;

                    var background = Background.FromSprite(images[i]);
                    var index = backgrounds.Count;
                    backgrounds.Add(background);
                    _imageContents.Add(CreateDetailImage(background, GetSpriteAspectRatio(images[i]), index));
                }
            }

            _hasPrImages = backgrounds.Count > 0;

            // 実画像がなければフォールバックのみの表示はせず、PRImages を隠す
            SetDisplay(_prImages, _hasPrImages);
            if (_hasPrImages)
                _imagePreview?.SetImages(backgrounds);
            else
                _imagePreview?.SetImages(null);

            UpdatePrContentsVisibility();
        }

        VisualElement CreateDetailImage(Background background, float aspectRatio, int index)
        {
            var image = new VisualElement { name = "Image" };
            image.AddToClassList("DtailImages");
            image.style.height = DetailImageHeight;
            image.style.width = DetailImageHeight * Mathf.Max(0.01f, aspectRatio);
            image.style.flexShrink = 0;
            image.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            image.style.backgroundImage = background;
            image.RegisterCallback<ClickEvent>(evt => OnDetailImageClicked(index, evt));
            return image;
        }

        void OnDetailImageClicked(int index, ClickEvent evt)
        {
            _imagePreview?.Show(index);
            evt.StopPropagation();
        }

        static float GetSpriteAspectRatio(Sprite sprite)
        {
            if (sprite == null || sprite.rect.height <= 0f)
                return 1f;
            return sprite.rect.width / sprite.rect.height;
        }

        void BindYoutube(string url)
        {
            var hasUrl = !string.IsNullOrWhiteSpace(url);
            SetDisplay(_prMovie, hasUrl);
            if (_youtubeLink != null)
                _youtubeLink.text = hasUrl ? url : string.Empty;

            UpdatePrContentsVisibility();
        }

        void UpdatePrContentsVisibility()
        {
            var hasMovie = !string.IsNullOrWhiteSpace(_youtubeUrl);
            // フォールバック画像のみ（実画像なし）かつ動画もなければ PRContents ごと非表示
            SetDisplay(_prContents, _hasPrImages || hasMovie);
        }

        void BindPlatformInfo(PlatformInfo platformInfo)
        {
            if (_platformInfo == null)
                return;

            var info = platformInfo ?? new PlatformInfo();
            SetPlatformSupport(_platformInfo.Q("PC_VR"), info.forPCVR);
            SetPlatformSupport(_platformInfo.Q("PC_Desktop"), info.forPCDesktop);
            SetPlatformSupport(_platformInfo.Q("Quest"), info.forQuest);
            SetPlatformSupport(_platformInfo.Q("Android_iOS"), info.forAndroid_iOS);
        }

        static void SetPlatformSupport(VisualElement platformRoot, bool isSupport)
        {
            if (platformRoot == null)
                return;

            var icon = platformRoot.Q("isSupport");
            if (icon == null)
                return;

            var texture = LoadSupportTexture(isSupport);
            if (texture != null)
                icon.style.backgroundImage = Background.FromTexture2D(texture);
        }

        static Texture2D LoadSupportTexture(bool isSupport)
        {
            var guid = isSupport ? SupportYesGuid : SupportNoGuid;
            var path = AssetDatabase.GUIDToAssetPath(guid);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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
            if (_additionalInfomations == null && _additionalInfoGroup == null)
                return;

            _additionalInfomations?.Clear();

            var added = BindAdditionalInfoElements(_additionalInfomations, infos);
            SetDisplay(_additionalInfoGroup, added > 0);
            if (_additionalInfomations != null && _additionalInfoGroup != null)
                SetDisplay(_additionalInfomations, added > 0);
        }

        void BindHowToSetupInfos(global::AdditionalInfo[] infos)
        {
            if (_howToSetupInfomations == null && _howToGroup == null)
                return;

            _howToSetupInfomations?.Clear();

            var added = BindAdditionalInfoElements(_howToSetupInfomations, infos);
            SetDisplay(_howToGroup, added > 0);
            if (_howToSetupInfomations != null && _howToGroup != null)
                SetDisplay(_howToSetupInfomations, added > 0);
        }

        int BindAdditionalInfoElements(VisualElement container, global::AdditionalInfo[] infos)
        {
            if (container == null || infos == null || infos.Length == 0)
                return 0;

            var added = 0;
            for (int i = 0; i < infos.Length; i++)
            {
                if (!HasAdditionalContent(infos[i]))
                    continue;

                container.Add(CreateAdditionalInfoElement(infos[i]));
                added++;
            }

            return added;
        }

        static bool HasAdditionalContent(global::AdditionalInfo info)
        {
            if (info == null)
                return false;

            if (!string.IsNullOrWhiteSpace(info.title))
                return true;
            if (!string.IsNullOrWhiteSpace(info.description))
                return true;
            if (info.image != null)
                return true;

            if (info.paths == null || info.paths.Length == 0)
                return false;

            for (int i = 0; i < info.paths.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(info.paths[i]))
                    return true;
            }

            return false;
        }

        AdditionalInfo CreateAdditionalInfoElement(global::AdditionalInfo info)
        {
            var element = new AdditionalInfo();
            element.Bind(info, BoundInfo);
            element.ImageClicked += OnAdditionalInfoImageClicked;
            return element;
        }

        void OnAdditionalInfoImageClicked(Background background)
        {
            _imagePreview?.Show(background);
        }

        void BindUpdateInfos(global::UpdateInfo[] infos)
        {
            if (_latestUpdateInfomations == null && _updateInfoGroup == null)
                return;

            _latestUpdateInfomations?.Clear();
            _pastUpdateInfomations?.Clear();

            var valid = CollectValidUpdateInfos(infos);
            if (valid.Count == 0)
            {
                SetPastVersionFoldoutVisible(false);
                SetPastUpdateInfomationsVisible(false);
                SetDisplay(_updateInfoGroup, false);
                return;
            }

            SetDisplay(_updateInfoGroup, true);
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
                if (HasUpdateContent(infos[i]))
                    list.Add(infos[i]);
            }

            return list;
        }

        static bool HasUpdateContent(global::UpdateInfo info)
        {
            if (info == null)
                return false;

            if (!string.IsNullOrWhiteSpace(info.updateName))
                return true;
            if (!string.IsNullOrWhiteSpace(info.updateDescription))
                return true;

            return info.updateDate != null && info.updateDate.year > 0;
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

        void BindAnalysisInfo(SamirinBoothAssetInfo info)
        {
            if (_analysisInfoGroup == null && _analysisInfo == null)
                return;

            var text = LoadItemAnalysisText(info);
            var displayText = ExtractMetricsSection(text);
            var hasText = !string.IsNullOrWhiteSpace(displayText);

            if (_analysisInfoText != null)
                _analysisInfoText.text = hasText ? displayText : string.Empty;

            SetDisplay(_analysisInfoGroup ?? _analysisInfo, hasText);
            if (_analysisInfo != null && _analysisInfoGroup != null)
                SetDisplay(_analysisInfo, hasText);
        }

        /// <summary>
        /// 「ポリゴン数:」以降のメトリクス部分だけを抜き出します。
        /// </summary>
        static string ExtractMetricsSection(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            const string marker = "ポリゴン数:";
            var index = text.IndexOf(marker, StringComparison.Ordinal);
            if (index < 0)
                return null;

            return text.Substring(index).Trim();
        }

        static string LoadItemAnalysisText(SamirinBoothAssetInfo info)
        {
            if (info == null)
                return null;

            foreach (var assetPath in EnumerateItemAnalysisAssetPaths(info))
            {
                if (string.IsNullOrEmpty(assetPath))
                    continue;

                var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
                if (textAsset != null && !string.IsNullOrWhiteSpace(textAsset.text))
                    return textAsset.text;

                // TextAsset として未インポートでもファイルがあれば読む
                var absolute = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(Application.dataPath, "..", assetPath));
                if (System.IO.File.Exists(absolute))
                {
                    try
                    {
                        var fileText = System.IO.File.ReadAllText(absolute);
                        if (!string.IsNullOrWhiteSpace(fileText))
                            return fileText;
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }

            return null;
        }

        static IEnumerable<string> EnumerateItemAnalysisAssetPaths(SamirinBoothAssetInfo info)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string TryAdd(string folder)
            {
                if (string.IsNullOrWhiteSpace(folder))
                    return null;
                var normalized = folder.Replace("\\", "/").TrimEnd('/');
                var path = normalized + "/" + ItemAnalysisFileName;
                return seen.Add(path) ? path : null;
            }

            // Info アセットと同じフォルダ（例: .../SamirinBoothInformation/DeskPen/ItemAnalysis.txt）
            var infoPath = AssetDatabase.GetAssetPath(info)?.Replace("\\", "/");
            if (!string.IsNullOrEmpty(infoPath))
            {
                var parent = System.IO.Path.GetDirectoryName(infoPath)?.Replace("\\", "/");
                var path = TryAdd(parent);
                if (path != null)
                    yield return path;
            }

            // folderName から Booth Information 配下を解決
            if (!string.IsNullOrWhiteSpace(info.folderName))
            {
                var path = TryAdd(InformationFolder + "/" + info.folderName.Trim());
                if (path != null)
                    yield return path;
            }
        }

        void BindRelatedAssets(SamirinBoothAssetInfo[] related)
        {
            if (_relatedItemsContainer == null && _relatedItems == null)
                return;

            _relatedItemsContainer?.Clear();

            if (related == null || related.Length == 0)
            {
                SetDisplay(_relatedItems, false);
                return;
            }

            var avatar = SBM_Header.CurrentAvatarDescriptor;
            var added = 0;

            for (int i = 0; i < related.Length; i++)
            {
                if (related[i] == null)
                    continue;

                var element = new AssetElement();
                element.AddToClassList("SBM_AssetElement");
                element.Bind(related[i]);
                element.RefreshAttached(avatar);
                element.clicked += OnRelatedAssetClicked;
                _relatedItemsContainer?.Add(element);
                added++;
            }

            SetDisplay(_relatedItems, added > 0);
        }

        void BindLicenseGroup(SamirinBoothAssetInfo info)
        {
            if (_licenseGroup == null && _buttonLicense == null)
                return;

            var hasLicense = SamirinBoothLicenseUtil.TryFindLicensePath(info, out _);
            SetDisplay(_licenseGroup, hasLicense);
            if (_buttonLicense != null)
                SetDisplay(_buttonLicense, hasLicense);
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

        void OnLicenseClicked()
        {
            if (BoundInfo == null)
                return;
            SBM_LicenseViewer.ShowForAsset(BoundInfo);
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

        static string FormatDate(SamirinBoothDate date)
        {
            if (date == null || date.year <= 0)
                return "----/--/--";
            return $"{date.year:0000}/{date.month:00}/{date.day:00}";
        }

        static string FormatVersion(Version version)
        {
            return $"ver{version.Major}.{version.Minor}.{version.Build}";
        }
    }
}
