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

                    // 1. 保存先ディレクトリの作成
                    string dir = "Assets/Resources/Ursa";
                    if (!System.IO.Directory.Exists(dir))
                    {
                        System.IO.Directory.CreateDirectory(dir);
                        UnityEditor.AssetDatabase.Refresh();
                    }
                    UnityEditor.AssetDatabase.CreateAsset(_instance, $"{dir}/UrsaSettings.asset");
                }

                // 2. トランジション設定が空の場合は自動でローカルのプレファブ群を探してセットする
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
            var prefabs = new[] {
                "Fade", "FadeTransitionEffect",
                "Wipe", "ShaderWipeTransitionEffect",
                "Circle", "ShaderCircleTransitionEffect",
                "Dissolve", "ShaderDissolveTransitionEffect",
                "Animator", "AnimatorTransitionEffect"
            };
            for (int i = 0; i < prefabs.Length; i += 2)
            {
                var path = $"Assets/Ursa/Prefabs/Transitions/{prefabs[i + 1]}.prefab";
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<TransitionEffectBase>(path);
                if (prefab != null)
                {
                    Transitions.Add(new TransitionEntry { Name = prefabs[i], Prefab = prefab });
                    Debug.Log($"[Ursa] Auto attached transition prefab: {prefabs[i]}");
                }
                else
                {
                    Debug.LogWarning($"[Ursa] Failed to find transition prefab at: {path}");
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
        /// 指定した TransitionType に対応するプレハブを返します。
        /// </summary>
        public TransitionEffectBase GetTransitionPrefab(TransitionType type)
        {
            if (Transitions == null || type == TransitionType.Default) return null;

            string typeName = type.ToString();
            foreach (var entry in Transitions)
            {
                if (entry.Name == typeName) return entry.Prefab;
            }
            return null;
        }
    }
}
