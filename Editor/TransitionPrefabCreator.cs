#if URSA_DEVELOPER
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Ursa.Transitions;
using Ursa.UI;

namespace Ursa.Editor
{
    /// <summary>
    /// Ursa のトランジション用 Prefab を自動生成するエディターメニュー。
    /// </summary>
    public static class TransitionPrefabCreator
    {
        [MenuItem("Ursa/Create Transition Prefabs")]
        public static void CreateTransitionPrefabs()
        {
            const string prefabFolder = "Assets/Ursa/Prefabs/Transitions";
            const string matFolder    = "Assets/Ursa/Materials/Transitions";

            EnsureFolder("Assets/Ursa", "Prefabs");
            EnsureFolder("Assets/Ursa/Prefabs", "Transitions");
            EnsureFolder("Assets/Ursa", "Materials");
            EnsureFolder("Assets/Ursa/Materials", "Transitions");

            CreateFadePrefab(prefabFolder);
            CreateSpadeAnimatorPrefab(prefabFolder);
            CreateShaderEffectPrefab(prefabFolder, matFolder, "ShaderWipeTransitionEffect",     "Assets/Ursa/Shaders/WipeTransition.shader");
            CreateShaderEffectPrefab(prefabFolder, matFolder, "ShaderCircleTransitionEffect",   "Assets/Ursa/Shaders/CircleTransition.shader");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=cyan>[Ursa]</color> Transition Prefabs created in {prefabFolder}");
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
                AssetDatabase.CreateFolder(parent, child);
        }

        private static void CreateFadePrefab(string folder)
        {
            var go = new GameObject("FadeTransitionEffect");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.sortingOrder = UrsaUIRenderOrder.Transition;
            canvas.planeDistance = 1f;
            go.AddComponent<UnityEngine.UI.CanvasScaler>();
            go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var rt = go.GetComponent<RectTransform>();
            rt.localScale = Vector3.one;
            var cg = go.AddComponent<UnityEngine.CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;

            // 背景Image
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(go.transform, false);
            var image = bgGO.AddComponent<UnityEngine.UI.Image>();
            image.color = Color.black;
            var rect = bgGO.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            var fade = go.AddComponent<FadeTransitionEffect>();
            // SerializedObjectでCanvasGroupをアサイン
            var so = new SerializedObject(fade);
            so.FindProperty("_canvasGroup").objectReferenceValue = cg;
            so.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(go, $"{folder}/FadeTransitionEffect.prefab");
            Object.DestroyImmediate(go);
        }

