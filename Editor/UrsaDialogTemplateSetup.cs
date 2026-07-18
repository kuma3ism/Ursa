using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Ursa.Editor
{
    /// <summary>
    /// ダイアログ生成ウィンドウで使うテンプレートプレファブを生成・管理する。
    /// Ursa/Setup Dialog Template Prefab からいつでも再生成できる。
    ///
    /// ※ Barrier は UrsaDialogManager が管理するため、テンプレートプレファブには含めません。
    ///
    /// テンプレートプレファブの優先順位:
    ///   1. Assets/UrsaTemplates/Dialog/TemplateDialog.prefab  ← ユーザーカスタマイズ
    ///   2. Ursa/Editor/Templates/Dialog/TemplateDialog.prefab ← パッケージデフォルト
    /// </summary>
    internal static class UrsaDialogTemplateSetup
    {
        private const string DefaultTemplatePath = "Assets/Ursa/Editor/Templates/Dialog/TemplateDialog.prefab";
        private const string UserTemplatePath    = "Assets/UrsaTemplates/Dialog/TemplateDialog.prefab";

        [MenuItem("Ursa/Setup Dialog Template Prefab", priority = 11)]
        public static void Setup()
        {
            EnsureFolder("Assets/Ursa/Editor/Templates", "Dialog");

            var root = BuildTemplatePrefab("TemplateDialog");
            PrefabUtility.SaveAsPrefabAsset(root, DefaultTemplatePath);
            Object.DestroyImmediate(root);

            AssetDatabase.Refresh();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultTemplatePath);
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"<color=cyan>[Ursa]</color> ダイアログテンプレートプレファブを生成しました → {DefaultTemplatePath}");
        }

        internal static GameObject LoadTemplatePrefab()
        {
            // 1. ユーザーカスタマイズ優先
            var userPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UserTemplatePath);
            if (userPrefab != null) return userPrefab;

            // 2. パッケージデフォルト
            string[] guids = AssetDatabase.FindAssets("TemplateDialog t:Prefab");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (UrsaEditorTemplates.IsDefaultTemplatePath(
                    path,
                    "Dialog",
                    "TemplateDialog.prefab"))
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            return null;
        }

        // ────────────────────────────────────────────
        // プレファブ構造
        // Barrier は UrsaDialogManager が管理するので含めない。
        // ────────────────────────────────────────────

        internal static GameObject BuildTemplatePrefab(string rootName)
        {
            // Root（フルストレッチ）
            var root     = new GameObject(rootName);
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // Panel（中央配置、600x400）
            var panel     = new GameObject("Panel");
            panel.transform.SetParent(root.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot     = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(600f, 400f);

            // Panel/Background
            var bg     = new GameObject("Background");
            bg.transform.SetParent(panel.transform, false);
            var bgRect = bg.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImg = bg.AddComponent<Image>();
            bgImg.color         = new Color(0.15f, 0.15f, 0.15f, 1f);
            bgImg.raycastTarget = true;

            // Panel/Content（パディング 24px）
            var content     = new GameObject("Content");
            content.transform.SetParent(panel.transform, false);
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = new Vector2(24f,  24f);
            contentRect.offsetMax = new Vector2(-24f, -24f);

            return root;
        }

        private static void EnsureFolder(string parent, string folderName)
        {
            string path = $"{parent}/{folderName}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
