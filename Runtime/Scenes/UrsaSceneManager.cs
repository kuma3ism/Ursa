using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Ursa;
using Ursa.Transitions;

namespace Ursa.Scenes
{
    /// <summary>
    /// 履歴エントリの実装
    /// </summary>
    internal class SceneHistoryEntry : ISceneHistoryEntry
    {
        public int Index { get; set; }
        public string SceneName { get; set; }
        public Type SceneType { get; set; }
        public Scene Scene { get; set; }
    }

    /// <summary>
    /// Ursaフレームワークにおける、シーンのロード・アンロードおよび履歴（スタック）管理を行う実体クラス。
    /// ISceneManagerの実装であり、シングルトンとして管理されることが想定されています。
    /// </summary>
    public class UrsaSceneManager : ISceneManager
    {
        private bool _isTransitioning;
        public bool IsTransitioning => _isTransitioning;

        // 履歴スタック（index 0 が最も古い = bottom）
        private List<SceneHistoryEntry> _history = new List<SceneHistoryEntry>();

        /// <summary>
        /// エディター上でのみ使用されるデフォルトの ISceneLoader。
        /// Ursa.Editor の EditorSceneLoaderInstaller が InitializeOnLoad で自動登録します。
        /// ユーザーが明示的にローダーを渡した場合はそちらが優先されます。
        /// </summary>
        public static ISceneLoader DefaultEditorLoader { get; set; }

        private readonly ISceneLoader _sceneLoader;
        private readonly IUrsaLogger _logger;

        // Buffers to avoid allocations
        private readonly List<GameObject> _rootGameObjectBuffer = new List<GameObject>();
        private readonly List<ISceneBackHandler> _backHandlerBuffer = new List<ISceneBackHandler>();

        public UrsaSceneManager(ISceneLoader sceneLoader = null, IUrsaLogger logger = null)
        {
            _sceneLoader = sceneLoader ?? DefaultEditorLoader ?? new BuildSettingsSceneLoader();
#if URSA_LOG
            _logger = logger ?? new UnityDebugLogger();
#else
            _logger = logger ?? new NullUrsaLogger();
#endif
        }

        /// <inheritdoc />
        public IReadOnlyList<ISceneHistoryEntry> History => _history;

        // 遷移フラグ管理を共通化（TransitionCanvas のシングルトンを使用）
        private async Task ExecuteTransitionAsync(Func<Task> action, TransitionType transitionType = TransitionType.Default)
        {
            TransitionController manualController = null;
            bool shouldDestroyController = false;

            if (transitionType != TransitionType.Default)
            {
                var library = UrsaCore.TransitionLibrary;
                if (library != null)
                {
                    var prefab = library.GetPrefab(transitionType);
                    if (prefab != null)
                    {
                        manualController = UnityEngine.Object.Instantiate(prefab);
                        shouldDestroyController = true;
                    }
                }
            }

            var controller = manualController ?? GetActiveController();
            var canvas = controller != null ? TransitionCanvas.EnsureInstance() : null;
            canvas?.ApplyController(controller);

            _isTransitioning = true;
            try
            {
                if (canvas != null) await canvas.PlayOutAsync();
                await action();
                if (canvas != null) await canvas.PlayInAsync();
            }
            finally
            {
                _isTransitioning = false;
                if (shouldDestroyController && manualController != null)
                {
                    UnityEngine.Object.Destroy(manualController.gameObject);
                }
            }
        }

        // 現在最前面のシーンから TransitionController を取得（なければ null）
        private TransitionController GetActiveController()
        {
            if (_history.Count == 0) return null;
            var topEntry = _history[_history.Count - 1];
            var topScene = topEntry.Scene;
            if (!topScene.IsValid() || !topScene.isLoaded) return null;

            topScene.GetRootGameObjects(_rootGameObjectBuffer);
            TransitionController result = null;
            foreach (var go in _rootGameObjectBuffer)
            {
                var controller = go.GetComponentInChildren<TransitionController>();
                if (controller != null)
                {
                    result = controller;
                    break;
                }
            }
            _rootGameObjectBuffer.Clear();
            return result;
        }