        private static void CreateSpadeAnimatorPrefab(string prefabFolder)
        {
            const string animFolder  = "Assets/Ursa/Animations/Transitions";
            const string spritePath  = "Assets/Ursa/Textures/Transitions/spade_1024.png";
            const string prefabName  = "SpadeTransitionEffect";

            EnsureFolder("Assets/Ursa", "Animations");
            EnsureFolder("Assets/Ursa/Animations", "Transitions");

            // spade_1024.png をスプライトとしてインポート
            var importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"<color=cyan>[Ursa]</color> Spade texture not found: {spritePath}");
                return;
            }
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.SaveAndReimport();
            }
            var spadeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);

            // ---- Idle clip（scale=0 で待機、背景 alpha=0）----
            var idleClip = new AnimationClip { name = $"{prefabName}_Idle" };
            idleClip.wrapMode = WrapMode.Loop;
            // CanvasGroup alpha
            idleClip.SetCurve("", typeof(CanvasGroup), "m_Alpha",
                new AnimationCurve(new Keyframe(0f, 0f)));
            // Spade scale (x/y/z = 0)
            idleClip.SetCurve("Spade", typeof(Transform), "m_LocalScale.x",
                new AnimationCurve(new Keyframe(0f, 0f)));
            idleClip.SetCurve("Spade", typeof(Transform), "m_LocalScale.y",
                new AnimationCurve(new Keyframe(0f, 0f)));
            idleClip.SetCurve("Spade", typeof(Transform), "m_LocalScale.z",
                new AnimationCurve(new Keyframe(0f, 1f)));
            AssetDatabase.CreateAsset(idleClip, $"{animFolder}/{prefabName}_Idle.anim");

            // ---- Out clip（スペードが 0→大 に拡大、背景が 0→1 に暗転, 0.5s）----
            var outClip = new AnimationClip { name = $"{prefabName}_Out" };
            outClip.SetCurve("", typeof(CanvasGroup), "m_Alpha",
                AnimationCurve.EaseInOut(0f, 0f, 0.5f, 1f));
            outClip.SetCurve("Spade", typeof(Transform), "m_LocalScale.x",
                AnimationCurve.EaseInOut(0f, 0f, 0.5f, 15f));
            outClip.SetCurve("Spade", typeof(Transform), "m_LocalScale.y",
                AnimationCurve.EaseInOut(0f, 0f, 0.5f, 15f));
            outClip.SetCurve("Spade", typeof(Transform), "m_LocalScale.z",
                new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.5f, 1f)));
            AssetDatabase.CreateAsset(outClip, $"{animFolder}/{prefabName}_Out.anim");

            // ---- In clip（スペードが 大→0 に縮小、背景が 1→0 に明転, 0.5s）----
            var inClip = new AnimationClip { name = $"{prefabName}_In" };
            inClip.SetCurve("", typeof(CanvasGroup), "m_Alpha",
                AnimationCurve.EaseInOut(0f, 1f, 0.5f, 0f));
            inClip.SetCurve("Spade", typeof(Transform), "m_LocalScale.x",
                AnimationCurve.EaseInOut(0f, 15f, 0.5f, 0f));
            inClip.SetCurve("Spade", typeof(Transform), "m_LocalScale.y",
                AnimationCurve.EaseInOut(0f, 15f, 0.5f, 0f));
            inClip.SetCurve("Spade", typeof(Transform), "m_LocalScale.z",
                new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.5f, 1f)));
            AssetDatabase.CreateAsset(inClip, $"{animFolder}/{prefabName}_In.anim");

            AssetDatabase.SaveAssets();

            // ---- AnimatorController ----
            var controller = AnimatorController.CreateAnimatorControllerAtPath(
                $"{animFolder}/{prefabName}.controller");
            var sm = controller.layers[0].stateMachine;

            var idleState = sm.AddState("Idle");
            idleState.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{animFolder}/{prefabName}_Idle.anim");
            sm.defaultState = idleState;

            var outState = sm.AddState("Out");
            outState.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{animFolder}/{prefabName}_Out.anim");

            var inState = sm.AddState("In");
            inState.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{animFolder}/{prefabName}_In.anim");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            // ---- Prefab 構築 ----
            var go = new GameObject(prefabName);
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

            // スペード画像（中央固定・スケールアニメーション対象）
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

            PrefabUtility.SaveAsPrefabAsset(go, $"{prefabFolder}/{prefabName}.prefab");
            Object.DestroyImmediate(go);
        }

        private static void CreateShaderEffectPrefab(string prefabFolder, string matFolder, string name, string shaderPath)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            if (shader == null)
            {
                Debug.LogWarning($"<color=cyan>[Ursa]</color> Shader not found: {shaderPath}");
                return;
            }

            // マテリアルを作成・保存
            var mat = new Material(shader);
            string matPath = $"{matFolder}/{name}.mat";
            AssetDatabase.CreateAsset(mat, matPath);
            var savedMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

            // Prefab の GameObject 構築
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.sortingOrder = UrsaUIRenderOrder.Transition;
            canvas.planeDistance = 1f;
            go.AddComponent<UnityEngine.UI.CanvasScaler>();
            go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            go.GetComponent<RectTransform>().localScale = Vector3.one;

            // 全画面 RawImage（シェーダーで描画）
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(go.transform, false);
            var rawImage = bgGO.AddComponent<UnityEngine.UI.RawImage>();
            rawImage.material = savedMat;
            rawImage.color = Color.white;
            var bgRect = bgGO.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;

            // ShaderTransitionEffect にマテリアルをアサイン
            var effect = go.AddComponent<ShaderTransitionEffect>();
            var so = new SerializedObject(effect);
            so.FindProperty("_material").objectReferenceValue = savedMat;
            so.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(go, $"{prefabFolder}/{name}.prefab");
            Object.DestroyImmediate(go);
        }
    }
}
#endif
