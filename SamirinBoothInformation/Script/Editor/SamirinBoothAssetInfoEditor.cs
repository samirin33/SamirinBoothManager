using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(SamirinBoothAssetInfo))]
public class SamirinBoothAssetInfoEditor : Editor
{
    SerializedProperty _name;
    SerializedProperty _description;
    SerializedProperty _images;
    SerializedProperty _category;
    SerializedProperty _majorVertion;
    SerializedProperty _minorVertion;
    SerializedProperty _patchVertion;
    SerializedProperty _releaseDate;
    SerializedProperty _updateDate;
    SerializedProperty _updateInfos;
    SerializedProperty _url;
    SerializedProperty _price;
    SerializedProperty _youtubeUrl;
    SerializedProperty _additionalInfos;
    SerializedProperty _variations;
    SerializedProperty _relatedAssets;
    SerializedProperty _folderName;

    ReorderableList _imagesList;
    ReorderableList _updateInfosList;
    ReorderableList _variationsList;
    ReorderableList _relatedAssetsList;
    ReorderableList _additionalInfosList;

    bool _foldBasic = true;
    bool _foldVersion = true;
    bool _foldDates = true;
    bool _foldImages = true;
    bool _foldUpdates = true;
    bool _foldAdditional = true;
    bool _foldVariations = true;
    bool _foldRelated = true;

    void OnEnable()
    {
        _name = serializedObject.FindProperty("name");
        _description = serializedObject.FindProperty("description");
        _images = serializedObject.FindProperty("images");
        _category = serializedObject.FindProperty("category");
        _majorVertion = serializedObject.FindProperty("majorVertion");
        _minorVertion = serializedObject.FindProperty("minorVertion");
        _patchVertion = serializedObject.FindProperty("patchVertion");
        _releaseDate = serializedObject.FindProperty("releaseDate");
        _updateDate = serializedObject.FindProperty("updateDate");
        _updateInfos = serializedObject.FindProperty("updateInfos");
        _url = serializedObject.FindProperty("url");
        _price = serializedObject.FindProperty("price");
        _youtubeUrl = serializedObject.FindProperty("youtubeUrl");
        _additionalInfos = serializedObject.FindProperty("additionalInfos");
        _variations = serializedObject.FindProperty("variations");
        _relatedAssets = serializedObject.FindProperty("relatedAssets");
        _folderName = serializedObject.FindProperty("folderName");

        _imagesList = CreateSpriteList(_images, "画像一覧");
        _updateInfosList = CreateUpdateInfoList(_updateInfos);
        _variationsList = CreateVariationList(_variations);
        _relatedAssetsList = CreateRelatedAssetsList(_relatedAssets);
        _additionalInfosList = CreateAdditionalInfoList(_additionalInfos);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawBasicSection();
        DrawVersionSection();
        DrawDateSection();
        DrawImagesSection();
        DrawUpdateInfosSection();
        DrawAdditionalInfosSection();
        DrawVariationsSection();
        DrawRelatedSection();

        serializedObject.ApplyModifiedProperties();
    }

    static readonly string[] CategoryDisplayNames =
    {
        "アバター",
        "アバターギミック",
        "衣装・アクセサリー",
        "ワールド",
        "ワールドギミック",
        "3Dモデル",
        "その他"
    };

