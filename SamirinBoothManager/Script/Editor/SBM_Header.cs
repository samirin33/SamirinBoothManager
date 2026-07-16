using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;

namespace samirin33.SamirinBoothManager.UI.Parts
{
    /// <summary>
    /// ヘッダー。シーン上の有効な VRCAvatarDescriptor をドロップダウンで選択する。
    /// </summary>
    public class SBM_Header : SBM_UxmlPartElement
    {
        public new class UxmlFactory : UxmlFactory<SBM_Header, UxmlTraits> { }
        public new class UxmlTraits : VisualElement.UxmlTraits { }

        const string NoneLabel = "None";
        const string BoothUrl = "https://samirin33-vrc.booth.pm/";

        public static event Action<VRCAvatarDescriptor> AvatarDescriptorChanged;

        public static VRCAvatarDescriptor CurrentAvatarDescriptor { get; private set; }

        readonly DropdownField _avatarDropdown;
        readonly VisualElement _logoArea;
        readonly SBM_Button _boothButton;
        readonly List<VRCAvatarDescriptor> _avatars = new List<VRCAvatarDescriptor>();
        readonly List<string> _choices = new List<string>();

        bool _suppressNotify;
        bool _hierarchyHooked;

        public SBM_Header() : base(nameof(SBM_Header))
        {
            _avatarDropdown = this.Q<DropdownField>("AvatarDescriptor");
            _logoArea = this.Q<VisualElement>("LogoArea");
            _boothButton = this.Q<SBM_Button>("Button_Booth");

            if (_logoArea != null)
            {
                _logoArea.pickingMode = PickingMode.Position;
                _logoArea.RegisterCallback<ClickEvent>(OnBoothClicked);
            }

            if (_boothButton != null)
                _boothButton.clicked += OpenBoothPage;

            if (_avatarDropdown != null)
            {
                _avatarDropdown.RegisterValueChangedCallback(OnAvatarDropdownChanged);
                // 開く直前に最新のシーン状態へ更新
                _avatarDropdown.RegisterCallback<PointerDownEvent>(
                    _ => RefreshAvatarChoices(keepSelection: true),
                    TrickleDown.TrickleDown);
            }

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        void OnBoothClicked(ClickEvent evt)
        {
            OpenBoothPage();
            evt.StopPropagation();
        }

        static void OpenBoothPage()
        {
            Application.OpenURL(BoothUrl);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            if (!_hierarchyHooked)
            {
                EditorApplication.hierarchyChanged += OnHierarchyChanged;
                _hierarchyHooked = true;
            }

            RefreshAvatarChoices(keepSelection: false);
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            if (!_hierarchyHooked)
                return;

            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            _hierarchyHooked = false;
        }

        void OnHierarchyChanged()
        {
            if (panel == null)
                return;

            RefreshAvatarChoices(keepSelection: true);
        }

        void OnAvatarDropdownChanged(ChangeEvent<string> evt)
        {
            if (_suppressNotify)
                return;

            NotifyAvatarChanged(ResolveSelectedAvatar());
        }

        void RefreshAvatarChoices(bool keepSelection)
        {
            if (_avatarDropdown == null)
                return;

            var previous = keepSelection ? CurrentAvatarDescriptor : null;
            CollectEnabledAvatars(_avatars);
            BuildChoices(_avatars, _choices);

            var nextIndex = 0;
            if (previous != null)
            {
                var found = _avatars.IndexOf(previous);
                if (found >= 0)
                    nextIndex = found + 1; // +1 = None の分
            }
            else if (!keepSelection && _avatars.Count > 0)
            {
                // 既定: Hierarchy 上で最も上の有効アバター
                nextIndex = 1;
            }

            _suppressNotify = true;
            try
            {
                _avatarDropdown.choices = new List<string>(_choices);
                if (nextIndex >= 0 && nextIndex < _choices.Count)
                    _avatarDropdown.SetValueWithoutNotify(_choices[nextIndex]);
                else
                    _avatarDropdown.SetValueWithoutNotify(NoneLabel);
            }
            finally
            {
                _suppressNotify = false;
            }

            var selected = ResolveSelectedAvatar();
            if (selected != CurrentAvatarDescriptor)
                NotifyAvatarChanged(selected);
        }

        VRCAvatarDescriptor ResolveSelectedAvatar()
        {
            if (_avatarDropdown == null)
                return null;

            var index = _choices.IndexOf(_avatarDropdown.value);
            if (index <= 0 || index > _avatars.Count)
                return null;

            return _avatars[index - 1];
        }

        static void BuildChoices(List<VRCAvatarDescriptor> avatars, List<string> choices)
        {
            choices.Clear();
            choices.Add(NoneLabel);

            var nameCounts = new Dictionary<string, int>();
            for (int i = 0; i < avatars.Count; i++)
            {
                var name = avatars[i] != null ? avatars[i].gameObject.name : "Missing";
                nameCounts.TryGetValue(name, out var count);
                nameCounts[name] = count + 1;
            }

            var nameSeen = new Dictionary<string, int>();
            for (int i = 0; i < avatars.Count; i++)
            {
                var avatar = avatars[i];
                if (avatar == null)
                {
                    choices.Add($"Missing ({i})");
                    continue;
                }

                var name = avatar.gameObject.name;
                if (nameCounts[name] <= 1)
                {
                    choices.Add(name);
                    continue;
                }

                nameSeen.TryGetValue(name, out var seen);
                nameSeen[name] = seen + 1;
                choices.Add($"{name} ({GetHierarchyPath(avatar.transform)})");
            }
        }

        static string GetHierarchyPath(Transform transform)
        {
            if (transform.parent == null)
                return transform.name;

            var path = transform.name;
            var current = transform.parent;
            while (current != null && current.parent != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        static void CollectEnabledAvatars(List<VRCAvatarDescriptor> results)
        {
            results.Clear();

            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                var roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                    CollectEnabledAvatarsDepthFirst(roots[i].transform, results);
            }
        }

        static void CollectEnabledAvatarsDepthFirst(Transform root, List<VRCAvatarDescriptor> results)
        {
            if (root == null)
                return;

            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            if (descriptor != null
                && descriptor.enabled
                && root.gameObject.activeInHierarchy)
                results.Add(descriptor);

            for (int i = 0; i < root.childCount; i++)
                CollectEnabledAvatarsDepthFirst(root.GetChild(i), results);
        }

        /// <summary>
        /// ヒエラルキー上で最も上（先に出現する）有効な VRCAvatarDescriptor を返す。
        /// </summary>
        public static VRCAvatarDescriptor FindTopmostEnabledAvatarDescriptor()
        {
            var list = new List<VRCAvatarDescriptor>();
            CollectEnabledAvatars(list);
            return list.Count > 0 ? list[0] : null;
        }

        static void NotifyAvatarChanged(VRCAvatarDescriptor descriptor)
        {
            CurrentAvatarDescriptor = descriptor;
            AvatarDescriptorChanged?.Invoke(descriptor);
        }

        /// <summary>
        /// アバター配下（自身除く）に指定 prefabPath のプレハブ実体が含まれるか。
        /// </summary>
        public static bool AvatarContainsPrefab(VRCAvatarDescriptor avatar, string prefabPath)
        {
            return FindPrefabInstance(avatar, prefabPath) != null;
        }

        /// <summary>
        /// アバター配下の、指定 prefabPath に対応するプレハブインスタンスのルートを返す。
        /// </summary>
        public static GameObject FindPrefabInstance(VRCAvatarDescriptor avatar, string prefabPath)
        {
            if (avatar == null || string.IsNullOrEmpty(prefabPath))
                return null;

            var normalized = prefabPath.Replace('\\', '/');
            var transforms = avatar.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == avatar.transform)
                    continue;

                var nearestPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
                if (!string.IsNullOrEmpty(nearestPath)
                    && string.Equals(nearestPath.Replace('\\', '/'), normalized, StringComparison.OrdinalIgnoreCase))
                {
                    var root = PrefabUtility.GetNearestPrefabInstanceRoot(t.gameObject);
                    return root != null ? root : t.gameObject;
                }

                var source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                if (source == null)
                    continue;

                var sourcePath = AssetDatabase.GetAssetPath(source);
                if (!string.IsNullOrEmpty(sourcePath)
                    && string.Equals(sourcePath.Replace('\\', '/'), normalized, StringComparison.OrdinalIgnoreCase))
                {
                    var root = PrefabUtility.GetNearestPrefabInstanceRoot(t.gameObject);
                    return root != null ? root : t.gameObject;
                }
            }

            return null;
        }

