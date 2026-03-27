using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Ursa.Scenes;

namespace Ursa.Editor
{
    /// <summary>
    /// エディター専用の ISceneLoader。
    /// Build Settings への登録なしに、AssetDatabase からシーン名でパスを検索してロードします。
    /// 同名シーンが複数存在する場合は最初に見つかったものをロードし、警告を出します。
    /// </summary>
    public class EditorSceneLoader : ISceneLoader
    {
        public async Task<Scene> LoadSceneAsync(string sceneName, LoadSceneMode mode)
        {
            string scenePath = FindScenePath(sceneName);
            if (scenePath == null)
            {
                Debug.LogError($"[Ursa] Scene '{sceneName}' が AssetDatabase 内に見つかりませんでした。");
                return default;
            }

            var op = EditorSceneManager.LoadSceneAsyncInPlayMode(
                scenePath, new LoadSceneParameters(mode));
            if (op == null)
            {
                Debug.LogError($"[Ursa] Scene '{sceneName}' のロードに失敗しました。パス: {scenePath}");
                return default;
            }

            while (!op.isDone)
                await Task.Yield();

            return SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
        }

        public async Task UnloadSceneAsync(Scene scene)
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                var op = SceneManager.UnloadSceneAsync(scene);
                if (op != null)
                    while (!op.isDone)
                        await Task.Yield();
            }
        }

        private static string FindScenePath(string sceneName)
        {
            var guids = AssetDatabase.FindAssets("t:Scene");
            string found = null;
            int matchCount = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == sceneName)
                {
                    matchCount++;
                    found ??= path;
                }
            }

            if (matchCount > 1)
                Debug.LogWarning($"[Ursa] 同名のシーンが {matchCount} 個見つかりました: '{sceneName}'。最初に見つかった '{found}' をロードします。");

            return found;
        }
    }

    /// <summary>
    /// エディター起動時に EditorSceneLoader を UrsaSceneManager へ自動登録します。
    /// </summary>
    [InitializeOnLoad]
    static class EditorSceneLoaderInstaller
    {
        static EditorSceneLoaderInstaller()
        {
            UrsaSceneManager.DefaultEditorLoader = new EditorSceneLoader();
        }
    }
}