        /// <summary>
        /// 全履歴を捨てて、新しいシーンへ遷移 (Single)
        /// </summary>
        public async Task ResetAsync<TScene>(ISceneParameter parameter, TransitionType transitionType = TransitionType.Default) where TScene : MonoBehaviour
        {
            if (_isTransitioning)
            {
                _logger.LogWarning("[Ursa] 遷移中のため、ResetAsync 要求を無視しました。");
                return;
            }

            _history.Clear();
            await InternalLoad(typeof(TScene).Name, typeof(TScene), parameter, LoadSceneMode.Single, transitionType);
        }

        /// <summary>
        /// 現在のシーンの上に重ねる (Additive)
        /// パラメーターの IsHistory が false の場合、履歴には積まれません。
        /// </summary>
        public async Task PushAsync<TScene>(ISceneParameter parameter, TransitionType transitionType = TransitionType.Default) where TScene : MonoBehaviour
        {
            if (_isTransitioning)
            {
                _logger.LogWarning("[Ursa] 遷移中のため、PushAsync 要求を無視しました。");
                return;
            }

            if (_history.Count == 0) RegisterInitialScene();
            await InternalLoad(typeof(TScene).Name, typeof(TScene), parameter, LoadSceneMode.Additive, transitionType);
        }

        /// <summary>
        /// 現在の最前面シーンを捨てて、新しいシーンに入れ替える
        /// </summary>
        public async Task ReplaceAsync<TScene>(ISceneParameter parameter, TransitionType transitionType = TransitionType.Default) where TScene : MonoBehaviour
        {
            if (_isTransitioning)
            {
                _logger.LogWarning("[Ursa] 遷移中のため、ReplaceAsync 要求を無視しました。");
                return;
            }

            await ExecuteTransitionAsync(async () =>
            {
                // 1. 今の一番上を取り出す
                if (_history.Count > 0)
                {
                    var oldEntry = _history[_history.Count - 1];
                    _history.RemoveAt(_history.Count - 1);
                    if (oldEntry.Scene.IsValid() && oldEntry.Scene.isLoaded)
                    {
                        _logger.Log($"<color=orange>[Ursa]</color> Replacing: {oldEntry.Scene.name}");
                        await _sceneLoader.UnloadSceneAsync(oldEntry.Scene);
                    }
                }

                // 2. 新しいシーンをロードする
                string sceneName = typeof(TScene).Name;
                Task<Scene> sceneLoadTask = _sceneLoader.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                Task resourceLoadTask = (parameter as ISceneResourcePreloader)?.PreloadResourcesAsync(null) ?? Task.CompletedTask;

                await Task.WhenAll(sceneLoadTask, resourceLoadTask);
                Scene newlyLoadedScene = await sceneLoadTask;

                // 3. 新しいシーンを登録する（IsHistory に関わらずReplaceは必ず履歴に入る）
                if (newlyLoadedScene.IsValid())
                    PushHistory(newlyLoadedScene, typeof(TScene));

                if (parameter != null) await InjectParameterToScene(newlyLoadedScene, parameter);
            }, transitionType);
        }

        /// <summary>
        /// 一つ前のシーンに戻る
        /// </summary>
        public async Task PopAsync(TransitionType transitionType = TransitionType.Default)
        {
            if (_isTransitioning || _history.Count <= 1)
            {
                _logger.LogWarning("[Ursa] 戻る先のシーンがない、もしくは遷移中です。");
                return;
            }

            await ExecuteTransitionAsync(async () =>
            {
                var entry = _history[_history.Count - 1];
                _history.RemoveAt(_history.Count - 1);
                if (entry.Scene.IsValid() && entry.Scene.isLoaded)
                {
                    _logger.Log($"<color=cyan>[Ursa]</color> Pop: {entry.Scene.name}");
                    await _sceneLoader.UnloadSceneAsync(entry.Scene);
                }
                NotifyBackToScene();
            }, transitionType);
        }

        /// <summary>
        /// 履歴内で最も直近にある TScene 型のシーンまで一気にPopします。
        /// 対象が見つからない場合は InvalidOperationException をスローします。
        /// </summary>
        public async Task JumpToAsync<TScene>(TransitionType transitionType = TransitionType.Default) where TScene : MonoBehaviour
        {
            var targetType = typeof(TScene);

            // 末尾から検索（最も直近）。末尾 = 最前面なので一つ手前から探す
            int targetIndex = -1;
            for (int i = _history.Count - 2; i >= 0; i--)
            {
                if (_history[i].SceneType == targetType)
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex < 0)
                throw new InvalidOperationException(
                    $"[Ursa] JumpToAsync: 履歴に {targetType.Name} が見つかりませんでした。");

            await JumpToIndexAsync(targetIndex, transitionType);
        }

