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
    /// AvatarDiscriptor.uxml。シーン上の有効な VRCAvatarDescriptor をドロップダウンで選択する。
    /// </summary>
    public class AvatarDiscriptor : SBM_UxmlPartElement
    {
        public new class UxmlFactory : UxmlFactory<AvatarDiscriptor, UxmlTraits> { }
        public new class UxmlTraits : VisualElement.UxmlTraits { }

        const string NoneLabel = "None";

        public static event Action<VRCAvatarDescriptor> AvatarDescriptorChanged;

        public static VRCAvatarDescriptor CurrentAvatarDescriptor { get; private set; }

        readonly DropdownField _avatarDropdown;
        readonly List<VRCAvatarDescriptor> _avatars = new List<VRCAvatarDescriptor>();
        readonly List<string> _choices = new List<string>();

        bool _suppressNotify;
        bool _hierarchyHooked;

        public AvatarDiscriptor() : base(nameof(AvatarDiscriptor))
        {
            _avatarDropdown = this.Q<DropdownField>("AvatarDescriptorField");

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

            if (!HasAvatarSdkAssembly())
            {
                _avatars.Clear();
                _choices.Clear();
                _choices.Add(NoneLabel);
                _suppressNotify = true;
                try
                {
                    _avatarDropdown.choices = new List<string>(_choices);
                    _avatarDropdown.SetValueWithoutNotify(NoneLabel);
                    _avatarDropdown.SetEnabled(false);
                }
                finally
                {
                    _suppressNotify = false;
                }

                if (CurrentAvatarDescriptor != null)
                    NotifyAvatarChanged(null);
                return;
            }

            _avatarDropdown.SetEnabled(true);

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

            try
            {
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
            catch (Exception e)
            {
                Debug.LogWarning($"[SBM] Failed to collect avatars (Avatar SDK may be missing): {e.Message}");
                results.Clear();
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
        /// Avatar SDK アセンブリがロードされているか。無い場合はアバター選択を無効化する。
        /// </summary>
        static bool HasAvatarSdkAssembly()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                try
                {
                    if (assemblies[i].GetType("VRC.SDK3.Avatars.Components.VRCAvatarDescriptor", false) != null)
                        return true;
                }
                catch
                {
                    // ignore unloadable assemblies
                }
            }

            return false;
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
    }
}
