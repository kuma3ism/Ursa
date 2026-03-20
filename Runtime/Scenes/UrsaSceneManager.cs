using System;
using System.Collections.Generic;
using System.Linq;
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
        private Stack<Scene> _history = new Stack<Scene>();
        private readonly ISceneLoader _sceneLoader;

        public UrsaSceneManager(ISceneLoader sceneLoader = null)
        {
            _sceneLoader = sceneLoader ?? new BuildSettingsSceneLoader();
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
            _isTransitioning = true; // 遷移開始

            try
            {
                // 1. 今の一番上を取り出す
                if (_history.Count > 0)
                {
                    Scene oldScene = _history.Pop(); // スタックから抜く
                    if (oldScene.IsValid() && oldScene.isLoaded)
                    {
                        Debug.Log($"<color=orange>[Ursa]</color> Replacing: {oldScene.name}");
                        await _sceneLoader.UnloadSceneAsync(oldScene);
                    }
                }

                // 2. 新しいシーンをロードする
                string sceneName = typeof(TScene).Name;
                Scene newlyLoadedScene = await _sceneLoader.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

                // 3. 新しいシーンを登録する
                if (newlyLoadedScene.IsValid())
                {
                    _history.Push(newlyLoadedScene); // 新しいシーンを積む
                }

                await ExecutePreloadAsync(newlyLoadedScene);

                if (parameter != null) await InjectParameterToScene(newlyLoadedScene, parameter);
            }
            finally
            {
                _isTransitioning = false; // 確実にフラグを戻す
            }
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

            _isTransitioning = true;
            try
            {
                Scene scene = _history.Pop();
                if (scene.IsValid() && scene.isLoaded)
                {
                    Debug.Log($"<color=cyan>[Ursa]</color> Pop: {scene.name}");
                    await _sceneLoader.UnloadSceneAsync(scene);
                }

                NotifyBackToScene();
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        private async Task InternalLoad(string sceneName, ISceneParameter parameter, LoadSceneMode mode)
        {
            _isTransitioning = true;
            try
            {
                Debug.Log($"<color=cyan>[Ursa]</color> Loading: {sceneName} ({mode})");
                Scene newlyLoadedScene = await _sceneLoader.LoadSceneAsync(sceneName, mode);
                if (!newlyLoadedScene.IsValid()) return;

                _history.Push(newlyLoadedScene);

                await ExecutePreloadAsync(newlyLoadedScene);

                if (parameter != null)
                {
                    await InjectParameterToScene(newlyLoadedScene, parameter);
                }
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        private async Task ExecutePreloadAsync(Scene targetScene)
        {
            if (!targetScene.IsValid()) return;
            var rootObjects = targetScene.GetRootGameObjects();
            foreach (var go in rootObjects)
            {
                var preloaders = go.GetComponentsInChildren<IScenePreloader>(true);
                foreach (var preloader in preloaders)
                {
                    await preloader.PreloadAsync();
                }
            }
        }

        private void RegisterInitialScene()
        {
            // すでに履歴があるなら何もしない
            if (_history.Count > 0) return;

            Scene active = SceneManager.GetActiveScene();

            _history.Push(active);

            Debug.Log($"<color=cyan>[Ursa]</color> Initial scene '{active.name}' registered (Handle: {active.handle})");
        }

        private async Task InjectParameterToScene(Scene targetScene, ISceneParameter parameter)
        {
            var rootObjects = targetScene.GetRootGameObjects();
            foreach (var go in rootObjects)
            {
                var receivers = go.GetComponentsInChildren<MonoBehaviour>();
                foreach (var mono in receivers)
                {
                    var interfaces = mono.GetType().GetInterfaces();
                    var receiverInterface = interfaces.FirstOrDefault(i =>
                        i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISceneReceiver<>));

                    if (receiverInterface != null)
                    {
                        var expectedParamType = receiverInterface.GetGenericArguments()[0];
                        if (expectedParamType.IsAssignableFrom(parameter.GetType()))
                        {
                            var method = receiverInterface.GetMethod("OnEnterScene");
                            if (method != null) await (Task)method.Invoke(mono, new object[] { parameter });
                        }
                    }
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
                    var monos = go.GetComponentsInChildren<MonoBehaviour>();
                    foreach (var m in monos)
                    {
                        m.SendMessage("OnBackToScene", null, SendMessageOptions.DontRequireReceiver);
                    }
                }
            }
        }

        public bool IsTopScene(Scene scene)
        {
            // 履歴が空なら、今いるシーンを登録しちゃう
            if (_history.Count == 0) RegisterInitialScene();

            if (_history.Count > 0)
            {
                Scene topScene = _history.Peek();
                return topScene.handle == scene.handle;
            }
            return false;
        }

        /// <summary>
        /// シーンのロードや事前準備を管理するためのハンドル（SceneHandle）を同期的に生成して返します。
        /// ロードされた時点では履歴スタックへの追加はまだ行われません。
        /// </summary>
        public SceneHandle<TScene> CreateScene<TScene>() where TScene : MonoBehaviour
        {
            string sceneName = typeof(TScene).Name;
            return new SceneHandle<TScene>(sceneName, _sceneLoader);
        }

        /// <summary>
        /// 既にロード済みのシーンインスタンスを、現在の履歴（スタック）の最前面にPush（追加）します。
        /// </summary>
        public async Task PushInstanceAsync(Scene scene)
        {
            if (_isTransitioning)
            {
                Debug.LogWarning("[Ursa] 遷移中のため、PushInstanceAsync 要求を無視しました。");
                return;
            }

            if (_history.Count == 0) RegisterInitialScene();

            _history.Push(scene);
            await Task.CompletedTask;
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
            _isTransitioning = true;
            try
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
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        /// <summary>
        /// 対象のシーンをロードし、パラメーターを渡して開いた上で、
        /// そのシーンが閉じられて結果が返ってくるまで待機して値を返します。
        /// </summary>
        public async Task<TResult> OpenResultAsync<TScene, TParam, TResult>(TParam parameter)
            where TScene : SceneBase<TParam, TResult>
            where TParam : ISceneParameter
        {
            var handle = CreateScene<TScene>();
            
            // 内部で自動的にPreload（事前DL等）を実行する
            await handle.PreloadAsync();
            
            var sceneInstance = await handle.GetSceneAsync();
            if (sceneInstance == null) return default;

            await sceneInstance.OpenAsync(parameter);
            return await sceneInstance.CloseResultAsync();
        }

    }
}