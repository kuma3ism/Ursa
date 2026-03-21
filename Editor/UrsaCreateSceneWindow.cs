using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ursa.Editor
{
    /// <summary>
    /// Ursaシーンテンプレート作成ウィンドウ
    /// Ursa/Create Scene... から開く
    /// </summary>
    public class UrsaCreateSceneWindow : EditorWindow
    {
        private string _featureName = "NewScene";
        private string _namespace = "";
        private bool _withResult = false;
        private bool _registerToBuildSettings = true;
        private bool _namespaceDirty = false;

        // コンパイル後にスクリプトアタッチするための SessionState キー
        private const string SessionKeyScenePath = "Ursa_PendingScenePath";
        private const string SessionKeyTypeName  = "Ursa_PendingTypeName";

        [MenuItem("Ursa/Create Scene...", priority = 1)]
        [MenuItem("Assets/Create/Ursa/Create Scene...", priority = 1)]
        public static void Open()
        {
            var window = GetWindow<UrsaCreateSceneWindow>(true, "Create Ursa Scene", true);
            window.minSize = new Vector2(360, 180);
            window.maxSize = new Vector2(360, 180);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);

            EditorGUI.BeginChangeCheck();
            _featureName = EditorGUILayout.TextField("Feature Name", _featureName);
            if (EditorGUI.EndChangeCheck() && !_namespaceDirty)
                _namespace = _featureName;

            EditorGUI.BeginChangeCheck();
            _namespace = EditorGUILayout.TextField("Namespace", _namespace);
            if (EditorGUI.EndChangeCheck())
                _namespaceDirty = _namespace != _featureName;

            EditorGUILayout.Space(4);
            _withResult = EditorGUILayout.Toggle("With Result (戻り値あり)", _withResult);
            _registerToBuildSettings = EditorGUILayout.Toggle("Register to Build Settings", _registerToBuildSettings);

            EditorGUILayout.Space(12);

            bool isValid = IsValidIdentifier(_featureName);
            if (!isValid)
                EditorGUILayout.HelpBox("Feature Name はC#の識別子として有効な文字列にしてください。", MessageType.Warning);

            using (new EditorGUI.DisabledScope(!isValid))
            {
                if (GUILayout.Button("Create", GUILayout.Height(32)))
                {
                    CreateScene();
                    Close();
                }
            }
        }

        private void CreateScene()
        {
            string basePath   = GetSelectedFolderPath();
            string featureDir = Path.Combine(basePath, _featureName);

            // --- 既存チェック ---
            if (Directory.Exists(featureDir))
            {
                if (!EditorUtility.DisplayDialog("確認",
                    $"'{_featureName}' はすでに存在します。上書きしますか？",
                    "上書き", "キャンセル"))
                    return;
            }
            string scriptDir  = Path.Combine(featureDir, "Script");
            string sceneDir   = Path.Combine(featureDir, "Scene");
            string scriptPath = Path.Combine(scriptDir, $"{_featureName}.cs");
            string scenePath  = Path.Combine(sceneDir,  $"{_featureName}.unity");

            // --- フォルダ作成 ---
            Directory.CreateDirectory(scriptDir);
            Directory.CreateDirectory(sceneDir);
            Directory.CreateDirectory(Path.Combine(featureDir, "Prefab"));
            Directory.CreateDirectory(Path.Combine(featureDir, "Texture"));
            // 空フォルダをGitで追跡するための.gitkeep
            File.WriteAllText(Path.Combine(featureDir, "Prefab",  ".gitkeep"), "");
            File.WriteAllText(Path.Combine(featureDir, "Texture", ".gitkeep"), "");

            // --- C# スクリプト生成 ---
            string scriptContent = _withResult
                ? GenerateScriptWithResult(_featureName, _namespace)
                : GenerateScript(_featureName, _namespace);
            File.WriteAllText(scriptPath, scriptContent);

            // --- シーン作成（スクリプトなしの GameObject を置く）---
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var rootGO = new GameObject(_featureName);
            SceneManager.MoveGameObjectToScene(rootGO, newScene);
            EditorSceneManager.SaveScene(newScene, scenePath);
            EditorSceneManager.CloseScene(newScene, true);

            // --- Build Settings 自動登録 ---
            if (_registerToBuildSettings)
                AddSceneToBuildSettings(scenePath);

            // --- コンパイル後にスクリプトをアタッチするよう予約 ---
            string fullTypeName = string.IsNullOrWhiteSpace(_namespace)
                ? _featureName
                : $"{_namespace}.{_featureName}";
            SessionState.SetString(SessionKeyScenePath, scenePath);
            SessionState.SetString(SessionKeyTypeName,  fullTypeName);

            // コンパイル開始
            AssetDatabase.Refresh();
            Debug.Log($"<color=cyan>[Ursa]</color> Generating scene... コンパイル後にスクリプトをアタッチします。");
        }

        /// <summary>
        /// コンパイル完了・エディター起動後に呼ばれ、予約されていたスクリプトを
        /// シーンの GameObject にアタッチして再保存する。
        /// </summary>
        [InitializeOnLoadMethod]
        private static void TryAttachPendingScript()
        {
            // 予約がなければ何もしない
            string scenePath   = SessionState.GetString(SessionKeyScenePath, "");
            string fullTypeName = SessionState.GetString(SessionKeyTypeName,  "");
            if (string.IsNullOrEmpty(scenePath)) return;

            // 予約をクリア
            SessionState.EraseString(SessionKeyScenePath);
            SessionState.EraseString(SessionKeyTypeName);

            // コンパイル完了後に実行するよう少し遅らせる
            EditorApplication.delayCall += () => AttachScript(scenePath, fullTypeName);
        }

        private static void AttachScript(string scenePath, string fullTypeName)
        {
            // MonoScript からスクリプトを検索
            var guids = AssetDatabase.FindAssets($"t:MonoScript");
            MonoScript targetScript = null;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
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

            // シーンを開いて GameObject にアタッチ
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            foreach (var go in scene.GetRootGameObjects())
            {
                go.AddComponent(targetScript.GetClass());
                break; // 最初の Root GO にアタッチ
            }
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);

            Debug.Log($"<color=lime>[Ursa]</color> スクリプトをアタッチしました: {scenePath}");
            EditorUtility.RevealInFinder(Path.GetDirectoryName(scenePath));
        }

        // ────────────────────────────────────────────
        // テンプレート生成
        // ────────────────────────────────────────────

        private static string GenerateScript(string name, string ns)
        {
            string nsOpen  = string.IsNullOrWhiteSpace(ns) ? "" : $"namespace {ns}\n{{\n";
            string nsClose = string.IsNullOrWhiteSpace(ns) ? "" : "}\n";
            string i       = string.IsNullOrWhiteSpace(ns) ? "" : "    ";

            return
$@"using System.Threading.Tasks;
using Ursa;
using Ursa.Scenes;

{nsOpen}{i}public class {name}Parameter : ISceneParameter {{ }}

{i}public class {name} : SceneBase<{name}Parameter>
{i}{{
{i}    public override async Task OpenAsync({name}Parameter parameter)
{i}    {{
{i}        await base.OpenAsync(parameter);
{i}    }}

{i}    protected override async Task OnUpdateAsync()
{i}    {{
{i}        await Task.CompletedTask;
{i}    }}

{i}    public override void OnBackToScene()
{i}    {{
{i}        base.OnBackToScene();
{i}    }}
{i}}}
{nsClose}";
        }

        private static string GenerateScriptWithResult(string name, string ns)
        {
            string nsOpen  = string.IsNullOrWhiteSpace(ns) ? "" : $"namespace {ns}\n{{\n";
            string nsClose = string.IsNullOrWhiteSpace(ns) ? "" : "}\n";
            string i       = string.IsNullOrWhiteSpace(ns) ? "" : "    ";

            return
$@"using System.Threading.Tasks;
using Ursa;
using Ursa.Scenes;

{nsOpen}{i}public class {name}Parameter : ISceneParameter {{ }}

{i}public class {name}Result {{ }}

{i}public class {name} : SceneBase<{name}Parameter, {name}Result>
{i}{{
{i}    public override async Task OpenAsync({name}Parameter parameter)
{i}    {{
{i}        await base.OpenAsync(parameter);
{i}    }}

{i}    protected override async Task OnUpdateAsync()
{i}    {{
{i}        await Task.CompletedTask;
{i}    }}

{i}    public override void OnBackToScene()
{i}    {{
{i}        base.OnBackToScene();
{i}    }}

{i}    protected override void OnBackKeyPressed()
{i}    {{
{i}        _ = CloseAsync(default);
{i}    }}
{i}}}
{nsClose}";
        }

        // ────────────────────────────────────────────
        // ユーティリティ
        // ────────────────────────────────────────────

        private static string GetSelectedFolderPath()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(path)) return "Assets";
            return Directory.Exists(path) ? path : Path.GetDirectoryName(path);
        }

        private static bool IsValidIdentifier(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (!char.IsLetter(name[0]) && name[0] != '_') return false;
            foreach (char c in name)
                if (!char.IsLetterOrDigit(c) && c != '_') return false;
            return true;
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
                if (s.path == scenePath) return;

            var newScenes = new EditorBuildSettingsScene[scenes.Length + 1];
            scenes.CopyTo(newScenes, 0);
            newScenes[scenes.Length] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = newScenes;
        }
    }
}