        /// <summary>
        /// 指定インデックスのシーンまで一気にPopします（インデックス0が最も古い）。
        /// 範囲外の場合は ArgumentOutOfRangeException をスローします。
        /// </summary>
        public async Task JumpToIndexAsync(int index, TransitionType transitionType = TransitionType.Default)
        {
            if (index < 0 || index >= _history.Count)
                throw new ArgumentOutOfRangeException(nameof(index),
                    $"[Ursa] JumpToIndexAsync: index {index} は範囲外です（履歴数: {_history.Count}）。");

            if (_isTransitioning)
            {
                _logger.LogWarning("[Ursa] 遷移中のため、JumpToIndexAsync 要求を無視しました。");
                return;
            }

            await ExecuteTransitionAsync(async () =>
            {
                // index より上にあるシーンを全部アンロード
                while (_history.Count - 1 > index)
                {
                    var entry = _history[_history.Count - 1];
                    _history.RemoveAt(_history.Count - 1);
                    if (entry.Scene.IsValid() && entry.Scene.isLoaded)
                    {
                        _logger.Log($"<color=cyan>[Ursa]</color> JumpTo Pop: {entry.Scene.name}");
                        await _sceneLoader.UnloadSceneAsync(entry.Scene);
                    }
                }
                NotifyBackToScene();
            }, transitionType);
        }

        private async Task InternalLoad(string sceneName, Type sceneType, ISceneParameter parameter, LoadSceneMode mode, TransitionType transitionType = TransitionType.Default)
        {
            await ExecuteTransitionAsync(async () =>
            {
                _logger.Log($"<color=cyan>[Ursa]</color> Loading: {sceneName} ({mode})");

                Task<Scene> sceneLoadTask = _sceneLoader.LoadSceneAsync(sceneName, mode);
                Task resourceLoadTask = (parameter as ISceneResourcePreloader)?.PreloadResourcesAsync(null) ?? Task.CompletedTask;

                await Task.WhenAll(sceneLoadTask, resourceLoadTask);
                Scene newlyLoadedScene = await sceneLoadTask;

                if (!newlyLoadedScene.IsValid()) return;

                // IsHistory が false の場合は履歴に積まない
                bool addToHistory = parameter == null || parameter.IsHistory;
                if (addToHistory)
                    PushHistory(newlyLoadedScene, sceneType);

                if (parameter != null)
                    await InjectParameterToScene(newlyLoadedScene, parameter);
            }, transitionType);
        }

        private void PushHistory(Scene scene, Type sceneType)
        {
            _history.Add(new SceneHistoryEntry
            {
                Index = _history.Count,
                SceneName = scene.name,
                SceneType = sceneType,
                Scene = scene,
            });
            // インデックスを振り直す
            for (int i = 0; i < _history.Count; i++)
                _history[i].Index = i;
        }

        private void RegisterInitialScene()
        {
            if (_history.Count > 0) return;

            Scene active = SceneManager.GetActiveScene();
            PushHistory(active, null);
            _logger.Log($"<color=cyan>[Ursa]</color> Initial scene '{active.name}' registered (Handle: {active.handle})");
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

            Scene activeScene = _history[_history.Count - 1].Scene;
            if (activeScene.IsValid() && activeScene.isLoaded)
            {
                activeScene.GetRootGameObjects(_rootGameObjectBuffer);
                foreach (var go in _rootGameObjectBuffer)
                {
                    go.GetComponentsInChildren<ISceneBackHandler>(true, _backHandlerBuffer);
                    foreach (var handler in _backHandlerBuffer)
                        handler.OnResumeScene();
                    _backHandlerBuffer.Clear();
                }
                _rootGameObjectBuffer.Clear();
            }
        }

        public bool IsTopScene(Scene scene)
        {
            if (_history.Count == 0) RegisterInitialScene();
            return _history[_history.Count - 1].Scene.handle == scene.handle;
        }

