#if URSA_DEVELOPER
using UnityEditor;
using UnityEngine;
using Ursa.Transitions;

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
            CreateAnimatorPrefab(prefabFolder);
            CreateShaderEffectPrefab(prefabFolder, matFolder, "ShaderWipeTransitionEffect",     "Assets/Ursa/Shaders/WipeTransition.shader");
            CreateShaderEffectPrefab(prefabFolder, matFolder, "ShaderCircleTransitionEffect",   "Assets/Ursa/Shaders/CircleTransition.shader");
            CreateShaderEffectPrefab(prefabFolder, matFolder, "ShaderDissolveTransitionEffect", "Assets/Ursa/Shaders/DissolveTransition.shader");

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
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
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

        private static void CreateAnimatorPrefab(string folder)
        {
            var go = new GameObject("AnimatorTransitionEffect");
            go.AddComponent<AnimatorTransitionEffect>();

            PrefabUtility.SaveAsPrefabAsset(go, $"{folder}/AnimatorTransitionEffect.prefab");
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
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
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
