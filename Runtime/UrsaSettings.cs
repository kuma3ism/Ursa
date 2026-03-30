using System;
using System.Collections.Generic;
using UnityEngine;
using Ursa.Blur;
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

                // トランジション設定が空の場合は自動でプレファブを探してセットする
                if (_instance.Transitions == null || _instance.Transitions.Count == 0)
                {
                    _instance.SetupDefaultTransitions();
                    UnityEditor.EditorUtility.SetDirty(_instance);
                    UnityEditor.AssetDatabase.SaveAssets();
                    UnityEditor.AssetDatabase.Refresh();
                }

                // ブラー設定が空の場合は自動でプレファブを探してセットする
                if (_instance.Blurs == null || _instance.Blurs.Count == 0)
                {
                    _instance.SetupDefaultBlurs();
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
        private void SetupDefaultTransitions()
        {
            Transitions ??= new List<TransitionEntry>();
            Transitions.Clear();

            var targets = new[]
            {
                ("Fade",     "FadeTransitionEffect"),
                ("Wipe",     "ShaderWipeTransitionEffect"),
                ("Circle",   "ShaderCircleTransitionEffect"),
                ("Dissolve", "ShaderDissolveTransitionEffect"),
                ("Animator", "AnimatorTransitionEffect"),
            };

            foreach (var (name, typeName) in targets)
            {
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
                }
                else
                {
                    Debug.LogWarning($"[Ursa] Transition prefab not found: {typeName}");
                }
            }
        }

        private void SetupDefaultBlurs()
        {
            Blurs ??= new List<BlurEntry>();
            Blurs.Clear();

            var targets = new[]
            {
                ("Screenshot", "ScreenshotBlurEffect"),
            };

            foreach (var (name, typeName) in targets)
            {
                var guids = UnityEditor.AssetDatabase.FindAssets($"{typeName} t:prefab");
                BlurEffectBase prefab = null;
                foreach (var guid in guids)
                {
                    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    var candidate = UnityEditor.AssetDatabase.LoadAssetAtPath<BlurEffectBase>(path);
                    if (candidate != null) { prefab = candidate; break; }
                }

                if (prefab != null)
                {
                    Blurs.Add(new BlurEntry { Name = name, Prefab = prefab });
                    Debug.Log($"[Ursa] Auto attached blur prefab: {name}");
                }
                else
                {
                    Debug.LogWarning($"[Ursa] Blur prefab not found: {typeName}");
                }
            }
        }
#endif

        // ==========================================
        //  設定項目
        // ==========================================

        [Header("シーン遷移（Transitions）")]
        public List<TransitionEntry> Transitions = new List<TransitionEntry>();

        [Serializable]
        public class TransitionEntry
        {
            public string Name;
            [Tooltip("使用するエフェクトの実体（TransitionEffectBase）")]
            public TransitionEffectBase Prefab;
        }

        [Header("背景ブラー（Blurs）")]
        public List<BlurEntry> Blurs = new List<BlurEntry>();

        [Serializable]
        public class BlurEntry
        {
            public string Name;
            [Tooltip("使用するブラーエフェクトの実体（BlurEffectBase）")]
            public BlurEffectBase Prefab;
        }

        /// <summary>
        /// 指定した TransitionType に対応するプレハブを返します。
        /// </summary>
        public TransitionEffectBase GetTransitionPrefab(TransitionType type)
        {
            if (Transitions == null || type == TransitionType.Default) return null;
            string typeName = type.ToString();
            foreach (var entry in Transitions)
                if (entry.Name == typeName) return entry.Prefab;
            return null;
        }

        /// <summary>
        /// 指定した名前のブラーエフェクトプレハブを返します。
        /// 名前を省略するとリストの先頭を返します。
        /// </summary>
        public BlurEffectBase GetBlurPrefab(string blurName = null)
        {
            if (Blurs == null || Blurs.Count == 0) return null;
            if (string.IsNullOrEmpty(blurName)) return Blurs[0].Prefab;
            foreach (var entry in Blurs)
                if (entry.Name == blurName) return entry.Prefab;
            return null;
        }
    }
}