        /// <summary>
        /// アバター直下に prefabPath のプレハブを追加する。
        /// </summary>
        public static GameObject AttachPrefabToAvatar(VRCAvatarDescriptor avatar, string prefabPath)
        {
            if (avatar == null || string.IsNullOrEmpty(prefabPath))
                return null;

            if (AvatarContainsPrefab(avatar, prefabPath))
                return FindPrefabInstance(avatar, prefabPath);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[SBM] Prefab not found: {prefabPath}");
                return null;
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab, avatar.transform) as GameObject;
            if (instance == null)
                return null;

            Undo.RegisterCreatedObjectUndo(instance, "Attach Prefab");
            return instance;
        }

        /// <summary>
        /// アバター配下から prefabPath のプレハブインスタンスを削除する。
        /// </summary>
        public static bool DetachPrefabFromAvatar(VRCAvatarDescriptor avatar, string prefabPath)
        {
            var instance = FindPrefabInstance(avatar, prefabPath);
            if (instance == null)
                return false;

            Undo.DestroyObjectImmediate(instance);
            return true;
        }

        /// <summary>
        /// アバター上の oldPrefabPath インスタンスを、newPrefabPath のプレハブで置き換える。
        /// old が無い場合は new を追加するだけ。
        /// </summary>
        public static GameObject ReplacePrefabOnAvatar(
            VRCAvatarDescriptor avatar,
            string oldPrefabPath,
            string newPrefabPath)
        {
            if (avatar == null || string.IsNullOrEmpty(newPrefabPath))
                return null;

            var siblingIndex = -1;
            var oldInstance = FindPrefabInstance(avatar, oldPrefabPath);
            if (oldInstance != null)
            {
                siblingIndex = oldInstance.transform.GetSiblingIndex();
                Undo.DestroyObjectImmediate(oldInstance);
            }

            // 既に同じものが付いている場合はそのまま
            var existing = FindPrefabInstance(avatar, newPrefabPath);
            if (existing != null)
                return existing;

            var instance = AttachPrefabToAvatar(avatar, newPrefabPath);
            if (instance != null && siblingIndex >= 0)
                instance.transform.SetSiblingIndex(siblingIndex);

            return instance;
        }
    }
}
