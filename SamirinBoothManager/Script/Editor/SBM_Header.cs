using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;

namespace samirin33.SamirinBoothManager.UI.Parts
{
    /// <summary>
    /// ヘッダー。Booth リンクと AvatarDiscriptor（アバター選択）を含む。
    /// </summary>
    public class SBM_Header : SBM_UxmlPartElement
    {
        public new class UxmlFactory : UxmlFactory<SBM_Header, UxmlTraits> { }
        public new class UxmlTraits : VisualElement.UxmlTraits { }

        const string BoothUrl = "https://samirin33-vrc.booth.pm/";
        const string XUrl = "https://x.com/samirin33_VRC";
        const string MainXUrl = "https://x.com/samirin33";

        /// <summary>互換用。実体は AvatarDiscriptor。</summary>
        public static event Action<VRCAvatarDescriptor> AvatarDescriptorChanged
        {
            add => AvatarDiscriptor.AvatarDescriptorChanged += value;
            remove => AvatarDiscriptor.AvatarDescriptorChanged -= value;
        }

        /// <summary>互換用。実体は AvatarDiscriptor。</summary>
        public static VRCAvatarDescriptor CurrentAvatarDescriptor =>
            AvatarDiscriptor.CurrentAvatarDescriptor;

        readonly VisualElement _headerBackGround;
        readonly VisualElement _logoArea;
        readonly SBM_Button _boothButton;
        readonly SBM_Button _xButton;

        /// <summary>
        /// 親 UXML で SBM_Header に置いた子要素は HeaderBackGround 配下に入る。
        /// </summary>
        public override VisualElement contentContainer =>
            _headerBackGround ?? this;

        public SBM_Header() : base(nameof(SBM_Header))
        {
            _headerBackGround = this.Q<VisualElement>("HeaderBackGround");
            _logoArea = this.Q<VisualElement>("LogoArea");
            _boothButton = this.Q<SBM_Button>("Button_Booth");
            _xButton = this.Q<SBM_Button>("Button_X");

            if (_logoArea != null)
            {
                _logoArea.pickingMode = PickingMode.Position;
                _logoArea.RegisterCallback<ClickEvent>(OnLogoClicked);
            }

            if (_boothButton != null)
                _boothButton.clicked += OpenBoothPage;

            if (_xButton != null)
                _xButton.clicked += OpenXPage;
        }

        void OnLogoClicked(ClickEvent evt)
        {
            OpenMainXPage();
            evt.StopPropagation();
        }

        static void OpenBoothPage()
        {
            Application.OpenURL(BoothUrl);
        }

        static void OpenXPage()
        {
            Application.OpenURL(XUrl);
        }

        static void OpenMainXPage()
        {
            Application.OpenURL(MainXUrl);
        }

        /// <summary>
        /// ヒエラルキー上で最も上（先に出現する）有効な VRCAvatarDescriptor を返す。
        /// </summary>
        public static VRCAvatarDescriptor FindTopmostEnabledAvatarDescriptor()
        {
            return AvatarDiscriptor.FindTopmostEnabledAvatarDescriptor();
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

        /// <summary>
        /// アクティブシーンに prefabPath のプレハブインスタンスがあるか。
        /// </summary>
        public static bool SceneContainsPrefab(string prefabPath)
        {
            return FindPrefabInstanceInScene(prefabPath) != null;
        }

        /// <summary>
        /// アクティブシーン内の、指定 prefabPath に対応するプレハブインスタンスを返す。
        /// </summary>
        public static GameObject FindPrefabInstanceInScene(string prefabPath)
        {
            if (string.IsNullOrEmpty(prefabPath))
                return null;

            var normalized = prefabPath.Replace('\\', '/');
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
                return null;

            var roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                var transforms = roots[r].GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    var go = transforms[i].gameObject;
                    var nearestPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                    if (!string.IsNullOrEmpty(nearestPath)
                        && string.Equals(nearestPath.Replace('\\', '/'), normalized, StringComparison.OrdinalIgnoreCase))
                    {
                        var root = PrefabUtility.GetNearestPrefabInstanceRoot(go);
                        return root != null ? root : go;
                    }

                    var source = PrefabUtility.GetCorrespondingObjectFromSource(go);
                    if (source == null)
                        continue;

                    var sourcePath = AssetDatabase.GetAssetPath(source);
                    if (!string.IsNullOrEmpty(sourcePath)
                        && string.Equals(sourcePath.Replace('\\', '/'), normalized, StringComparison.OrdinalIgnoreCase))
                    {
                        var root = PrefabUtility.GetNearestPrefabInstanceRoot(go);
                        return root != null ? root : go;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// アクティブシーンのルートに prefabPath のプレハブを配置する（複数配置可）。
        /// </summary>
        public static GameObject InstantiatePrefabInScene(string prefabPath)
        {
            if (string.IsNullOrEmpty(prefabPath))
                return null;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[SBM] Prefab not found: {prefabPath}");
                return null;
            }

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[SBM] Active scene is not available.");
                return null;
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
                return null;

            Undo.RegisterCreatedObjectUndo(instance, "Place Prefab In Scene");
            return instance;
        }

        /// <summary>
        /// シーン上の prefabPath インスタンスを削除する。
        /// </summary>
        public static bool DetachPrefabFromScene(string prefabPath)
        {
            var instance = FindPrefabInstanceInScene(prefabPath);
            if (instance == null)
                return false;

            Undo.DestroyObjectImmediate(instance);
            return true;
        }
    }
}
