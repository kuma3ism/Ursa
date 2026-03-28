using UnityEditor;
using UnityEngine;

namespace Ursa.Editor
{
    /// <summary>
    /// メニューから開いて UrsaSettings 全体を設定・編集するための専用ウィンドウ
    /// </summary>
    public class UrsaSettingsEditorWindow : EditorWindow
    {
        private UnityEditor.Editor _cachedEditor;
        private Vector2 _scrollPos;

        [MenuItem("Ursa/Ursa Settings", priority = 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<UrsaSettingsEditorWindow>("Ursa Settings");
            window.minSize = new Vector2(350, 500);
            window.Show();
        }

        private void OnGUI()
        {
            var settings = UrsaSettings.Instance;
            
            if (settings == null)
            {
                EditorGUILayout.HelpBox("UrsaSettings のインスタンスが見つかりません。", MessageType.Error);
                return;
            }

            GUILayout.Label("Ursa Global Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("フレームワーク全体の設定を行います。\nここで設定された内容は、自動的にランタイムで読み込まれます。", MessageType.Info);
            EditorGUILayout.Space();

            // Inspectorと同様の描画を行うために Editor オブジェクトをキャッシュして利用
            if (_cachedEditor == null || _cachedEditor.target != settings)
            {
                UnityEditor.Editor.CreateCachedEditor(settings, null, ref _cachedEditor);
            }

            if (_cachedEditor != null)
            {
                EditorGUI.BeginChangeCheck();
                
                // スクロールビュー開始
                _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
                
                _cachedEditor.OnInspectorGUI();
                
                EditorGUILayout.EndScrollView();

                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssets();
                }
            }
        }
        
        private void OnDisable()
        {
            if (_cachedEditor != null)
            {
                DestroyImmediate(_cachedEditor);
                _cachedEditor = null;
            }
        }
    }
}
