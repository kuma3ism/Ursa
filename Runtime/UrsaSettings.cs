using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Ursa.Transitions;

namespace Ursa
{
    /// <summary>
    /// Ursaフレームワーク全体のグローバル設定管理クラス。
    /// プロジェクト（Assets/Resources/Ursa/UrsaSettings.asset）に保存され、
    /// エディタおよびランタイムからシングルトン（Instance）としてアクセスできます。
    /// </summary>
    [CreateAssetMenu(fileName = "UrsaSettings", menuName = "Ursa/Ursa Settings")]
    public class UrsaSettings : ScriptableObject
    {
        private static UrsaSettings _instance;

        public static UrsaSettings Instance
        {
            get
            {
                if (_instance != null) return _instance;

                _instance = Resources.Load<UrsaSettings>("Ursa/UrsaSettings");

#if UNITY_EDITOR
                if (_instance == null)
                {
                    _instance = CreateInstance<UrsaSettings>();

                    string dir = "Assets/Resources/Ursa";
                    if (!System.IO.Directory.Exists(dir))
                    {
                        System.IO.Directory.CreateDirectory(dir);
                        UnityEditor.AssetDatabase.Refresh();
                    }
                    UnityEditor.AssetDatabase.CreateAsset(_instance, $"{dir}/UrsaSettings.asset");
                }

                // 未登録のデフォルトトランジションを補完する
                bool dirty = _instance.SetupDefaultTransitions();

                // デフォルトダイアログバリアマテリアルを生成
                dirty |= _instance.SetupDefaultDialogMaterials();

                if (dirty)
                {
                    UnityEditor.EditorUtility.SetDirty(_instance);
                    UnityEditor.AssetDatabase.SaveAssets();
                    UnityEditor.AssetDatabase.Refresh();
                }
#endif
                return _instance;
            }
            set => _instance = value;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 未登録のデフォルトトランジションをリストに補完します。
        /// 既存エントリは変更しません。
        /// </summary>
        /// <returns>1件以上追加された場合は true。</returns>
        private bool SetupDefaultTransitions()
        {
            Transitions ??= new List<TransitionEntry>();

            var targets = new[]
            {
                ("Fade",     "FadeTransitionEffect"),
                ("Wipe",     "ShaderWipeTransitionEffect"),
                ("Circle",   "ShaderCircleTransitionEffect"),
                ("Spade",    "SpadeTransitionEffect"),
            };

            bool dirty = false;
            foreach (var (name, typeName) in targets)
            {
                // 既に同名で登録済みならスキップ
                bool exists = false;
                foreach (var entry in Transitions)
                {
                    if (entry.Name == name) { exists = true; break; }
                }
                if (exists) continue;

                // プレハブを検索して追加
                var guids = UnityEditor.AssetDatabase.FindAssets($"{typeName} t:prefab");
                TransitionEffectBase prefab = null;
                foreach (var guid in guids)
                {
                    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    var candidate = UnityEditor.AssetDatabase.LoadAssetAtPath<TransitionEffectBase>(path);
                    if (candidate != null) { prefab = candidate; break; }
                }

                if (prefab != null)
                {
                    Transitions.Add(new TransitionEntry { Name = name, Prefab = prefab });
                    Debug.Log($"[Ursa] Auto attached transition prefab: {name}");
                    dirty = true;
                }
                else
                {
                    Debug.LogWarning($"[Ursa] Transition prefab not found: {typeName}");
                }
            }
            return dirty;
        }

        /// <summary>
        /// デフォルトのダイアログバリア用マテリアルを生成します。
        /// 既に存在する場合は上書きしません。
        /// </summary>
        /// <returns>1件以上作成された場合は true。</returns>
        private bool SetupDefaultDialogMaterials()
        {
            string dir = "Assets/Resources/Ursa";
            if (!System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
                UnityEditor.AssetDatabase.Refresh();
            }

            bool dirty = false;

            dirty |= CreateDialogMaterialIfNeeded(
                $"{dir}/UrsaRealtimeBlur.mat",
                "Ursa/UI/DialogBlur",
                "UrsaDialogBlur.shader",
                "_BlurSize", 4.0f);

            dirty |= CreateDialogMaterialIfNeeded(
                $"{dir}/UrsaCameraOpaqueTextureBlur.mat",
                "Ursa/UI/CameraOpaqueTextureBlur",
                "UrsaCameraOpaqueTextureBlur.shader",
                "_BlurSize", 4.0f);

            dirty |= CreateDialogMaterialIfNeeded(
                $"{dir}/UrsaRendererFeatureBlur.mat",
                "Ursa/UI/RendererFeatureBlur",
                "UrsaRendererFeatureBlur.shader",
                "_BlurSize", 4.0f);

            dirty |= CreateDialogMaterialIfNeeded(
                $"{dir}/UrsaScreenshotBlur.mat",
                "Ursa/UI/ScreenshotBlur",
                "UrsaScreenshotBlur.shader",
                "_BlurSize", 2.0f);

            return dirty;
        }

        /// <summary>
        /// 指定パスにマテリアルが存在しなければ、指定シェーダーから新規作成します。
        /// </summary>
        private bool CreateDialogMaterialIfNeeded(string materialPath, string shaderName, string shaderFileName, string blurSizeProperty, float defaultBlurSize)
        {
            if (UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(materialPath) != null)
                return false;

            var shader = ResolveDialogShader(shaderName, shaderFileName);
            if (shader == null)
            {
                Debug.LogWarning($"[Ursa] ダイアログバリア用シェーダーが見つかりません: {shaderName} ({shaderFileName})");
                return false;
            }

            var material = new Material(shader);
            if (material.HasProperty(blurSizeProperty))
                material.SetFloat(blurSizeProperty, defaultBlurSize);

            UnityEditor.AssetDatabase.CreateAsset(material, materialPath);
            Debug.Log($"[Ursa] Created default dialog barrier material: {materialPath}");
            return true;
        }

        private Shader ResolveDialogShader(string shaderName, string shaderFileName)
        {
            var shader = Shader.Find(shaderName);
            if (shader != null)
                return shader;

            var guids = UnityEditor.AssetDatabase.FindAssets($"{Path.GetFileNameWithoutExtension(shaderFileName)} t:Shader");
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path) != shaderFileName)
                    continue;

                shader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader != null && shader.name == shaderName)
                    return shader;
            }

            return null;
        }
#endif

        // ==========================================
        //  設定項目
        // ==========================================

        [Header("シーン遷移（Transitions）")]
        [SerializeField]
        public List<TransitionEntry> Transitions = new List<TransitionEntry>();

        [Serializable]
        public class TransitionEntry
        {
            public string Name;
            [Tooltip("使用するエフェクトの実体（TransitionEffectBase）")]
            public TransitionEffectBase Prefab;
        }

        /// <summary>
        /// 指定した名前に対応するプレハブを返します。null または空文字の場合は null を返します。
        /// </summary>
        public TransitionEffectBase GetTransitionPrefab(string name)
        {
            if (Transitions == null || string.IsNullOrEmpty(name)) return null;

            foreach (var entry in Transitions)
            {
                if (entry.Name == name) return entry.Prefab;
            }
            return null;
        }
    }
}
