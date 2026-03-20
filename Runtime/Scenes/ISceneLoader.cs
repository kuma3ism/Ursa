using System.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Ursa.Scenes
{
    /// <summary>
    /// シーンのロードとアンロードを抽象化するインターフェース。
    /// Addressables, AssetBundle, 標準のBuildSettingsなど様々な実装を提供する基盤となります。
    /// </summary>
    public interface ISceneLoader
    {
        Task<Scene> LoadSceneAsync(string sceneName, LoadSceneMode mode);
        Task UnloadSceneAsync(Scene scene);
    }
}
