using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Ursa;
using Ursa.Transitions;
using Ursa.UI;

namespace Ursa.Editor
{
    /// <summary>
    /// スペードトランジションのアニメーション・コントローラー・プレハブを生成するエディターツール。
    /// 実行後は UrsaSettings に自動登録されます。
    /// </summary>
    public static class SpadeTransitionSetup
    {
        private const string AnimFolder  = "Assets/Ursa/Animations/Transitions";
        private const string PrefabFolder = "Assets/Ursa/Prefabs/Transitions";
        private const string SpritePath  = "Assets/Ursa/Textures/Transitions/spade_1024.png";
        private const string PrefabName  = "SpadeTransitionEffect";

        [MenuItem("Ursa/Setup Spade Transition")]
        public static void Setup()
        {
            EnsureFolder("Assets/Ursa",           "Animations");
            EnsureFolder("Assets/Ursa/Animations","Transitions");
            EnsureFolder("Assets/Ursa",           "Prefabs");
            EnsureFolder("Assets/Ursa/Prefabs",   "Transitions");

            // spade_1024.png をスプライトとして取得
            var importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[Ursa] Spade texture not found: {SpritePath}");
                return;
            }
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.SaveAndReimport();
            }
            var spadeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);

            // 既存アセットを削除して再生成できるようにする
            var overwriteTargets = new[]
            {
                $"{AnimFolder}/{PrefabName}_Idle.anim",
                $"{AnimFolder}/{PrefabName}_Out.anim",
                $"{AnimFolder}/{PrefabName}_In.anim",
                $"{AnimFolder}/{PrefabName}.controller",
                $"{PrefabFolder}/{PrefabName}.prefab",
            };
            foreach (var path in overwriteTargets)
            {
                if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                    AssetDatabase.DeleteAsset(path);
            }

            // ---- Idle clip（スケール 0 で待機、背景 alpha=0）----
            var idleClip = new AnimationClip { name = $"{PrefabName}_Idle" };
            idleClip.wrapMode = WrapMode.Loop;
            idleClip.SetCurve("", typeof(CanvasGroup), "m_Alpha",
                new AnimationCurve(new Keyframe(0f, 0f)));
            idleClip.SetCurve("Spade", typeof(Transform), "m_LocalScale.x",
                new AnimationCurve(new Keyframe(0f, 0f)));
            idleClip.SetCurve("Spade", typeof(Transform), "m_LocalScale.y",
                new AnimationCurve(new Keyframe(0f, 0f)));
            idleClip.SetCurve("Spade", typeof(Transform), "m_LocalScale.z",
                new AnimationCurve(new Keyframe(0f, 1f)));
            AssetDatabase.CreateAsset(idleClip, $"{AnimFolder}/{PrefabName}_Idle.anim");

            // ---- Out clip（スペードが 0→15 に拡大しながら黒背景が出現, 0.5s）----
            var outClip = new AnimationClip { name = $"{PrefabName}_Out" };
            outClip.SetCurve("", typeof(CanvasGroup), "m_Alpha",
                AnimationCurve.EaseInOut(0f, 0f, 2.0f, 1f));
            outClip.SetCurve("Spade", typeof(Transform), "m_LocalScale.x",
                AnimationCurve.EaseInOut(0f, 0f, 2.0f, 15f));
            outClip.SetCurve("Spade", typeof(Transform), "m_LocalScale.y",
                AnimationCurve.EaseInOut(0f, 0f, 2.0f, 15f));
            outClip.SetCurve("Spade", typeof(Transform), "m_LocalScale.z",
                new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(2.0f, 1f)));
            AssetDatabase.CreateAsset(outClip, $"{AnimFolder}/{PrefabName}_Out.anim");

            // ---- In clip（スペードが 15→0 に縮小しながら黒背景が消える, 0.5s）----
            var inClip = new AnimationClip { name = $"{PrefabName}_In" };
            inClip.SetCurve("", typeof(CanvasGroup), "m_Alpha",
                AnimationCurve.EaseInOut(0f, 1f, 2.0f, 0f));
            inClip.SetCurve("Spade", typeof(Transform), "m_LocalScale.x",
                AnimationCurve.EaseInOut(0f, 15f, 2.0f, 0f));
            inClip.SetCurve("Spade", typeof(Transform), "m_LocalScale.y",
                AnimationCurve.EaseInOut(0f, 15f, 2.0f, 0f));
            inClip.SetCurve("Spade", typeof(Transform), "m_LocalScale.z",
                new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(2.0f, 1f)));
            AssetDatabase.CreateAsset(inClip, $"{AnimFolder}/{PrefabName}_In.anim");

            AssetDatabase.SaveAssets();

            // ---- AnimatorController ----
            var controller = AnimatorController.CreateAnimatorControllerAtPath(
                $"{AnimFolder}/{PrefabName}.controller");
            var sm = controller.layers[0].stateMachine;

            var idleState = sm.AddState("Idle");
            idleState.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                $"{AnimFolder}/{PrefabName}_Idle.anim");
            sm.defaultState = idleState;

            var outState = sm.AddState("Out");
            outState.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                $"{AnimFolder}/{PrefabName}_Out.anim");

            var inState = sm.AddState("In");
            inState.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                $"{AnimFolder}/{PrefabName}_In.anim");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            // ---- Prefab 構築 ----
            var go = new GameObject(PrefabName);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.sortingOrder = UrsaUIRenderOrder.Transition;
            canvas.planeDistance = 1f;
            go.AddComponent<UnityEngine.UI.CanvasScaler>();
            go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            go.GetComponent<RectTransform>().localScale = Vector3.one;

            var cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;

            var animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            // 全画面黒背景
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(go.transform, false);
            var bgImage = bgGO.AddComponent<UnityEngine.UI.Image>();
            bgImage.color = Color.black;
            var bgRect = bgGO.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;

            // スペード画像（画面中央・スケールアニメーション対象）
            var spadeGO = new GameObject("Spade");
            spadeGO.transform.SetParent(go.transform, false);
            var spadeImage = spadeGO.AddComponent<UnityEngine.UI.Image>();
            spadeImage.sprite = spadeSprite;
            spadeImage.color = Color.white;
            spadeImage.preserveAspect = true;
            var spadeRect = spadeGO.GetComponent<RectTransform>();
            spadeRect.anchorMin = new Vector2(0.5f, 0.5f);
            spadeRect.anchorMax = new Vector2(0.5f, 0.5f);
            spadeRect.pivot     = new Vector2(0.5f, 0.5f);
            spadeRect.sizeDelta = new Vector2(200f, 200f);
            spadeRect.anchoredPosition = Vector2.zero;
            spadeGO.transform.localScale = Vector3.zero;

            var effect = go.AddComponent<AnimatorTransitionEffect>();
            var so = new SerializedObject(effect);
            so.FindProperty("_animator").objectReferenceValue = animator;
            so.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabFolder}/{PrefabName}.prefab");
            Object.DestroyImmediate(go);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // UrsaSettings に自動登録（SetupDefaultTransitions が次回アクセス時に検出する）
            var settings = UrsaSettings.Instance;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            Debug.Log($"<color=cyan>[Ursa]</color> Spade Transition created. UrsaSettings に自動登録されました。");
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
