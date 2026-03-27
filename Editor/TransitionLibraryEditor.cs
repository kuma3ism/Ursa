using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Ursa.Transitions.Editor
{
    [CustomEditor(typeof(TransitionLibrary))]
    public class TransitionLibraryEditor : UnityEditor.Editor
    {
        private const string EnumFileRelativePath = "Ursa/Runtime/Transitions/TransitionType.cs";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();

            if (GUILayout.Button("Generate TransitionType Enum"))
            {
                GenerateEnum();
            }
        }

        private void GenerateEnum()
        {
            var library = (TransitionLibrary)target;

            // 登録されている名前を正規化して収集
            var names = new List<string> { "Default" };
            foreach (var entry in library.Entries)
            {
                if (!string.IsNullOrEmpty(entry.Name))
                {
                    // スペースや記号をアンダースコアに置換し、先頭が数字の場合はプレフィックスを付ける
                    string safeName = Regex.Replace(entry.Name, @"[^a-zA-Z0-9_]", "_");
                    if (safeName.Length > 0 && char.IsDigit(safeName[0]))
                        safeName = "_" + safeName;

                    if (!string.IsNullOrEmpty(safeName))
                        names.Add(safeName);
                }
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("namespace Ursa.Transitions");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 自動生成されるトランジションタイプ Enum。");
            sb.AppendLine("    /// TransitionLibrary の内容に合わせてエディター拡張によって更新されます。");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public enum TransitionType");
            sb.AppendLine("    {");

            for (int i = 0; i < names.Count; i++)
            {
                sb.Append("        ").Append(names[i]);
                if (i < names.Count - 1) sb.Append(",");
                sb.AppendLine();
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            try
            {
                // Application.dataPath は "<project>/Assets" を返すため、直接結合する
                string fullPath = Path.Combine(UnityEngine.Application.dataPath, EnumFileRelativePath).Replace("\\", "/");
                string assetPath = "Assets/" + EnumFileRelativePath;
                File.WriteAllText(fullPath, sb.ToString());
                AssetDatabase.Refresh();
                Debug.Log($"<color=cyan>[Ursa]</color> Generated TransitionType enum at: {assetPath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Ursa] Failed to generate TransitionType enum: {e.Message}");
            }
        }
    }
}