        /// <summary>
        /// 指定したシーンをロード（Additive）し、対象となるTSceneコンポーネントのインスタンスを検索して返します。
        /// ロードされた時点では履歴スタックへの追加はまだ行われません。
        /// </summary>
        public async Task<TScene> CreateSceneAsync<TScene>(ISceneParameter parameter = null, TransitionType transitionType = TransitionType.Default) where TScene : MonoBehaviour
        {
            if (_isTransitioning)
            {
                _logger.LogWarning("[Ursa] 遷移中のため、CreateSceneAsync 要求を無視しました。");
                return null;
            }

            string sceneName = typeof(TScene).Name;
            TScene result = null;

            await ExecuteTransitionAsync(async () =>
            {
                _logger.Log($"<color=cyan>[Ursa]</color> Loading Instance: {sceneName}");

                Task<Scene> sceneLoadTask = _sceneLoader.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                Task resourceLoadTask = (parameter as ISceneResourcePreloader)?.PreloadResourcesAsync(null) ?? Task.CompletedTask;

                await Task.WhenAll(sceneLoadTask, resourceLoadTask);
                Scene newlyLoadedScene = await sceneLoadTask;

                if (!newlyLoadedScene.IsValid())
                {
                    _logger.LogWarning($"[Ursa] {sceneName} シーンのロードに失敗しました。");
                    return;
                }

                newlyLoadedScene.GetRootGameObjects(_rootGameObjectBuffer);
                foreach (var go in _rootGameObjectBuffer)
                {
                    var comp = go.GetComponentInChildren<TScene>(true);
                    if (comp != null)
                    {
                        result = comp;
                        _rootGameObjectBuffer.Clear();
                        return;
                    }
                }
                _rootGameObjectBuffer.Clear();

                _logger.LogWarning($"[Ursa] {sceneName} シーンから {typeof(TScene).Name} が見つかりませんでした。");
            }, transitionType);

            return result;
        }

        /// <summary>
        /// 既にロード済みのシーンインスタンスを、現在の履歴（スタック）の最前面にPush（追加）します。
        /// </summary>
        public Task PushInstanceAsync(Scene scene, TransitionType transitionType = TransitionType.Default)
        {
            if (_isTransitioning)
            {
                _logger.LogWarning("[Ursa] 遷移中のため、PushInstanceAsync 要求を無視しました。");
                return Task.CompletedTask;
            }

            if (_history.Count == 0) RegisterInitialScene();
            PushHistory(scene, null);
            return Task.CompletedTask;
            // Note: Currently PushInstanceAsync doesn't support transition because it's additive and usually immediate.
            // If transition is needed, we should wrap PushHistory in ExecuteTransitionAsync.
        }

        /// <summary>
        /// 既にロード済みのシーンインスタンスを、現在の最前面のシーンと入れ替え（Replace）て履歴を更新します。
        /// </summary>
        public async Task ReplaceInstanceAsync(Scene scene, TransitionType transitionType = TransitionType.Default)
        {
            if (_isTransitioning)
            {
                _logger.LogWarning("[Ursa] 遷移中のため、ReplaceInstanceAsync 要求を無視しました。");
                return;
            }

            await ExecuteTransitionAsync(async () =>
            {
                if (_history.Count > 0)
                {
                    var oldEntry = _history[_history.Count - 1];
                    _history.RemoveAt(_history.Count - 1);
                    if (oldEntry.Scene.IsValid() && oldEntry.Scene.isLoaded)
                    {
                        _logger.Log($"<color=orange>[Ursa]</color> Replacing Instance: {oldEntry.Scene.name}");
                        await _sceneLoader.UnloadSceneAsync(oldEntry.Scene);
                    }
                }
                PushHistory(scene, null);
            }, transitionType);
        }

        /// <summary>
        /// 対象のシーンをロードし、パラメーターを渡して開いた上で、
        /// そのシーンが閉じられて結果が返ってくるまで待機して値を返します。
        /// </summary>
        public async Task<TResult> OpenResultAsync<TScene, TParam, TResult>(TParam parameter, TransitionType transitionType = TransitionType.Default)
            where TScene : SceneBaseWithResult<TParam, TResult>
            where TParam : ISceneParameter
        {
            var sceneInstance = await CreateSceneAsync<TScene>(parameter, transitionType);
            if (sceneInstance == null) return default;

            await sceneInstance.OpenAsync(parameter);
            return await sceneInstance.WaitForResultAsync();
        }
    }
}
