using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Ursa;

namespace Ursa.Scenes
{
    /// <summary>
    /// Ursaフレームワークにおける、シーンのロード・アンロードおよび履歴（スタック）管理を行う実体クラス。
    /// ISceneManagerの実装であり、シングルトンとして管理されることが想定されています。
    /// </summary>
    public class UrsaSceneManager : ISceneManager
    {
        private bool _isTransitioning;
        public bool IsTransitioning => _isTransitioning;
        private Stack<Scene> _history = new Stack<Scene>();
        private readonly ISceneLoader _sceneLoader;

        public UrsaSceneManager(ISceneLoader sceneLoader = null)
        {
            _sceneLoader = sceneLoader ?? new BuildSettingsSceneLoader();
        }

        // 遷移フラグ管理を共通化
        private async Task ExecuteTransitionAsync(Func<Task> action)
        {
            _isTransitioning = true;
            try { await action(); }
            finally { _isTransitioning = false; }
        }

        /// <summary>
        /// 全履歴を捨てて、新しいシーンへ遷移 (Single)
        /// </summary>
        public async Task ResetAsync<TScene>(ISceneParameter parameter) where TScene : MonoBehaviour
        {
            if (_isTransitioning)
            {
                Debug.LogWarning("[Ursa] 遷移中のため、ResetAsync 要求を無視しました。");
                return;
            }

            _history.Clear();
            await InternalLoad(typeof(TScene).Name, parameter, LoadSceneMode.Single);
        }

        /// <summary>
        /// 現在のシーンの上に重ねる (Additive)
        /// </summary>
        public async Task PushAsync<TScene>(ISceneParameter parameter) where TScene : MonoBehaviour
        {
            if (_isTransitioning)
            {
                Debug.LogWarning("[Ursa] 遷移中のため、PushAsync 要求を無視しました。");
                return;
            }

            if (_history.Count == 0) RegisterInitialScene();
            await InternalLoad(typeof(TScene).Name, parameter, LoadSceneMode.Additive);
        }

        /// <summary>
        /// 現在の最前面シーンを捨てて、新しいシーンに入れ替える
        /// </summary>
        public async Task ReplaceAsync<TScene>(ISceneParameter parameter) where TScene : MonoBehaviour
        {
            if (_isTransitioning)
            {
                Debug.LogWarning("[Ursa] 遷移中のため、ReplaceAsync 要求を無視しました。");
                return;
            }

            await ExecuteTransitionAsync(async () =>
            {
                // 1. 今の一番上を取り出す
                if (_history.Count > 0)
                {
                    Scene oldScene = _history.Pop();
                    if (oldScene.IsValid() && oldScene.isLoaded)
                    {
                        Debug.Log($"<color=orange>[Ursa]</color> Replacing: {oldScene.name}");
                        await _sceneLoader.UnloadSceneAsync(oldScene);
                    }
                }

                // 2. 新しいシーンをロードする
                string sceneName = typeof(TScene).Name;
                Task<Scene> sceneLoadTask = _sceneLoader.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                Task resourceLoadTask = (parameter as ISceneResourcePreloader)?.PreloadResourcesAsync(null) ?? Task.CompletedTask;

                await Task.WhenAll(sceneLoadTask, resourceLoadTask);
                Scene newlyLoadedScene = await sceneLoadTask;

                // 3. 新しいシーンを登録する
                if (newlyLoadedScene.IsValid())
                    _history.Push(newlyLoadedScene);

                if (parameter != null) await InjectParameterToScene(newlyLoadedScene, parameter);
            });
        }

        /// <summary>
        /// 一つ前のシーンに戻る
        /// </summary>
        public async Task PopAsync()
        {
            if (_isTransitioning || _history.Count <= 1)
            {
                Debug.LogWarning("[Ursa] 戻る先のシーンがない、もしくは遷移中です。");
                return;
            }

            await ExecuteTransitionAsync(async () =>
            {
                Scene scene = _history.Pop();
                if (scene.IsValid() && scene.isLoaded)
                {
                    Debug.Log($"<color=cyan>[Ursa]</color> Pop: {scene.name}");
                    await _sceneLoader.UnloadSceneAsync(scene);
                }
                NotifyBackToScene();
            });
        }

        private async Task InternalLoad(string sceneName, ISceneParameter parameter, LoadSceneMode mode)
        {
            await ExecuteTransitionAsync(async () =>
            {
                Debug.Log($"<color=cyan>[Ursa]</color> Loading: {sceneName} ({mode})");

                Task<Scene> sceneLoadTask = _sceneLoader.LoadSceneAsync(sceneName, mode);
                Task resourceLoadTask = (parameter as ISceneResourcePreloader)?.PreloadResourcesAsync(null) ?? Task.CompletedTask;

                await Task.WhenAll(sceneLoadTask, resourceLoadTask);
                Scene newlyLoadedScene = await sceneLoadTask;

                if (!newlyLoadedScene.IsValid()) return;

                _history.Push(newlyLoadedScene);

                if (parameter != null)
                    await InjectParameterToScene(newlyLoadedScene, parameter);
            });
        }

