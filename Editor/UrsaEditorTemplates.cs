using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ursa.Editor
{
    /// <summary>
    /// Ursa エディタージェネレーター共通のテンプレートロード・適用ユーティリティ。
    ///
    /// テンプレートの優先順位:
    ///   1. Assets/UrsaTemplates/{category}/{fileName}  ← ユーザーオーバーライド
    ///   2. Ursa/Editor/Templates/{category}/{fileName} ← パッケージデフォルト
    ///
    /// プレースホルダー一覧:
    ///   #CLASSNAME#        生成クラス名
    ///   #PARAMTYPE#        パラメータークラス名（ダイアログ用）
    ///   #PLACEMENT#        DialogPlacement の値（ダイアログ用）
    ///   #NAMESPACE_OPEN#   namespace Foo\n{\n  または空文字
    ///   #NAMESPACE_CLOSE#  }\n              または空文字
    ///   #INDENT#           4スペース         または空文字
    /// </summary>
    internal static class UrsaEditorTemplates
    {
        private const string UserTemplateRoot = "Assets/UrsaTemplates";

        /// <summary>
        /// テンプレートファイルを読み込む。
        /// ユーザーオーバーライドが存在しない場合はパッケージデフォルトを使用する。
        /// </summary>
        public static string Load(string category, string fileName)
        {
            // 1. ユーザーオーバーライド
            string userAssetPath = $"{UserTemplateRoot}/{category}/{fileName}";
            var userAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(userAssetPath);
            if (userAsset != null) return userAsset.text;

            // 2. パッケージデフォルト（Assets/Ursa 直下 または UPM パッケージ内）
            string[] guids = AssetDatabase.FindAssets(
                $"{Path.GetFileNameWithoutExtension(fileName)} t:TextAsset");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains($"/Ursa/Editor/Templates/{category}/") && path.EndsWith(fileName))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                    if (asset != null) return asset.text;
                }
            }

            throw new FileNotFoundException(
                $"[Ursa] テンプレートが見つかりません: {category}/{fileName}\n" +
                $"  デフォルト: Ursa/Editor/Templates/{category}/{fileName}\n" +
                $"  ユーザー:   {UserTemplateRoot}/{category}/{fileName}");
        }

        /// <summary>
        /// テンプレートにプレースホルダー置換と名前空間ラップを適用する。
        /// </summary>
        /// <param name="template">Load() で取得したテンプレート文字列</param>
        /// <param name="ns">名前空間。空の場合は #NAMESPACE_OPEN# 等が空文字に置換される</param>
        /// <param name="replacements">(プレースホルダー, 値) のペア</param>
        public static string Apply(string template, string ns,
            params (string key, string value)[] replacements)
        {
            bool hasNs = !string.IsNullOrWhiteSpace(ns);

            string result = template
                .Replace("\r\n", "\n")
                .Replace("#NAMESPACE_OPEN#",  hasNs ? $"namespace {ns}\n{{\n" : "")
                .Replace("#NAMESPACE_CLOSE#", hasNs ? "}\n" : "")
                .Replace("#INDENT#",          hasNs ? "    " : "");

            foreach (var (key, value) in replacements)
                result = result.Replace(key, value);

            return result.TrimEnd() + "\n";
        }
    }
}
