using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Ursa;

namespace Ursa.Scenes
{
    /// <summary>
    /// Unity標準のBuild Settingsに登録されたシーンをロードするデフォルト実装。
    /// </summary>
    public class BuildSettingsSceneLoader : ISceneLoader
    {
        public async Task<Scene> LoadSceneAsync(string sceneName, LoadSceneMode mode)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, mode);
            if (op == null)
            {
                Debug.LogError($"[Ursa] Failed to load scene: {sceneName}");
                return default;
            }

            while (!op.isDone)
            {
                await Task.Yield();
            }

            // ロード完了後、一番最後に追加されたシーンが新しくロードされたシーンとなる
            return SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
        }

        public async Task UnloadSceneAsync(Scene scene)
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                var op = SceneManager.UnloadSceneAsync(scene);
                if (op != null)
                {
                    while (!op.isDone)
                    {
                        await Task.Yield();
                    }
                }
            }
        }
    }
}
