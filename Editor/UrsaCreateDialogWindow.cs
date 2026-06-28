using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ursa.Editor
{
    /// <summary>
    /// Ursaダイアログテンプレート作成ウィンドウ。
    /// Ursa/Create Dialog... から開く。
    /// </summary>
    public class UrsaCreateDialogWindow : EditorWindow
    {
        private enum PlacementMode { Scene, DontDestroyOnLoad }

        private string     _topDomain          = "Game";
        private string     _subDomain          = "";
        private string     _namespace          = "";
        private string     _dialogName         = "NewDialog";
        private bool       _namespaceDirty     = false;
        private bool       _createParameterFile = true;
        private PlacementMode _placement       = PlacementMode.DontDestroyOnLoad;
        private GameObject _basePrefab         = null;

        private const string PrefKeyTopDomain      = "Ursa_DialogTopDomain";
        private const string PrefKeySubDomain      = "Ursa_DialogSubDomain";
        private const string PrefKeyPlacement      = "Ursa_DialogPlacement";
        private const string PrefKeyBasePrefabGuid = "Ursa_DialogBasePrefabGuid";

        private const string SessionKeyDialogScriptPath = "Ursa_PendingDialogScriptPath";
        private const string SessionKeyDialogPrefabPath = "Ursa_PendingDialogPrefabPath";
        private const string SessionKeyDialogTypeName   = "Ursa_PendingDialogTypeName";

        [MenuItem("Ursa/Create Dialog...", priority = 2)]
        [MenuItem("Assets/Create/Ursa/Create Dialog...", priority = 2)]
        public static void Open()
        {
            var window = GetWindow<UrsaCreateDialogWindow>(true, "Create Ursa Dialog", true);
            window.minSize = new Vector2(380, 290);
            window.maxSize = new Vector2(380, 410);
            window.Show();
        }

        private void OnEnable()
        {
            _topDomain = EditorPrefs.GetString(PrefKeyTopDomain, "Game");
            _subDomain = EditorPrefs.GetString(PrefKeySubDomain, "");
            _placement = (PlacementMode)EditorPrefs.GetInt(PrefKeyPlacement, (int)PlacementMode.DontDestroyOnLoad);

            if (!_namespaceDirty)
                _namespace = BuildDefaultNamespace(_topDomain, _subDomain);

            // ベースプレファブを復元。保存済み GUID → なければテンプレートをデフォルトに
            string savedGuid = EditorPrefs.GetString(PrefKeyBasePrefabGuid, "");
            if (!string.IsNullOrEmpty(savedGuid))
            {
                string savedPath = AssetDatabase.GUIDToAssetPath(savedGuid);
                _basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(savedPath);
            }
            if (_basePrefab == null)
                _basePrefab = UrsaDialogTemplateSetup.LoadTemplatePrefab();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);

            EditorGUI.BeginChangeCheck();
            _topDomain = EditorGUILayout.TextField("Top Domain", _topDomain);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString(PrefKeyTopDomain, _topDomain);
                if (!_namespaceDirty)
                    _namespace = BuildDefaultNamespace(_topDomain, _subDomain);
            }

            EditorGUI.BeginChangeCheck();
            _subDomain = EditorGUILayout.TextField("Sub Domain", _subDomain);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString(PrefKeySubDomain, _subDomain);
                if (!_namespaceDirty)
                    _namespace = BuildDefaultNamespace(_topDomain, _subDomain);
            }

            EditorGUI.BeginChangeCheck();
            _namespace = EditorGUILayout.TextField("Namespace", _namespace);
            if (EditorGUI.EndChangeCheck())
                _namespaceDirty = _namespace != BuildDefaultNamespace(_topDomain, _subDomain);

            EditorGUILayout.Space(4);

            EditorGUI.BeginChangeCheck();
            _placement = (PlacementMode)EditorGUILayout.EnumPopup("Placement", _placement);
            if (EditorGUI.EndChangeCheck())
                EditorPrefs.SetInt(PrefKeyPlacement, (int)_placement);

            _createParameterFile = EditorGUILayout.Toggle("Separate Parameter File", _createParameterFile);

            EditorGUILayout.Space(4);

            _dialogName = EditorGUILayout.TextField("Dialog Name", _dialogName);

            // Base Prefab フィールド
            EditorGUI.BeginChangeCheck();
            _basePrefab = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("Base Prefab", "コピー元のプレファブ。空のときは最小構成を自動生成します"),
                _basePrefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck())
            {
                string guid = _basePrefab != null
                    ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_basePrefab))
                    : "";
                EditorPrefs.SetString(PrefKeyBasePrefabGuid, guid);
            }

            EditorGUILayout.Space(12);

            bool isValidDialog  = IsValidIdentifier(_dialogName);
            bool isValidTop     = !string.IsNullOrWhiteSpace(_topDomain);
            bool isSameAsNs     = !string.IsNullOrWhiteSpace(_namespace) && _namespace == _dialogName;
            bool endsWithDialog = _dialogName.EndsWith("Dialog");

            if (!isValidTop)
                EditorGUILayout.HelpBox("Top Domain は必須です。", MessageType.Error);
            if (!isValidDialog)
                EditorGUILayout.HelpBox("Dialog Name は C# の識別子として有効な文字列にしてください。", MessageType.Warning);
            if (endsWithDialog)
                EditorGUILayout.HelpBox("'Dialog' 接尾辞が既に含まれているため、クラス名はそのまま使用されます。", MessageType.Info);
            if (isSameAsNs)
                EditorGUILayout.HelpBox("Namespace と Dialog Name が同じです。外部から 'Hoge.HogeDialog' のように参照が冗長になります。", MessageType.Warning);

            using (new EditorGUI.DisabledScope(!isValidDialog || !isValidTop))
            {
                if (GUILayout.Button("Create", GUILayout.Height(32)))
                {
                    CreateDialog();
                    Close();
                }
            }
        }

        private void CreateDialog()
        {
            string rawName    = _dialogName;
            string className  = rawName.EndsWith("Dialog") ? rawName : $"{rawName}Dialog";

            string top          = _topDomain.Trim('/');
            string basePath     = top.StartsWith("Assets") ? top : $"Assets/{top}";
            string dialogFolder = $"{basePath}/Dialogs";
            if (!string.IsNullOrWhiteSpace(_subDomain))
                dialogFolder = $"{dialogFolder}/{_subDomain.Trim('/')}";
            string scriptDir = $"{dialogFolder}/{rawName}";

            string resourcesDir = "Assets/Resources/Dialogs";
            string prefabPath   = $"{resourcesDir}/{className}.prefab";

            string projectRoot      = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string absScriptDir     = Path.Combine(projectRoot, scriptDir);
            string absParameterPath = Path.Combine(absScriptDir, $"{className}Parameter.cs");
            string absScriptPath    = Path.Combine(absScriptDir, $"{className}.cs");

            if (File.Exists(Path.Combine(projectRoot, prefabPath)) || File.Exists(absScriptPath))
            {
                if (!EditorUtility.DisplayDialog("確認",
                    $"'{className}' はすでに存在します。上書きしますか？",
                    "上書き", "キャンセル"))
                    return;
            }

            EnsureFolder("Assets", top);
            EnsureFolder(basePath, "Dialogs");
            EnsureFolder(dialogFolder, rawName);
            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets/Resources", "Dialogs");

            if (_createParameterFile)
                File.WriteAllText(absParameterPath, GenerateParameterFile(className, _namespace, _placement));

            File.WriteAllText(absScriptPath, GenerateScript(className, _namespace, _createParameterFile, _placement));

            // ─── プレファブ生成 ───────────────────────────
            if (_basePrefab != null)
            {
                // ベースプレファブをコピー
                string srcPath = AssetDatabase.GetAssetPath(_basePrefab);
                if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(prefabPath)))
                    AssetDatabase.DeleteAsset(prefabPath);
                AssetDatabase.CopyAsset(srcPath, prefabPath);

                // コピー内を編集：ルート名変更 ＋ 旧 DialogBase を除去
                using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
                {
                    var prefabRoot = scope.prefabContentsRoot;
                    prefabRoot.name = className;
                    foreach (var mb in prefabRoot.GetComponents<MonoBehaviour>())
                    {
                        if (IsDialogBaseSubclass(mb))
                        {
                            DestroyImmediate(mb);
                            break;
                        }
                    }
                }
            }
            else
            {
                // ベースプレファブなし → 最小構成を自動生成
                var tempRoot = CreateMinimalPrefabRoot(className);
                PrefabUtility.SaveAsPrefabAsset(tempRoot, prefabPath);
                DestroyImmediate(tempRoot);
            }

            string fullTypeName = string.IsNullOrWhiteSpace(_namespace)
                ? className : $"{_namespace}.{className}";

            SessionState.SetString(SessionKeyDialogScriptPath, absScriptPath);
            SessionState.SetString(SessionKeyDialogPrefabPath, prefabPath);
            SessionState.SetString(SessionKeyDialogTypeName,   fullTypeName);

            AssetDatabase.Refresh();
            Debug.Log($"<color=cyan>[Ursa]</color> Dialog '{className}' を生成しました → {scriptDir}");
        }

        [InitializeOnLoadMethod]
        private static void TryAttachPendingScript()
        {
            string scriptPath   = SessionState.GetString(SessionKeyDialogScriptPath, "");
            string prefabPath   = SessionState.GetString(SessionKeyDialogPrefabPath, "");
            string fullTypeName = SessionState.GetString(SessionKeyDialogTypeName,   "");

            if (string.IsNullOrEmpty(scriptPath) || string.IsNullOrEmpty(prefabPath)) return;

            SessionState.EraseString(SessionKeyDialogScriptPath);
            SessionState.EraseString(SessionKeyDialogPrefabPath);
            SessionState.EraseString(SessionKeyDialogTypeName);

            EditorApplication.delayCall += () => AttachScriptToPrefab(prefabPath, fullTypeName);
        }

        private static void AttachScriptToPrefab(string prefabPath, string fullTypeName)
        {
            MonoScript targetScript = null;
            foreach (var guid in AssetDatabase.FindAssets("t:MonoScript"))
            {
                var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                if (ms != null && ms.GetClass() != null && ms.GetClass().FullName == fullTypeName)
                {
                    targetScript = ms;
                    break;
                }
            }

            if (targetScript == null)
            {
                Debug.LogWarning($"[Ursa] スクリプト '{fullTypeName}' が見つかりません。手動でアタッチしてください。");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                Debug.LogWarning($"[Ursa] Prefab '{prefabPath}' が見つかりません。");
                return;
            }

            using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
            {
                var root = scope.prefabContentsRoot;
                var dialogType = targetScript.GetClass();
                if (root.GetComponent(dialogType) == null)
                    root.AddComponent(dialogType);
            }

            Debug.Log($"<color=lime>[Ursa]</color> スクリプトを Prefab にアタッチしました: {prefabPath}");
            EditorUtility.RevealInFinder(Path.GetDirectoryName(prefabPath));
        }

        // ────────────────────────────────────────────
        // Prefab 構成
        // ────────────────────────────────────────────

        /// <summary>
        /// Base Prefab 未指定時のフォールバック用最小構成。
        /// </summary>
        private static GameObject CreateMinimalPrefabRoot(string className)
        {
            var root     = new GameObject(className);
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var content     = new GameObject("Content");
            content.transform.SetParent(root.transform, false);
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot     = new Vector2(0.5f, 0.5f);
            contentRect.sizeDelta = new Vector2(600f, 400f);

            return root;
        }

        // ────────────────────────────────────────────
        // テンプレート生成
        // ────────────────────────────────────────────

        private static string GenerateScript(string className, string ns, bool separateParameter, PlacementMode placement)
        {
            string fileName = separateParameter ? "DialogScript.txt" : "DialogScriptInline.txt";
            string placementValue = placement == PlacementMode.DontDestroyOnLoad
                ? "DialogPlacement.DontDestroyOnLoad"
                : "DialogPlacement.Scene";
            string template = UrsaEditorTemplates.Load("Dialog", fileName);
            return UrsaEditorTemplates.Apply(template, ns,
                ("#CLASSNAME#", className),
                ("#PARAMTYPE#", $"{className}Parameter"),
                ("#PLACEMENT#", placementValue));
        }

        private static string GenerateParameterFile(string className, string ns, PlacementMode placement)
        {
            string placementValue = placement == PlacementMode.DontDestroyOnLoad
                ? "DialogPlacement.DontDestroyOnLoad"
                : "DialogPlacement.Scene";
            string template = UrsaEditorTemplates.Load("Dialog", "DialogParameter.txt");
            return UrsaEditorTemplates.Apply(template, ns,
                ("#CLASSNAME#", className),
                ("#PLACEMENT#", placementValue));
        }

        // ────────────────────────────────────────────
        // ユーティリティ
        // ────────────────────────────────────────────

        /// <summary>
        /// MonoBehaviour が DialogBase のサブクラスかどうかを判定する。
        /// </summary>
        private static bool IsDialogBaseSubclass(MonoBehaviour mb)
        {
            var t = mb.GetType().BaseType;
            while (t != null && t != typeof(MonoBehaviour) && t != typeof(object))
            {
                if (t.IsGenericType && t.GetGenericTypeDefinition().Name.StartsWith("DialogBase"))
                    return true;
                t = t.BaseType;
            }
            return false;
        }

        private static void EnsureFolder(string parent, string folderName)
        {
            if (!AssetDatabase.IsValidFolder(parent))
            {
                int lastSlash = parent.LastIndexOf('/');
                if (lastSlash > 0)
                    EnsureFolder(parent.Substring(0, lastSlash), parent.Substring(lastSlash + 1));
            }
            string path = $"{parent}/{folderName}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, folderName);
        }

        private static string BuildDefaultNamespace(string topDomain, string subDomain)
        {
            string top = topDomain?.Trim('/') ?? "";
            if (top.StartsWith("Assets/")) top = top.Substring("Assets/".Length);
            else if (top == "Assets") top = "";
            string sub = subDomain?.Trim('/') ?? "";
            if (string.IsNullOrWhiteSpace(top) && string.IsNullOrWhiteSpace(sub)) return "";
            if (string.IsNullOrWhiteSpace(sub)) return top;
            if (string.IsNullOrWhiteSpace(top)) return sub;
            return $"{top}.{sub}";
        }

        private static bool IsValidIdentifier(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (!char.IsLetter(name[0]) && name[0] != '_') return false;
            foreach (char c in name)
                if (!char.IsLetterOrDigit(c) && c != '_') return false;
            return true;
        }
    }
}
