using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ursa.Scenes
{
    /// <summary>
    /// シーンのロードや事前準備（Preload）を同期的に管理するためのハンドルクラス。
    /// UrsaCore.Scene.CreateScene() によって生成されます。
    /// </summary>
    public class SceneHandle<TScene> where TScene : MonoBehaviour
    {
        private readonly string _sceneName;
        private readonly ISceneLoader _loader;
        private Scene _loadedScene;
        private TScene _instance;

        public SceneHandle(string sceneName, ISceneLoader loader)
        {
            _sceneName = sceneName;
            _loader = loader;
        }

        /// <summary>
        /// 裏で .unity ファイルの非同期ロードを行い、シーン内の事前準備（PreloadAsync）を実行します。
        /// </summary>
        public async Task PreloadAsync()
        {
            if (_loadedScene.IsValid()) return;

            _loadedScene = await _loader.LoadSceneAsync(_sceneName, LoadSceneMode.Additive);
            if (!_loadedScene.IsValid())
            {
                Debug.LogError($"[Ursa] SceneHandle: Failed to load scene '{_sceneName}'.");
                return;
            }

            foreach (var go in _loadedScene.GetRootGameObjects())
            {
                if (_instance == null)
                {
                    _instance = go.GetComponentInChildren<TScene>(true);
                }

                var preloaders = go.GetComponentsInChildren<IScenePreloader>(true);
                foreach (var preloader in preloaders)
                {
                    await preloader.PreloadAsync();
                }
            }

            if (_instance == null)
            {
                Debug.LogWarning($"[Ursa] SceneHandle: Component '{typeof(TScene).Name}' not found in '{_sceneName}'.");
            }
        }

        /// <summary>
        /// ロードされたシーンインスタンス（MonoBehaviour）を取得します。
        /// ※まだ PreloadAsync() が呼ばれておらずロードされていない場合は、ここで自動的にロード・準備が行われます。
        /// </summary>
        public async Task<TScene> GetSceneAsync()
        {
            if (!_loadedScene.IsValid() || !_loadedScene.isLoaded)
            {
                await PreloadAsync();
            }
            return _instance;
        }
    }
}