    void DrawBasicSection()
    {
        _foldBasic = EditorGUILayout.BeginFoldoutHeaderGroup(_foldBasic, "基本情報");
        if (_foldBasic)
        {
            EditorGUILayout.PropertyField(_name, new GUIContent("アセット名"));
            EditorGUILayout.PropertyField(_folderName, new GUIContent("フォルダ名"));

            if (_category != null)
            {
                int index = Mathf.Clamp(_category.enumValueIndex, 0, CategoryDisplayNames.Length - 1);
                int next = EditorGUILayout.Popup("カテゴリ", index, CategoryDisplayNames);
                if (next != index)
                    _category.enumValueIndex = next;
            }

            EditorGUILayout.LabelField("説明");
            _description.stringValue = EditorGUILayout.TextArea(_description.stringValue, GUILayout.MinHeight(60));
            EditorGUILayout.PropertyField(_price, new GUIContent("価格"));
            DrawUrlField(_url, "Booth URL");
            DrawUrlField(_youtubeUrl, "YouTube URL");
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(4);
    }

    void DrawVersionSection()
    {
        _foldVersion = EditorGUILayout.BeginFoldoutHeaderGroup(_foldVersion, "バージョン");
        if (_foldVersion)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("現在", GUILayout.Width(40));
            EditorGUILayout.LabelField(
                $"{_majorVertion.intValue}.{_minorVertion.intValue}.{_patchVertion.intValue}",
                EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            string versionText = EditorGUILayout.DelayedTextField(
                "直接入力",
                $"{_majorVertion.intValue}.{_minorVertion.intValue}.{_patchVertion.intValue}");
            if (EditorGUI.EndChangeCheck())
                TryParseVersion(versionText);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Major", GUILayout.Width(42));
            _majorVertion.intValue = Mathf.Max(0, EditorGUILayout.IntField(_majorVertion.intValue));
            EditorGUILayout.LabelField("Minor", GUILayout.Width(42));
            _minorVertion.intValue = Mathf.Max(0, EditorGUILayout.IntField(_minorVertion.intValue));
            EditorGUILayout.LabelField("Patch", GUILayout.Width(42));
            _patchVertion.intValue = Mathf.Max(0, EditorGUILayout.IntField(_patchVertion.intValue));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Major +1"))
            {
                _majorVertion.intValue++;
                _minorVertion.intValue = 0;
                _patchVertion.intValue = 0;
                SetDateToToday(_updateDate);
            }
            if (GUILayout.Button("Minor +1"))
            {
                _minorVertion.intValue++;
                _patchVertion.intValue = 0;
                SetDateToToday(_updateDate);
            }
            if (GUILayout.Button("Patch +1"))
            {
                _patchVertion.intValue++;
                SetDateToToday(_updateDate);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox(
                "Major / Minor / Patch は直接入力できます。「直接入力」は 1.2.3 形式。+1 ボタンは更新日を今日にします。",
                MessageType.None);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(4);
    }

    void TryParseVersion(string versionText)
    {
        if (string.IsNullOrWhiteSpace(versionText))
            return;

        var parts = versionText.Trim().TrimStart('v', 'V').Split('.');
        if (parts.Length < 1)
            return;

        if (parts.Length >= 1 && int.TryParse(parts[0], out int major))
            _majorVertion.intValue = Mathf.Max(0, major);
        if (parts.Length >= 2 && int.TryParse(parts[1], out int minor))
            _minorVertion.intValue = Mathf.Max(0, minor);
        if (parts.Length >= 3 && int.TryParse(parts[2], out int patch))
            _patchVertion.intValue = Mathf.Max(0, patch);
    }

    void DrawDateSection()
    {
        _foldDates = EditorGUILayout.BeginFoldoutHeaderGroup(_foldDates, "日付");
        if (_foldDates)
        {
            DrawDateField(_releaseDate, "リリース日");
            DrawDateField(_updateDate, "更新日");
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(4);
    }

    void DrawImagesSection()
    {
        _foldImages = EditorGUILayout.BeginFoldoutHeaderGroup(_foldImages, $"画像 ({_images.arraySize})");
        if (_foldImages)
            _imagesList.DoLayoutList();
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(4);
    }

    void DrawUpdateInfosSection()
    {
        _foldUpdates = EditorGUILayout.BeginFoldoutHeaderGroup(_foldUpdates, $"更新履歴 ({_updateInfos.arraySize})");
        if (_foldUpdates)
        {
            if (GUILayout.Button("履歴を追加（今日の日付）"))
            {
                int index = _updateInfos.arraySize;
                _updateInfos.arraySize++;
                var element = _updateInfos.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("updateName").stringValue =
                    $"v{_majorVertion.intValue}.{_minorVertion.intValue}.{_patchVertion.intValue}";
                element.FindPropertyRelative("updateDescription").stringValue = string.Empty;
                SetDateToToday(element.FindPropertyRelative("updateDate"));
            }
            _updateInfosList.DoLayoutList();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(4);
    }

    void DrawAdditionalInfosSection()
    {
        _foldAdditional = EditorGUILayout.BeginFoldoutHeaderGroup(
            _foldAdditional, $"追加情報 ({_additionalInfos.arraySize})");
        if (_foldAdditional)
            _additionalInfosList.DoLayoutList();
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(4);
    }

    void DrawVariationsSection()
    {
        _foldVariations = EditorGUILayout.BeginFoldoutHeaderGroup(_foldVariations, $"バリエーション ({_variations.arraySize})");
        if (_foldVariations)
            _variationsList.DoLayoutList();
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(4);
    }

    void DrawRelatedSection()
    {
        _foldRelated = EditorGUILayout.BeginFoldoutHeaderGroup(_foldRelated, $"関連アセット ({_relatedAssets.arraySize})");
        if (_foldRelated)
            _relatedAssetsList.DoLayoutList();
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    void DrawUrlField(SerializedProperty property, string label)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(property, new GUIContent(label));
        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(property.stringValue)))
        {
            if (GUILayout.Button("開く", GUILayout.Width(40)))
                Application.OpenURL(property.stringValue);
        }
        EditorGUILayout.EndHorizontal();
    }

    void DrawDateField(SerializedProperty dateProp, string label)
    {
        var year = dateProp.FindPropertyRelative("year");
        var month = dateProp.FindPropertyRelative("month");
        var day = dateProp.FindPropertyRelative("day");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(label);
        year.intValue = EditorGUILayout.IntField(year.intValue, GUILayout.Width(50));
        EditorGUILayout.LabelField("/", GUILayout.Width(10));
        month.intValue = Mathf.Clamp(EditorGUILayout.IntField(month.intValue, GUILayout.Width(30)), 1, 12);
        EditorGUILayout.LabelField("/", GUILayout.Width(10));
        day.intValue = Mathf.Clamp(EditorGUILayout.IntField(day.intValue, GUILayout.Width(30)), 1, 31);

        if (GUILayout.Button("今日", GUILayout.Width(40)))
            SetDateToToday(dateProp);

        EditorGUILayout.EndHorizontal();
    }

    static void SetDateToToday(SerializedProperty dateProp)
    {
        var now = System.DateTime.Now;
        dateProp.FindPropertyRelative("year").intValue = now.Year;
        dateProp.FindPropertyRelative("month").intValue = now.Month;
        dateProp.FindPropertyRelative("day").intValue = now.Day;
    }

    ReorderableList CreateSpriteList(SerializedProperty property, string header)
    {
        var list = new ReorderableList(serializedObject, property, true, true, true, true);
        list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, header);
        list.elementHeight = 64f;
        list.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            var element = property.GetArrayElementAtIndex(index);
            var previewRect = new Rect(rect.x, rect.y + 2, 60, 60);
            var fieldRect = new Rect(rect.x + 68, rect.y + 20, rect.width - 68, EditorGUIUtility.singleLineHeight);

            var sprite = element.objectReferenceValue as Sprite;
            if (sprite != null && sprite.texture != null)
                GUI.DrawTexture(previewRect, AssetPreview.GetAssetPreview(sprite) ?? sprite.texture, ScaleMode.ScaleToFit);
            else
                EditorGUI.DrawRect(previewRect, new Color(0.2f, 0.2f, 0.2f, 0.3f));

            EditorGUI.PropertyField(fieldRect, element, GUIContent.none);
        };
        return list;
    }

    ReorderableList CreateUpdateInfoList(SerializedProperty property)
    {
        var list = new ReorderableList(serializedObject, property, true, true, true, true);
        list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "更新履歴一覧");
        list.elementHeightCallback = index =>
        {
            var element = property.GetArrayElementAtIndex(index);
            var desc = element.FindPropertyRelative("updateDescription");
            float descHeight = EditorStyles.textArea.CalcHeight(
                new GUIContent(desc.stringValue), EditorGUIUtility.currentViewWidth - 60);
            return EditorGUIUtility.singleLineHeight * 3 + Mathf.Max(descHeight, 40) + 16;
        };
        list.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            var element = property.GetArrayElementAtIndex(index);
            float y = rect.y + 2;
            float line = EditorGUIUtility.singleLineHeight;
            float width = rect.width;

            EditorGUI.PropertyField(
                new Rect(rect.x, y, width, line),
                element.FindPropertyRelative("updateName"),
                new GUIContent("タイトル"));
            y += line + 2;

            DrawInlineDate(new Rect(rect.x, y, width, line), element.FindPropertyRelative("updateDate"), "日付");
            y += line + 2;

            EditorGUI.LabelField(new Rect(rect.x, y, width, line), "内容");
            y += line;
            var desc = element.FindPropertyRelative("updateDescription");
            float descHeight = Mathf.Max(
                EditorStyles.textArea.CalcHeight(new GUIContent(desc.stringValue), width),
                40);
            desc.stringValue = EditorGUI.TextArea(new Rect(rect.x, y, width, descHeight), desc.stringValue);
        };
        return list;
    }

    ReorderableList CreateVariationList(SerializedProperty property)
    {
        var list = new ReorderableList(serializedObject, property, true, true, true, true);
        list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "バリエーション一覧");
        list.elementHeight = EditorGUIUtility.singleLineHeight * 4 + 14;
        list.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            var element = property.GetArrayElementAtIndex(index);
            float y = rect.y + 2;
            float line = EditorGUIUtility.singleLineHeight;
            float width = rect.width;

            EditorGUI.PropertyField(
                new Rect(rect.x, y, width, line),
                element.FindPropertyRelative("variationName"),
                new GUIContent("名前"));
            y += line + 2;

            EditorGUI.PropertyField(
                new Rect(rect.x, y, width, line),
                element.FindPropertyRelative("variationDescription"),
                new GUIContent("説明"));
            y += line + 2;

            var pathProp = element.FindPropertyRelative("prefabPath");
            var currentPrefab = string.IsNullOrEmpty(pathProp.stringValue)
                ? null
                : AssetDatabase.LoadAssetAtPath<GameObject>(pathProp.stringValue);

            EditorGUI.BeginChangeCheck();
            var newPrefab = (GameObject)EditorGUI.ObjectField(
                new Rect(rect.x, y, width, line),
                "Prefab",
                currentPrefab,
                typeof(GameObject),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                pathProp.stringValue = newPrefab != null
                    ? AssetDatabase.GetAssetPath(newPrefab)
                    : string.Empty;
            }

            y += line + 2;
            EditorGUI.BeginDisabledGroup(true);
            EditorGUI.TextField(new Rect(rect.x, y, width, line), "パス", pathProp.stringValue);
            EditorGUI.EndDisabledGroup();
        };
        return list;
    }

    ReorderableList CreateRelatedAssetsList(SerializedProperty property)
    {
        var list = new ReorderableList(serializedObject, property, true, true, true, true);
        list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "関連アセット一覧");
        list.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            var element = property.GetArrayElementAtIndex(index);
            rect.y += 2;
            rect.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(rect, element, GUIContent.none);
        };
        return list;
    }

    ReorderableList CreateAdditionalInfoList(SerializedProperty property)
    {
        const float previewSize = 56f;
        const float topPadding = 2f;
        const float bottomPadding = 4f;
        const float spacing = 2f;

        var list = new ReorderableList(serializedObject, property, true, true, true, true);
        list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "追加情報一覧");
        list.elementHeightCallback = index =>
        {
            var element = property.GetArrayElementAtIndex(index);
            float line = EditorGUIUtility.singleLineHeight;
            float width = EditorGUIUtility.currentViewWidth - 60f;
            var desc = element.FindPropertyRelative("description");
            var paths = element.FindPropertyRelative("paths");
            float descHeight = Mathf.Max(
                EditorStyles.textArea.CalcHeight(new GUIContent(desc.stringValue), width),
                40f);
            float pathsHeight = GetPathsEditorHeight(paths, line, spacing);

            return topPadding
                + line + spacing
                + line + spacing
                + line
                + descHeight + 4f
                + previewSize + spacing
                + pathsHeight
                + bottomPadding;
        };
        list.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            var element = property.GetArrayElementAtIndex(index);
            float y = rect.y + topPadding;
            float line = EditorGUIUtility.singleLineHeight;
            float width = rect.width;

            EditorGUI.LabelField(
                new Rect(rect.x, y, width, line),
                $"情報 {index + 1}",
                EditorStyles.boldLabel);
            y += line + spacing;

            EditorGUI.PropertyField(
                new Rect(rect.x, y, width, line),
                element.FindPropertyRelative("title"),
                new GUIContent("タイトル"));
            y += line + spacing;

            EditorGUI.LabelField(new Rect(rect.x, y, width, line), "説明");
            y += line;
            var desc = element.FindPropertyRelative("description");
            float descHeight = Mathf.Max(
                EditorStyles.textArea.CalcHeight(new GUIContent(desc.stringValue), width),
                40f);
            desc.stringValue = EditorGUI.TextArea(new Rect(rect.x, y, width, descHeight), desc.stringValue);
            y += descHeight + 4f;

            var imageProp = element.FindPropertyRelative("image");
            var pathsProp = element.FindPropertyRelative("paths");
            var previewRect = new Rect(rect.x, y, previewSize, previewSize);
            float fieldsX = rect.x + previewSize + 8f;
            float fieldsWidth = width - previewSize - 8f;

            var sprite = imageProp.objectReferenceValue as Sprite;
            if (sprite != null)
            {
                var preview = AssetPreview.GetAssetPreview(sprite);
                if (preview != null)
                    GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit);
            }
            else
            {
                EditorGUI.DrawRect(previewRect, new Color(0.2f, 0.2f, 0.2f, 0.3f));
            }

            float fieldY = y + (previewSize - line) * 0.5f;
            EditorGUI.PropertyField(
                new Rect(fieldsX, fieldY, fieldsWidth, line),
                imageProp,
                new GUIContent("画像"));
            y += previewSize + spacing;

            y = DrawPathsEditor(new Rect(rect.x, y, width, GetPathsEditorHeight(pathsProp, line, spacing)), pathsProp);
        };
        return list;
    }

    static float GetPathsEditorHeight(SerializedProperty pathsProp, float line, float spacing)
    {
        if (pathsProp == null)
            return line;

        // ヘッダー行 + 各パス行 + 「追加」ボタン
        int count = Mathf.Max(0, pathsProp.arraySize);
        return line + spacing
            + count * (line + spacing)
            + line;
    }

    static float DrawPathsEditor(Rect rect, SerializedProperty pathsProp)
    {
        float line = EditorGUIUtility.singleLineHeight;
        float spacing = 2f;
        float y = rect.y;
        float width = rect.width;

        EditorGUI.LabelField(
            new Rect(rect.x, y, width - 70f, line),
            $"パス ({pathsProp.arraySize})");

        if (GUI.Button(new Rect(rect.x + width - 66f, y, 66f, line), "追加"))
            pathsProp.arraySize++;
        y += line + spacing;

        for (int i = 0; i < pathsProp.arraySize; i++)
        {
            var pathElement = pathsProp.GetArrayElementAtIndex(i);
            float fieldWidth = width - 28f;

            EditorGUI.BeginChangeCheck();
            var next = EditorGUI.TextField(
                new Rect(rect.x, y, fieldWidth, line),
                pathElement.stringValue);
            if (EditorGUI.EndChangeCheck())
                pathElement.stringValue = next;

            if (GUI.Button(new Rect(rect.x + fieldWidth + 4f, y, 24f, line), "−"))
            {
                pathsProp.DeleteArrayElementAtIndex(i);
                break;
            }

            y += line + spacing;
        }

        return y;
    }

    void DrawInlineDate(Rect rect, SerializedProperty dateProp, string label)
    {
        float labelWidth = 40f;
        EditorGUI.LabelField(new Rect(rect.x, rect.y, labelWidth, rect.height), label);

        float x = rect.x + labelWidth;
        var year = dateProp.FindPropertyRelative("year");
        var month = dateProp.FindPropertyRelative("month");
        var day = dateProp.FindPropertyRelative("day");

        year.intValue = EditorGUI.IntField(new Rect(x, rect.y, 50, rect.height), year.intValue);
        x += 54;
        EditorGUI.LabelField(new Rect(x, rect.y, 10, rect.height), "/");
        x += 12;
        month.intValue = Mathf.Clamp(EditorGUI.IntField(new Rect(x, rect.y, 30, rect.height), month.intValue), 1, 12);
        x += 34;
        EditorGUI.LabelField(new Rect(x, rect.y, 10, rect.height), "/");
        x += 12;
        day.intValue = Mathf.Clamp(EditorGUI.IntField(new Rect(x, rect.y, 30, rect.height), day.intValue), 1, 31);
        x += 38;

        if (GUI.Button(new Rect(x, rect.y, 40, rect.height), "今日"))
            SetDateToToday(dateProp);
    }
}
