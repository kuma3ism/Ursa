using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Ursa.Editor
{
    /// <summary>
    /// Ursaシーンテンプレート作成ウィンドウ
    /// Ursa/Create Scene... から開く
    /// </summary>
    public class UrsaCreateSceneWindow : EditorWindow
    {
        private string _topDomain = "Game";
        private string _subDomain = "";
        private string _namespace = "";
        private string _sceneName = "NewScene";
        private bool _registerToBuildSettings = true;
        private bool _namespaceDirty = false;

        private const string PrefKeyTopDomain = "Ursa_TopDomain";
        private const string PrefKeySubDomain  = "Ursa_SubDomain";

        private const string SessionKeyScenePath = "Ursa_PendingScenePath";
        private const string SessionKeyTypeName  = "Ursa_PendingTypeName";

        [MenuItem("Ursa/Create Scene...", priority = 1)]
        [MenuItem("Assets/Create/Ursa/Create Scene...", priority = 1)]
        public static void Open()
        {
            var window = GetWindow<UrsaCreateSceneWindow>(true, "Create Ursa Scene", true);
            window.minSize = new Vector2(380, 220);
            window.maxSize = new Vector2(380, 300);
            window.Show();
        }

        private void OnEnable()
        {
            _topDomain = EditorPrefs.GetString(PrefKeyTopDomain, "Game");
            _subDomain  = EditorPrefs.GetString(PrefKeySubDomain,  "");
            if (!_namespaceDirty)
                _namespace = BuildDefaultNamespace(_topDomain, _subDomain);
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
            _registerToBuildSettings = EditorGUILayout.Toggle("Register to Build Settings", _registerToBuildSettings);
            EditorGUILayout.Space(4);

            _sceneName = EditorGUILayout.TextField("Scene Name", _sceneName);

            EditorGUILayout.Space(12);

            bool isValidScene = IsValidIdentifier(_sceneName);
            bool isValidTop   = !string.IsNullOrWhiteSpace(_topDomain);
            bool isSameAsNs   = !string.IsNullOrWhiteSpace(_namespace) && _namespace == _sceneName;

            if (!isValidTop)
                EditorGUILayout.HelpBox("Top Domain は必須です。", MessageType.Error);
            if (!isValidScene)
                EditorGUILayout.HelpBox("Scene Name は C# の識別子として有効な文字列にしてください。", MessageType.Warning);
            if (isSameAsNs)
                EditorGUILayout.HelpBox("Namespace と Scene Name が同じです。外部から 'Hoge.Hoge' のように参照が冗長になります。", MessageType.Warning);

            using (new EditorGUI.DisabledScope(!isValidScene || !isValidTop))
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
            string top      = _topDomain.Trim('/');
            string basePath = top.StartsWith("Assets") ? top : $"Assets/{top}";
            if (!string.IsNullOrWhiteSpace(_subDomain))
                basePath = $"{basePath}/{_subDomain.Trim('/')}";

            string scriptDir = $"{basePath}/Script";
            string sceneDir  = $"{basePath}/Scene";

            string projectRoot   = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string absScriptPath = Path.Combine(projectRoot, scriptDir, $"{_sceneName}.cs");
            string absPrefabKeep = Path.Combine(projectRoot, basePath,  "Prefab", ".gitkeep");
            string absTexKeep    = Path.Combine(projectRoot, basePath,  "Texture", ".gitkeep");
            string scenePath     = $"{sceneDir}/{_sceneName}.unity";

            if (File.Exists(Path.Combine(projectRoot, scenePath)))
            {
                if (!EditorUtility.DisplayDialog("確認",
                    $"'{_sceneName}' はすでに存在します。上書きしますか？",
                    "上書き", "キャンセル"))
                    return;
            }

            EnsureFolder(basePath.Substring(0, basePath.LastIndexOf('/')),
                         basePath.Substring(basePath.LastIndexOf('/') + 1));
            EnsureFolder(basePath, "Script");
            EnsureFolder(basePath, "Scene");
            EnsureFolder(basePath, "Prefab");
            EnsureFolder(basePath, "Texture");
            File.WriteAllText(absPrefabKeep, "");
            File.WriteAllText(absTexKeep,    "");

            string scriptContent = GenerateScript(_sceneName, _namespace);
            File.WriteAllText(absScriptPath, scriptContent);

            var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var rootGO = new GameObject(_sceneName);
            SceneManager.MoveGameObjectToScene(rootGO, newScene);

            var cameraGO = new GameObject("Main Camera");
            cameraGO.tag = "MainCamera";
            cameraGO.AddComponent<Camera>();
            cameraGO.AddComponent<AudioListener>();
            SceneManager.MoveGameObjectToScene(cameraGO, newScene);

            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            SceneManager.MoveGameObjectToScene(lightGO, newScene);

            var volumeGO = new GameObject("Global Volume");
            var volume = volumeGO.AddComponent<Volume>();
            volume.isGlobal = true;
            var defaultProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/DefaultVolumeProfile.asset");
            if (defaultProfile != null) volume.sharedProfile = defaultProfile;
            SceneManager.MoveGameObjectToScene(volumeGO, newScene);

            EditorSceneManager.SaveScene(newScene, scenePath);
            EditorSceneManager.CloseScene(newScene, true);

            if (_registerToBuildSettings)
                AddSceneToBuildSettings(scenePath);

            string fullTypeName = string.IsNullOrWhiteSpace(_namespace)
                ? _sceneName
                : $"{_namespace}.{_sceneName}";
            SessionState.SetString(SessionKeyScenePath, scenePath);
            SessionState.SetString(SessionKeyTypeName,  fullTypeName);

            AssetDatabase.Refresh();
            Debug.Log($"<color=cyan>[Ursa]</color> Scene '{_sceneName}' を生成しました → {basePath}");
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

        [InitializeOnLoadMethod]
        private static void TryAttachPendingScript()
        {
            string scenePath    = SessionState.GetString(SessionKeyScenePath, "");
            string fullTypeName = SessionState.GetString(SessionKeyTypeName,  "");
            if (string.IsNullOrEmpty(scenePath)) return;

            SessionState.EraseString(SessionKeyScenePath);
            SessionState.EraseString(SessionKeyTypeName);

            EditorApplication.delayCall += () => AttachScript(scenePath, fullTypeName);
        }

        private static void AttachScript(string scenePath, string fullTypeName)
        {
            var guids = AssetDatabase.FindAssets("t:MonoScript");
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

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            foreach (var go in scene.GetRootGameObjects())
            {
                go.AddComponent(targetScript.GetClass());
                break;
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

{nsOpen}{i}public class {name} : SceneBase<{name}.Parameter>
{i}{{
{i}    public class Parameter : ISceneParameter {{ public string Message; }}

{i}    protected override async Task OnInitializeAsync(Parameter parameter)
{i}    {{
{i}        await Task.CompletedTask;
{i}    }}

{i}    public override void OnResumeScene()
{i}    {{
{i}    }}

{i}    public override void OnPauseScene()
{i}    {{
{i}    }}

{i}    protected override async Task OnSceneWillClose()
{i}    {{
{i}        await Task.CompletedTask;
{i}    }}

{i}    public override void OnTransitionOutCompleted()
{i}    {{
{i}    }}

{i}    public override void OnTransitionInStarted()
{i}    {{
{i}    }}

{i}    protected override async Task OnBackKeyPressed()
{i}    {{
{i}        await CloseAsync();
{i}    }}
{i}}}
{nsClose}";
        }

        // ────────────────────────────────────────────
        // ユーティリティ
        // ────────────────────────────────────────────

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
