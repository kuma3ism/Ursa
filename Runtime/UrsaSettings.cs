using System;
using System.Collections.Generic;
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

                // トランジション設定が空の場合は自動でプレファブを探してセットする
                if (_instance.Transitions == null || _instance.Transitions.Count == 0)
                {
                    _instance.SetupDefaultTransitions();
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

            // パス決め打ちをやめ、型名で検索する（パッケージ配布時も Packages/ 以下を検索できる）
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
                // t:prefab + 型名で検索。Assets/ と Packages/ 両方がヒットする
                var guids = UnityEditor.AssetDatabase.FindAssets($"{typeName} t:prefab");
                TransitionEffectBase prefab = null;
                foreach (var guid in guids)
                {
                    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    var candidate = UnityEditor.AssetDatabase.LoadAssetAtPath<TransitionEffectBase>(path);
                    if (candidate != null)
                    {
                        prefab = candidate;
                        break;
                    }
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