        private void RegisterInitialScene()
        {
            if (_history.Count > 0) return;

            Scene active = SceneManager.GetActiveScene();
            _history.Push(active);
            Debug.Log($"<color=cyan>[Ursa]</color> Initial scene '{active.name}' registered (Handle: {active.handle})");
        }

        private async Task InjectParameterToScene(Scene targetScene, ISceneParameter parameter)
        {
            foreach (var go in targetScene.GetRootGameObjects())
            {
                foreach (var receiver in go.GetComponentsInChildren<ISceneReceiver>())
                {
                    await receiver.OnEnterScene(parameter);
                }
            }
        }

        private void NotifyBackToScene()
        {
            if (_history.Count == 0) return;

            Scene activeScene = _history.Peek();
            if (activeScene.IsValid() && activeScene.isLoaded)
            {
                foreach (var go in activeScene.GetRootGameObjects())
                {
                    foreach (var handler in go.GetComponentsInChildren<ISceneBackHandler>())
                        handler.OnResumeScene();
                }
            }
        }

        public bool IsTopScene(Scene scene)
        {
            if (_history.Count == 0) RegisterInitialScene();
            return _history.Peek().handle == scene.handle;
        }

        /// <summary>
        /// 指定したシーンをロード（Additive）し、対象となるTSceneコンポーネントのインスタンスを検索して返します。
        /// ロードされた時点では履歴スタックへの追加はまだ行われません。
        /// </summary>
        public async Task<TScene> CreateSceneAsync<TScene>(ISceneParameter parameter = null) where TScene : MonoBehaviour
        {
            if (_isTransitioning)
            {
                Debug.LogWarning("[Ursa] 遷移中のため、CreateSceneAsync 要求を無視しました。");
                return null;
            }

            string sceneName = typeof(TScene).Name;
            TScene result = null;

            await ExecuteTransitionAsync(async () =>
            {
                Debug.Log($"<color=cyan>[Ursa]</color> Loading Instance: {sceneName}");

                Task<Scene> sceneLoadTask = _sceneLoader.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                Task resourceLoadTask = (parameter as ISceneResourcePreloader)?.PreloadResourcesAsync(null) ?? Task.CompletedTask;

                await Task.WhenAll(sceneLoadTask, resourceLoadTask);
                Scene newlyLoadedScene = await sceneLoadTask;

                if (!newlyLoadedScene.IsValid())
                {
                    Debug.LogWarning($"[Ursa] {sceneName} シーンのロードに失敗しました。");
                    return;
                }

                foreach (var go in newlyLoadedScene.GetRootGameObjects())
                {
                    var comp = go.GetComponentInChildren<TScene>(true);
                    if (comp != null)
                    {
                        result = comp;
                        return;
                    }
                }

                Debug.LogWarning($"[Ursa] {sceneName} シーンから {typeof(TScene).Name} が見つかりませんでした。");
            });

            return result;
        }

        /// <summary>
        /// 既にロード済みのシーンインスタンスを、現在の履歴（スタック）の最前面にPush（追加）します。
        /// </summary>
        public Task PushInstanceAsync(Scene scene)
        {
            if (_isTransitioning)
            {
                Debug.LogWarning("[Ursa] 遷移中のため、PushInstanceAsync 要求を無視しました。");
                return Task.CompletedTask;
            }

            if (_history.Count == 0) RegisterInitialScene();
            _history.Push(scene);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 既にロード済みのシーンインスタンスを、現在の最前面のシーンと入れ替え（Replace）て履歴を更新します。
        /// </summary>
        public async Task ReplaceInstanceAsync(Scene scene)
        {
            if (_isTransitioning)
            {
                Debug.LogWarning("[Ursa] 遷移中のため、ReplaceInstanceAsync 要求を無視しました。");
                return;
            }

            await ExecuteTransitionAsync(async () =>
            {
                if (_history.Count > 0)
                {
                    Scene oldScene = _history.Pop();
                    if (oldScene.IsValid() && oldScene.isLoaded)
                    {
                        Debug.Log($"<color=orange>[Ursa]</color> Replacing Instance: {oldScene.name}");
                        await _sceneLoader.UnloadSceneAsync(oldScene);
                    }
                }
                _history.Push(scene);
            });
        }

        /// <summary>
        /// 対象のシーンをロードし、パラメーターを渡して開いた上で、
        /// そのシーンが閉じられて結果が返ってくるまで待機して値を返します。
        /// </summary>
        public async Task<TResult> OpenResultAsync<TScene, TParam, TResult>(TParam parameter)
            where TScene : SceneBaseWithResult<TParam, TResult>
            where TParam : ISceneParameter
        {
            var sceneInstance = await CreateSceneAsync<TScene>(parameter);
            if (sceneInstance == null) return default;

            await sceneInstance.OpenAsync(parameter);
            return await sceneInstance.WaitForResultAsync();
        }
    }
}