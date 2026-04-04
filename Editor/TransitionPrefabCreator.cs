#if URSA_DEVELOPER
using System.IO;
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
            CreateMaskEffectPrefab(prefabFolder, matFolder);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=cyan>[Ursa]</color> Transition Prefabs created in {prefabFolder}");
        }

        // ---------------------------------------------------------------
        //  Mask トランジション（マスクテクスチャで形を制御）
        // ---------------------------------------------------------------

        private static void CreateMaskEffectPrefab(string prefabFolder, string matFolder)
        {
            const string shaderPath = "Assets/Ursa/Shaders/MaskTransition.shader";
            const string texPath    = "Assets/Ursa/Textures/Transitions/HeartMask.png";
            const string name       = "ShaderMaskTransitionEffect";

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            if (shader == null)
            {
                Debug.LogWarning($"<color=cyan>[Ursa]</color> Shader not found: {shaderPath}");
                return;
            }

            EnsureFolder("Assets/Ursa", "Textures");
            EnsureFolder("Assets/Ursa/Textures", "Transitions");
            var heartTex = GenerateHeartMaskTexture(texPath);

            var mat = new Material(shader);
            mat.SetTexture("_MaskTex", heartTex);
            string matPath = $"{matFolder}/{name}.mat";
            AssetDatabase.CreateAsset(mat, matPath);
            var savedMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            go.AddComponent<UnityEngine.UI.CanvasScaler>();
            go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            go.GetComponent<RectTransform>().localScale = Vector3.one;

            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(go.transform, false);
            var rawImage = bgGO.AddComponent<UnityEngine.UI.RawImage>();
            rawImage.material = savedMat;
            rawImage.color = Color.white;
            var bgRect = bgGO.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;

            var effect = go.AddComponent<ShaderTransitionEffect>();
            var so = new SerializedObject(effect);
            so.FindProperty("_material").objectReferenceValue = savedMat;
            so.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(go, $"{prefabFolder}/{name}.prefab");
            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// ハート形グラデーションマスクテクスチャを生成して PNG として保存します。
        /// マスク値が低いピクセルほど先に黒くなります（ハート中心 → 外側の順に拡大）。
        /// </summary>
        private static Texture2D GenerateHeartMaskTexture(string savePath)
        {
            const int size = 256;

            // 1st pass: SDF を計算してinside/outside の最大絶対値を収集
            var sdfValues = new float[size * size];
            float minSDF = 0f;   // inside の最小値（負）
            float maxSDF = 0f;   // outside の最大値（正）

            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    // UV を心臓方程式の座標空間へ変換（Y は上向き正）
                    float hx = (px / (float)(size - 1) - 0.5f) * 3.0f;
                    float hy = (py / (float)(size - 1) - 0.5f) * 3.0f - 0.25f;
                    float sdf = HeartSDF(hx, hy);
                    sdfValues[py * size + px] = sdf;
                    if (sdf < minSDF) minSDF = sdf;
                    if (sdf > maxSDF) maxSDF = sdf;
                }
            }

            // 2nd pass: SDF をグラデーション値 [0.01, 1.0] へ正規化してテクスチャへ
            // inside (sdf <= 0) → [0.01, 0.499]  : ハート中心が低値 = 最初に黒くなる
            // outside (sdf > 0) → [0.501, 1.00]  : 遠いほど高値 = 最後に黒くなる
            var pixels = new Color32[size * size];
            for (int i = 0; i < sdfValues.Length; i++)
            {
                float sdf  = sdfValues[i];
                float mask;
                if (sdf <= 0f)
                {
                    float t = (minSDF != 0f) ? sdf / minSDF : 0f;  // 0=境界, 1=中心
                    mask = Mathf.Lerp(0.499f, 0.01f, t);
                }
                else
                {
                    float t = (maxSDF != 0f) ? Mathf.Min(sdf / maxSDF, 1.0f) : 1.0f;
                    mask = Mathf.Lerp(0.501f, 1.0f, t);
                }
                byte b = (byte)(mask * 255f);
                pixels[i] = new Color32(b, b, b, 255);
            }

            var tex = new Texture2D(size, size, TextureFormat.R8, false);
            tex.SetPixels32(pixels);
            tex.Apply();

            File.WriteAllBytes(savePath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(savePath);

            // テクスチャのインポート設定を調整（Clamp, 非圧縮グレースケール）
            var importer = AssetImporter.GetAtPath(savePath) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode        = TextureWrapMode.Clamp;
                importer.filterMode      = FilterMode.Bilinear;
                importer.textureType     = TextureImporterType.Default;
                importer.sRGBTexture     = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(savePath);
        }

        /// <summary>
        /// ハートの暗黙方程式による符号付き距離の近似値。
        /// 負 = ハート内部、正 = 外部。
        /// f(x,y) = (x² + y² - 1)³ - x² * y³
        /// </summary>
        private static float HeartSDF(float x, float y)
        {
            float a = x * x + y * y - 1f;
            return a * a * a - x * x * (y * y * y);
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
