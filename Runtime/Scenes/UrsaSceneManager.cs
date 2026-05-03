using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Ursa.Scenes;
using Ursa.Transitions;

namespace Ursa
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
        private readonly List<ISceneManagerReceiver> _managerReceiverBuffer = new List<ISceneManagerReceiver>();
        private readonly List<ISceneReceiver> _sceneReceiverBuffer = new List<ISceneReceiver>();
        private readonly List<TransitionController> _transitionControllerBuffer = new List<TransitionController>();

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

        private async Task ExecuteTransitionAsync(Func<Task> action, string transitionName = TransitionType.Fade)
        {
            TransitionEffectBase manualEffect = null;
            bool shouldDestroyEffect = false;

            if (!string.IsNullOrEmpty(transitionName))
            {
                var settings = UrsaCore.Settings;
                if (settings != null)
                {
                    var prefab = settings.GetTransitionPrefab(transitionName);
                    if (prefab != null)
                    {
                        manualEffect = UnityEngine.Object.Instantiate(prefab);
                        shouldDestroyEffect = true;
                    }
                }
            }

            // マニュアル指定のエフェクトが優先。無ければシーンにある TransitionController を探す
            var canvas = TransitionCanvas.EnsureInstance();
            if (manualEffect != null)
            {
                canvas.ApplyEffect(manualEffect);
            }
            else
            {
                var controller = GetActiveController();
                canvas.ApplyController(controller);
            }

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
                if (shouldDestroyEffect && manualEffect != null)
                {
                    UnityEngine.Object.Destroy(manualEffect.gameObject);
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

            try
            {
                topScene.GetRootGameObjects(_rootGameObjectBuffer);
                TransitionController result = null;
                for (int i = 0; i < _rootGameObjectBuffer.Count; i++)
                {
                    var go = _rootGameObjectBuffer[i];
                    if (go == null) continue;

                    try
                    {
                        go.GetComponentsInChildren<TransitionController>(false, _transitionControllerBuffer);
                        if (_transitionControllerBuffer.Count > 0)
                        {
                            result = _transitionControllerBuffer[0];
                            break;
                        }
                    }
                    finally
                    {
                        _transitionControllerBuffer.Clear();
                    }
                }
                return result;
            }
            finally
            {
                _rootGameObjectBuffer.Clear();
            }
        }

        /// <summary>
        /// 全履歴を捨てて、新しいシーンへ遷移 (Single)
        /// </summary>
        public async Task ResetAsync<TScene>(ISceneParameter parameter, string transitionName = TransitionType.Fade) where TScene : MonoBehaviour
        {
            if (_isTransitioning)
            {
                _logger.LogWarning("[Ursa] 遷移中のため、ResetAsync 要求を無視しました。");
                return;
            }

            _history.Clear();
            await InternalLoad(typeof(TScene).Name, typeof(TScene), parameter, LoadSceneMode.Single, transitionName);
        }

        /// <summary>
        /// 現在のシーンの上に重ねる (Additive)
        /// パラメーターの IsHistory が false の場合、履歴には積まれません。
        /// </summary>
        public async Task PushAsync<TScene>(ISceneParameter parameter, string transitionName = TransitionType.Fade) where TScene : MonoBehaviour
        {
            if (_isTransitioning)
            {
                _logger.LogWarning("[Ursa] 遷移中のため、PushAsync 要求を無視しました。");
                return;
            }

            if (_history.Count == 0) RegisterInitialScene();
            NotifyPauseScene();
            await InternalLoad(typeof(TScene).Name, typeof(TScene), parameter, LoadSceneMode.Additive, transitionName);
        }

        /// <summary>
        /// 現在の最前面シーンを捨てて、新しいシーンに入れ替える
        /// </summary>
        public async Task ReplaceAsync<TScene>(ISceneParameter parameter, string transitionName = TransitionType.Fade) where TScene : MonoBehaviour
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
                InjectSceneManager(newlyLoadedScene);
            }, transitionName);
        }

        /// <summary>
        /// 一つ前のシーンに戻る
        /// </summary>
        public async Task PopAsync(string transitionName = TransitionType.Fade)
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
            }, transitionName);
        }

        /// <summary>
        /// 履歴内で最も直近にある TScene 型のシーンまで一気にPopします。
        /// 対象が見つからない場合は InvalidOperationException をスローします。
        /// </summary>
        public async Task JumpToAsync<TScene>(string transitionName = TransitionType.Fade) where TScene : MonoBehaviour
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

            await JumpToIndexAsync(targetIndex, transitionName);
        }

        /// <summary>
        /// 指定インデックスのシーンまで一気にPopします（インデックス0が最も古い）。
        /// 範囲外の場合は ArgumentOutOfRangeException をスローします。
        /// </summary>
        public async Task JumpToIndexAsync(int index, string transitionName = TransitionType.Fade)
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
            }, transitionName);
        }

        private async Task InternalLoad(string sceneName, Type sceneType, ISceneParameter parameter, LoadSceneMode mode, string transitionName = TransitionType.Fade)
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

                InjectSceneManager(newlyLoadedScene);
            }, transitionName);
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
            var receiverList = new List<ISceneReceiver>();

            try
            {
                targetScene.GetRootGameObjects(_rootGameObjectBuffer);
                for (int i = 0; i < _rootGameObjectBuffer.Count; i++)
                {
                    var go = _rootGameObjectBuffer[i];
                    if (go == null) continue;

                    go.GetComponentsInChildren<ISceneReceiver>(false, _sceneReceiverBuffer);
                    for (int j = 0; j < _sceneReceiverBuffer.Count; j++)
                    {
                        var receiver = _sceneReceiverBuffer[j];
                        if (receiver != null)
                        {
                            receiverList.Add(receiver);
                        }
                    }
                    _sceneReceiverBuffer.Clear();
                }
            }
            finally
            {
                _rootGameObjectBuffer.Clear();
            }

            for (int i = 0; i < receiverList.Count; i++)
            {
                await receiverList[i].OnEnterScene(parameter);
            }
        }

        /// <summary>
        /// ロードしたシーン内の ISceneManagerReceiver に自分自身を注入します。
        /// これにより SceneBase は UrsaCore を参照せずに ISceneManager の機能を使えます。
        /// </summary>
        private void InjectSceneManager(Scene scene)
        {
            try
            {
                scene.GetRootGameObjects(_rootGameObjectBuffer);
                for (int i = 0; i < _rootGameObjectBuffer.Count; i++)
                {
                    var go = _rootGameObjectBuffer[i];
                    if (go == null) continue;

                    try
                    {
                        go.GetComponentsInChildren<ISceneManagerReceiver>(true, _managerReceiverBuffer);
                        for (int j = 0; j < _managerReceiverBuffer.Count; j++)
                        {
                            var receiver = _managerReceiverBuffer[j];
                            if (receiver != null)
                            {
                                receiver.SetManager(this);
                            }
                        }
                    }
                    finally
                    {
                        _managerReceiverBuffer.Clear();
                    }
                }
            }
            finally
            {
                _rootGameObjectBuffer.Clear();
            }
        }

        private void NotifyBackToScene()
        {
            if (_history.Count == 0) return;

            Scene activeScene = _history[_history.Count - 1].Scene;
            if (activeScene.IsValid() && activeScene.isLoaded)
            {
                try
                {
                    activeScene.GetRootGameObjects(_rootGameObjectBuffer);
                    for (int i = 0; i < _rootGameObjectBuffer.Count; i++)
                    {
                        var go = _rootGameObjectBuffer[i];
                        if (go == null) continue;

                        try
                        {
                            go.GetComponentsInChildren<ISceneBackHandler>(true, _backHandlerBuffer);
                            for (int j = 0; j < _backHandlerBuffer.Count; j++)
                            {
                                var handler = _backHandlerBuffer[j];
                                if (handler != null)
                                {
                                    handler.OnResumeScene();
                                }
                            }
                        }
                        finally
                        {
                            _backHandlerBuffer.Clear();
                        }
                    }
                }
                finally
                {
                    _rootGameObjectBuffer.Clear();
                }
            }
        }

        private void NotifyPauseScene()
        {
            if (_history.Count == 0) return;

            Scene topScene = _history[_history.Count - 1].Scene;
            if (topScene.IsValid() && topScene.isLoaded)
            {
                try
                {
                    topScene.GetRootGameObjects(_rootGameObjectBuffer);
                    for (int i = 0; i < _rootGameObjectBuffer.Count; i++)
                    {
                        var go = _rootGameObjectBuffer[i];
                        if (go == null) continue;

                        try
                        {
                            go.GetComponentsInChildren<ISceneBackHandler>(true, _backHandlerBuffer);
                            for (int j = 0; j < _backHandlerBuffer.Count; j++)
                            {
                                var handler = _backHandlerBuffer[j];
                                if (handler != null)
                                {
                                    handler.OnPauseScene();
                                }
                            }
                        }
                        finally
                        {
                            _backHandlerBuffer.Clear();
                        }
                    }
                }
                finally
                {
                    _rootGameObjectBuffer.Clear();
                }
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
        public async Task<TScene> CreateSceneAsync<TScene>(ISceneParameter parameter = null, string transitionName = TransitionType.Fade) where TScene : MonoBehaviour
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
            }, transitionName);

            return result;
        }

        /// <summary>
        /// 既にロード済みのシーンインスタンスを、現在の履歴（スタック）の最前面にPush（追加）します。
        /// </summary>
        public Task PushInstanceAsync(Scene scene, string transitionName = TransitionType.Fade)
        {
            if (_isTransitioning)
            {
                _logger.LogWarning("[Ursa] 遷移中のため、PushInstanceAsync 要求を無視しました。");
                return Task.CompletedTask;
            }

            if (!string.IsNullOrEmpty(transitionName))
                _logger.LogWarning("[Ursa] PushInstanceAsync はトランジション演出に未対応です。transitionName は無視されます。");

            if (_history.Count == 0) RegisterInitialScene();
            NotifyPauseScene();
            PushHistory(scene, null);
            InjectSceneManager(scene);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 既にロード済みのシーンインスタンスを、現在の最前面のシーンと入れ替え（Replace）て履歴を更新します。
        /// </summary>
        public async Task ReplaceInstanceAsync(Scene scene, string transitionName = TransitionType.Fade)
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
                InjectSceneManager(scene);
            }, transitionName);
        }
    }
}
