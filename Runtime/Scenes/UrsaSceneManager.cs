using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Ursa.Scenes;
using Ursa.Transitions;
using Ursa.UI;

namespace Ursa
{
    /// <summary>
    /// 履歴エントリの実装
    /// </summary>
    internal enum SceneHistoryKind
    {
        Root,
        Push,
        Replace
    }

    internal class SceneHistoryEntry : ISceneHistoryEntry
    {
        public int Index { get; set; }
        public string SceneName { get; set; }
        public Type SceneType { get; set; }
        public Scene Scene { get; set; }
        public UrsaScenePresentation Presentation { get; set; }
        public ISceneParameter Parameter { get; set; }
        public SceneHistoryKind Kind { get; set; }
        public SceneHistoryEntry ReplacedEntry { get; set; }
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
        private readonly Dictionary<Component, bool> _hiddenComponentStates = new Dictionary<Component, bool>();
        private readonly List<Component> _hiddenComponentStatePurgeBuffer = new List<Component>();

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

        public Scene CurrentScene
        {
            get
            {
                RegisterInitialSceneIfNeeded();
                return _history.Count > 0 ? _history[_history.Count - 1].Scene : SceneManager.GetActiveScene();
            }
        }

        private async Task ExecuteTransitionAsync(Func<Task> action, string transitionName = TransitionType.Default)
        {
            TransitionEffectBase manualEffect = null;
            bool shouldDestroyEffect = false;
            var resolvedTransitionName = ResolveTransitionName(transitionName);
            bool transitionEnabled = !string.IsNullOrEmpty(resolvedTransitionName);

            if (transitionEnabled)
            {
                var settings = UrsaCore.Settings;
                if (settings != null)
                {
                    var prefab = settings.GetTransitionPrefab(resolvedTransitionName);
                    if (prefab != null)
                    {
                        manualEffect = UnityEngine.Object.Instantiate(prefab);
                        var effectCanvases = manualEffect.GetComponentsInChildren<Canvas>(true);
                        foreach (var effectCanvas in effectCanvases)
                            UrsaUICanvasUtility.ConfigureTransitionCanvas(effectCanvas);
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
                var controller = transitionEnabled ? GetActiveController() : null;
                canvas.ApplyController(controller);
            }

            _isTransitioning = true;
            try
            {
                if (canvas != null) await canvas.PlayOutAsync();
                float coveredStartTime = Time.realtimeSinceStartup;
                await action();
                if (canvas != null)
                    await WaitForMinimumCoveredDurationAsync(canvas.MinimumCoveredDuration, coveredStartTime);
                if (canvas != null) await canvas.PlayInAsync();
            }
            finally
            {
                _isTransitioning = false;
                if (shouldDestroyEffect && manualEffect != null)
                {
                    UnityEngine.Object.Destroy(manualEffect.gameObject);
                }
                // シーンのアンロード（Additive経由、あるいは LoadSceneMode.Single による
                // Unity側の自動破棄の両方）によって破棄された Component が
                // _hiddenComponentStates に残り続けないよう、遷移の都度掃除する。
                PurgeDestroyedHiddenComponentStates();
            }
        }

        private static async Task WaitForMinimumCoveredDurationAsync(float minimumCoveredDuration, float coveredStartTime)
        {
            if (minimumCoveredDuration <= 0f)
                return;

            while (Time.realtimeSinceStartup - coveredStartTime < minimumCoveredDuration)
                await Task.Yield();
        }

        private static string ResolveTransitionName(string transitionName)
        {
            var settings = UrsaCore.Settings;
            if (settings != null)
                return settings.ResolveSceneTransitionName(transitionName);

            return string.Equals(transitionName, TransitionType.Default, StringComparison.Ordinal)
                ? TransitionType.Fade
                : transitionName;
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
        public async Task ResetAsync<TScene>(ISceneParameter parameter, string transitionName = TransitionType.Default) where TScene : MonoBehaviour
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
        public async Task PushAsync<TScene>(ISceneParameter parameter, string transitionName = TransitionType.Default) where TScene : MonoBehaviour
        {
            if (_isTransitioning)
            {
                _logger.LogWarning("[Ursa] 遷移中のため、PushAsync 要求を無視しました。");
                return;
            }

            RegisterInitialSceneIfNeeded();
            NotifyPauseScene();
            await InternalLoad(typeof(TScene).Name, typeof(TScene), parameter, LoadSceneMode.Additive, transitionName);
        }

        /// <summary>
        /// 現在の最前面シーンを置き換え元として保持し、新しいシーンを差し替え表示する。
        /// ルートシーンだけは戻り先がないため、履歴ごと置き換える。
        /// </summary>
        public async Task ReplaceAsync<TScene>(ISceneParameter parameter, string transitionName = TransitionType.Default) where TScene : MonoBehaviour
        {
            if (_isTransitioning)
            {
                _logger.LogWarning("[Ursa] 遷移中のため、ReplaceAsync 要求を無視しました。");
                return;
            }

            RegisterInitialSceneIfNeeded();
            await ExecuteTransitionAsync(async () =>
            {
                // ルートシーンの差し替えはUnity標準のSingleロードを使う。
                // 履歴がある場合は現在のトップを置き換え元として保持し、新シーンを表示する。
                bool replacingRootScene = _history.Count == 1;
                string sceneName = typeof(TScene).Name;
                Task<Scene> sceneLoadTask = _sceneLoader.LoadSceneAsync(
                    sceneName,
                    replacingRootScene ? LoadSceneMode.Single : LoadSceneMode.Additive);
                Task resourceLoadTask = (parameter as ISceneResourcePreloader)?.PreloadResourcesAsync(null) ?? Task.CompletedTask;

                await Task.WhenAll(sceneLoadTask, resourceLoadTask);
                Scene newlyLoadedScene = await sceneLoadTask;
                if (!newlyLoadedScene.IsValid())
                    return;

                if (newlyLoadedScene.isLoaded)
                    SceneManager.SetActiveScene(newlyLoadedScene);

                if (replacingRootScene)
                {
                    _history.Clear();
                }
                else
                {
                    var replacedEntry = _history[_history.Count - 1];
                    NotifyPauseScene();
                    _history.RemoveAt(_history.Count - 1);
                    await UnloadEntrySceneAsync(replacedEntry);
                    PushHistory(newlyLoadedScene, typeof(TScene), GetPresentation(parameter), SceneHistoryKind.Replace, replacedEntry, parameter);
                }

                // 新しいシーンを登録する（IsHistory に関わらずReplaceは必ず履歴に入る）
                if (replacingRootScene && newlyLoadedScene.IsValid())
                    PushHistory(newlyLoadedScene, typeof(TScene), GetPresentation(parameter), SceneHistoryKind.Root, parameter: parameter);

                if (parameter != null) await InjectParameterToScene(newlyLoadedScene, parameter);
                InjectSceneManager(newlyLoadedScene);
                SyncSceneCanvases();
            }, transitionName);
        }

        /// <summary>
        /// 履歴スタックを1つPopします。
        /// Pushされたシーンなら閉じ、Replaceされたシーンなら置き換え元を復帰します。
        /// </summary>
        public async Task PopAsync(string transitionName = TransitionType.Default)
        {
            if (_isTransitioning || _history.Count <= 1)
            {
                _logger.LogWarning("[Ursa] 戻る先のシーンがない、もしくは遷移中です。");
                return;
            }

            await ExecuteTransitionAsync(async () =>
            {
                await PopTopEntryAsync();
            }, transitionName);
        }

        /// <summary>
        /// 現在の最前面シーンを閉じます。
        /// </summary>
        public async Task CloseAsync(string transitionName = TransitionType.Default)
        {
            if (_isTransitioning || _history.Count <= 1)
            {
                _logger.LogWarning("[Ursa] 閉じる先のシーンがない、もしくは遷移中です。");
                return;
            }

            await ExecuteTransitionAsync(async () =>
            {
                await CloseTopEntryAsync();
            }, transitionName);
        }

        /// <summary>
        /// 指定シーンからの「自分を閉じる」要求を処理します。
        /// </summary>
        public async Task CloseAsync(Scene scene, string transitionName = TransitionType.Default)
        {
            if (_isTransitioning || _history.Count <= 1)
            {
                _logger.LogWarning("[Ursa] 閉じる先のシーンがない、もしくは遷移中です。");
                return;
            }

            if (_history[_history.Count - 1].Scene.handle != scene.handle)
            {
                _logger.LogWarning($"[Ursa] CloseAsync: '{scene.name}' は最前面シーンではありません。");
                return;
            }

            await CloseAsync(transitionName);
        }

        /// <summary>
        /// 履歴内で最も直近にある TScene 型のシーンまで一気にPopします。
        /// 対象が見つからない場合は InvalidOperationException をスローします。
        /// </summary>
        public async Task JumpToAsync<TScene>(string transitionName = TransitionType.Default) where TScene : MonoBehaviour
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
        public async Task JumpToIndexAsync(int index, string transitionName = TransitionType.Default)
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
                    _logger.Log($"<color=cyan>[Ursa]</color> JumpTo Pop: {entry.Scene.name}");
                    await UnloadEntryTreeAsync(entry);
                }
                ReindexHistory();
                SyncSceneCanvases();
                NotifyBackToScene();
            }, transitionName);
        }

        private async Task InternalLoad(string sceneName, Type sceneType, ISceneParameter parameter, LoadSceneMode mode, string transitionName = TransitionType.Default)
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
                    PushHistory(newlyLoadedScene, sceneType, GetPresentation(parameter), _history.Count == 0 ? SceneHistoryKind.Root : SceneHistoryKind.Push, parameter: parameter);

                if (parameter != null)
                    await InjectParameterToScene(newlyLoadedScene, parameter);

                InjectSceneManager(newlyLoadedScene);
                if (addToHistory)
                {
                    SyncSceneCanvases();
                }
                else
                {
                    SyncSceneCanvasesForTransientScene(newlyLoadedScene, GetPresentation(parameter));
                }
            }, transitionName);
        }

        private void PushHistory(Scene scene, Type sceneType)
        {
            PushHistory(scene, sceneType, UrsaScenePresentation.Fullscreen, _history.Count == 0 ? SceneHistoryKind.Root : SceneHistoryKind.Push);
        }

        private void PushHistory(Scene scene, Type sceneType, UrsaScenePresentation presentation)
        {
            PushHistory(scene, sceneType, presentation, _history.Count == 0 ? SceneHistoryKind.Root : SceneHistoryKind.Push);
        }

        private void PushHistory(
            Scene scene,
            Type sceneType,
            UrsaScenePresentation presentation,
            SceneHistoryKind kind,
            SceneHistoryEntry replacedEntry = null,
            ISceneParameter parameter = null)
        {
            _history.Add(new SceneHistoryEntry
            {
                Index = _history.Count,
                SceneName = scene.name,
                SceneType = sceneType,
                Scene = scene,
                Presentation = presentation,
                Parameter = parameter,
                Kind = kind,
                ReplacedEntry = replacedEntry,
            });
            ReindexHistory();
        }

        private void ReindexHistory()
        {
            for (int i = 0; i < _history.Count; i++)
                _history[i].Index = i;
        }

        private async Task PopTopEntryAsync()
        {
            var entry = _history[_history.Count - 1];
            _history.RemoveAt(_history.Count - 1);

            if (entry.ReplacedEntry != null)
            {
                _logger.Log($"<color=cyan>[Ursa]</color> Close Replace: {entry.Scene.name}");
                await UnloadEntrySceneAsync(entry);

                await ReloadHistoryEntryAsync(entry.ReplacedEntry);
                _history.Add(entry.ReplacedEntry);
                ReindexHistory();
                if (entry.ReplacedEntry.Scene.IsValid() && entry.ReplacedEntry.Scene.isLoaded)
                    SceneManager.SetActiveScene(entry.ReplacedEntry.Scene);
            }
            else
            {
                _logger.Log($"<color=cyan>[Ursa]</color> Close: {entry.Scene.name}");
                await UnloadEntrySceneAsync(entry);
                ReindexHistory();
            }

            SyncSceneCanvases();
            NotifyBackToScene();
        }

        private async Task CloseTopEntryAsync()
        {
            var entry = _history[_history.Count - 1];
            _history.RemoveAt(_history.Count - 1);

            _logger.Log($"<color=cyan>[Ursa]</color> Close: {entry.Scene.name}");
            await UnloadEntrySceneAsync(entry);
            await UnloadEntryTreeAsync(entry.ReplacedEntry);
            ReindexHistory();
            SyncSceneCanvases();
            NotifyBackToScene();
        }

        private async Task UnloadEntryTreeAsync(SceneHistoryEntry entry)
        {
            if (entry == null)
                return;

            await UnloadEntrySceneAsync(entry);
            await UnloadEntryTreeAsync(entry.ReplacedEntry);
        }

        private async Task UnloadEntrySceneAsync(SceneHistoryEntry entry)
        {
            if (entry.Scene.IsValid() && entry.Scene.isLoaded)
                await _sceneLoader.UnloadSceneAsync(entry.Scene);
        }

        private async Task ReloadHistoryEntryAsync(SceneHistoryEntry entry)
        {
            if (entry == null)
                return;

            if (entry.Scene.IsValid() && entry.Scene.isLoaded)
                return;

            var sceneName = entry.SceneType != null ? entry.SceneType.Name : entry.SceneName;
            _logger.Log($"<color=cyan>[Ursa]</color> Reload Replace Source: {sceneName}");

            var scene = await _sceneLoader.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (!scene.IsValid())
                return;

            entry.Scene = scene;
            entry.SceneName = scene.name;

            if (entry.Parameter != null)
                await InjectParameterToScene(scene, entry.Parameter);
            InjectSceneManager(scene);
        }

        private void RegisterInitialSceneIfNeeded()
        {
            if (_history.Count > 0 || _isTransitioning) return;

            Scene active = SceneManager.GetActiveScene();
            if (!active.IsValid() || !active.isLoaded) return;

            PushHistory(active, ResolveSceneType(active));
            InjectSceneManager(active);
            SyncSceneCanvases();
            _logger.Log($"<color=cyan>[Ursa]</color> Initial scene '{active.name}' registered (Handle: {active.handle})");
        }

        private Type ResolveSceneType(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;

            scene.GetRootGameObjects(_rootGameObjectBuffer);
            foreach (var go in _rootGameObjectBuffer)
            {
                var behaviours = go.GetComponentsInChildren<MonoBehaviour>(true);
                foreach (var behaviour in behaviours)
                {
                    if (behaviour is ISceneReceiver)
                    {
                        _rootGameObjectBuffer.Clear();
                        return behaviour.GetType();
                    }
                }
            }

            _rootGameObjectBuffer.Clear();
            return null;
        }

        private void SyncSceneCanvases()
        {
            SyncSceneCanvases(default, UrsaScenePresentation.Overlay);
        }

        private void SyncSceneCanvasesForTransientScene(Scene transientScene, UrsaScenePresentation presentation)
        {
            SyncSceneCanvases(transientScene, presentation);
        }

        private void SyncSceneCanvases(Scene transientScene, UrsaScenePresentation transientPresentation)
        {
            bool coveredByFullscreen = transientScene.IsValid() && transientPresentation == UrsaScenePresentation.Fullscreen;
            for (int i = _history.Count - 1; i >= 0; i--)
            {
                var entry = _history[i];
                bool visible = !coveredByFullscreen;
                UrsaUICanvasUtility.SyncSceneCanvases(entry.Scene, i, visible);
                SetSceneVisualsVisible(entry.Scene, visible);
                if (entry.Presentation == UrsaScenePresentation.Fullscreen)
                    coveredByFullscreen = true;
            }

            if (transientScene.IsValid() && transientScene.isLoaded)
            {
                UrsaUICanvasUtility.SyncSceneCanvases(transientScene, _history.Count, true);
                SetSceneVisualsVisible(transientScene, true);
            }

            AttachUiCameraToTopmostVisibleScene(transientScene, transientPresentation);
        }

        private void SetSceneVisualsVisible(Scene scene, bool visible)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            scene.GetRootGameObjects(_rootGameObjectBuffer);
            foreach (var go in _rootGameObjectBuffer)
            {
                var cameras = go.GetComponentsInChildren<Camera>(true);
                foreach (var camera in cameras)
                    SetEnabled(camera, visible);

                var listeners = go.GetComponentsInChildren<AudioListener>(true);
                foreach (var listener in listeners)
                    SetEnabled(listener, visible);

                var renderers = go.GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in renderers)
                    SetEnabled(renderer, visible);

                var lights = go.GetComponentsInChildren<Light>(true);
                foreach (var light in lights)
                    SetEnabled(light, visible);

                // Volume (URP/HDRP の Post-processing Volume) はUnity.RenderPipelines.Core.Runtime に
                // 定義されているため、Built-in RP専用プロジェクト（Core RP Library未インストール）でも
                // コンパイルが通るようリフレクションで解決する。
                var volumeType = GetVolumeType();
                if (volumeType != null)
                {
                    var volumes = go.GetComponentsInChildren(volumeType, true);
                    foreach (var volumeObj in volumes)
                    {
                        if (volumeObj is Behaviour volumeBehaviour)
                            SetEnabled(volumeBehaviour, visible);
                    }
                }
            }
            _rootGameObjectBuffer.Clear();
        }

        private static bool _volumeTypeResolved;
        private static Type _volumeType;

        internal static void ResetStaticState()
        {
            _volumeTypeResolved = false;
            _volumeType = null;
        }

        /// <summary>
        /// UnityEngine.Rendering.Volume 型を、アセンブリへの直接参照無しで解決します。
        /// Core RP Library が存在しない環境（Built-in RP専用）では null を返します。
        /// </summary>
        private static Type GetVolumeType()
        {
            if (_volumeTypeResolved)
                return _volumeType;

            _volumeTypeResolved = true;
            _volumeType = Type.GetType("UnityEngine.Rendering.Volume, Unity.RenderPipelines.Core.Runtime");
            return _volumeType;
        }

        private void AttachUiCameraToTopmostVisibleScene(Scene transientScene, UrsaScenePresentation transientPresentation)
        {
            if (transientScene.IsValid() && transientScene.isLoaded)
            {
                var camera = FindEnabledCamera(transientScene);
                if (camera != null)
                {
                    UrsaUICamera.AttachToBaseCameraExclusive(camera);
                    return;
                }
            }

            bool coveredByFullscreen = transientScene.IsValid() && transientPresentation == UrsaScenePresentation.Fullscreen;
            for (int i = _history.Count - 1; i >= 0; i--)
            {
                var entry = _history[i];
                bool visible = !coveredByFullscreen;
                if (visible)
                {
                    var camera = FindEnabledCamera(entry.Scene);
                    if (camera != null)
                    {
                        UrsaUICamera.AttachToBaseCameraExclusive(camera);
                        return;
                    }
                }

                if (entry.Presentation == UrsaScenePresentation.Fullscreen)
                    coveredByFullscreen = true;
            }
        }

        private Camera FindEnabledCamera(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;

            Camera bestCamera = null;
            scene.GetRootGameObjects(_rootGameObjectBuffer);
            foreach (var go in _rootGameObjectBuffer)
            {
                var cameras = go.GetComponentsInChildren<Camera>(true);
                foreach (var camera in cameras)
                {
                    if (camera == null || !camera.enabled)
                        continue;

                    if (bestCamera == null || camera.depth > bestCamera.depth)
                        bestCamera = camera;
                }
            }
            _rootGameObjectBuffer.Clear();
            return bestCamera;
        }

        private void SetEnabled(Behaviour component, bool visible)
        {
            if (component == null) return;
            if (visible)
                RestoreEnabled(component);
            else
                HideEnabled(component, component.enabled, value => component.enabled = value);
        }

        private void SetEnabled(Renderer component, bool visible)
        {
            if (component == null) return;
            if (visible)
                RestoreEnabled(component);
            else
                HideEnabled(component, component.enabled, value => component.enabled = value);
        }

        private void HideEnabled(Component component, bool enabled, Action<bool> setEnabled)
        {
            if (!_hiddenComponentStates.ContainsKey(component))
                _hiddenComponentStates.Add(component, enabled);
            setEnabled(false);
        }

        private void RestoreEnabled(Behaviour component)
        {
            if (_hiddenComponentStates.TryGetValue(component, out bool enabled))
            {
                component.enabled = enabled;
                _hiddenComponentStates.Remove(component);
            }
        }

        private void RestoreEnabled(Renderer component)
        {
            if (_hiddenComponentStates.TryGetValue(component, out bool enabled))
            {
                component.enabled = enabled;
                _hiddenComponentStates.Remove(component);
            }
        }

        /// <summary>
        /// _hiddenComponentStates に残っている、既に破棄済み（Destroy済み）の Component の
        /// エントリを取り除く。
        ///
        /// SetSceneVisualsVisible で非表示にした Component が、再表示（RestoreEnabled）される前に
        /// シーンごとアンロード（Additive の UnloadSceneAsync、または LoadSceneMode.Single による
        /// Unity側の自動破棄）された場合、_hiddenComponentStates に破棄済み Component への
        /// キーが残り続けてしまう（メモリリーク）。Unity の Object は破棄後も == null が true を
        /// 返すため、それを利用してエントリを検出・除去する。
        /// </summary>
        private void PurgeDestroyedHiddenComponentStates()
        {
            if (_hiddenComponentStates.Count == 0)
                return;

            _hiddenComponentStatePurgeBuffer.Clear();
            foreach (var component in _hiddenComponentStates.Keys)
            {
                if (component == null)
                    _hiddenComponentStatePurgeBuffer.Add(component);
            }

            if (_hiddenComponentStatePurgeBuffer.Count == 0)
                return;

            foreach (var key in _hiddenComponentStatePurgeBuffer)
                _hiddenComponentStates.Remove(key);
            _hiddenComponentStatePurgeBuffer.Clear();
        }

        private UrsaScenePresentation GetPresentation(ISceneParameter parameter)
        {
            return parameter?.Presentation ?? UrsaScenePresentation.Fullscreen;
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

        /// <summary>
        /// ロードしたシーン内の ISceneManagerReceiver に自分自身を注入します。
        /// これにより SceneBase は UrsaCore を参照せずに ISceneManager の機能を使えます。
        /// </summary>
        private void InjectSceneManager(Scene scene)
        {
            scene.GetRootGameObjects(_rootGameObjectBuffer);
            foreach (var go in _rootGameObjectBuffer)
            {
                go.GetComponentsInChildren<ISceneManagerReceiver>(true, _managerReceiverBuffer);
                foreach (var receiver in _managerReceiverBuffer)
                    receiver.SetManager(this);
                _managerReceiverBuffer.Clear();
            }
            _rootGameObjectBuffer.Clear();
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

        private void NotifyPauseScene()
        {
            if (_history.Count == 0) return;

            Scene topScene = _history[_history.Count - 1].Scene;
            if (topScene.IsValid() && topScene.isLoaded)
            {
                topScene.GetRootGameObjects(_rootGameObjectBuffer);
                foreach (var go in _rootGameObjectBuffer)
                {
                    go.GetComponentsInChildren<ISceneBackHandler>(true, _backHandlerBuffer);
                    foreach (var handler in _backHandlerBuffer)
                        handler.OnPauseScene();
                    _backHandlerBuffer.Clear();
                }
                _rootGameObjectBuffer.Clear();
            }
        }

        public bool IsTopScene(Scene scene)
        {
            RegisterInitialSceneIfNeeded();
            if (_history.Count == 0)
                return SceneManager.GetActiveScene().handle == scene.handle;
            return _history[_history.Count - 1].Scene.handle == scene.handle;
        }

        /// <summary>
        /// 指定したシーンをロード（Additive）し、対象となるTSceneコンポーネントのインスタンスを検索して返します。
        /// ロードされた時点では履歴スタックへの追加はまだ行われません。
        /// </summary>
        public async Task<TScene> CreateSceneAsync<TScene>(ISceneParameter parameter = null, string transitionName = TransitionType.Default) where TScene : MonoBehaviour
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

                InjectSceneManager(newlyLoadedScene);
                SyncSceneCanvasesForTransientScene(newlyLoadedScene, UrsaScenePresentation.Overlay);
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
        public Task PushInstanceAsync(Scene scene, string transitionName = TransitionType.Default, UrsaScenePresentation presentation = UrsaScenePresentation.Fullscreen, ISceneParameter parameter = null)
        {
            if (_isTransitioning)
            {
                _logger.LogWarning("[Ursa] 遷移中のため、PushInstanceAsync 要求を無視しました。");
                return Task.CompletedTask;
            }

            if (!string.IsNullOrEmpty(transitionName) &&
                !string.Equals(transitionName, TransitionType.Default, StringComparison.Ordinal))
            {
                _logger.LogWarning("[Ursa] PushInstanceAsync はトランジション演出に未対応です。transitionName は無視されます。");
            }

            RegisterInitialSceneIfNeeded();
            NotifyPauseScene();
            PushHistory(scene, ResolveSceneType(scene), presentation, _history.Count == 0 ? SceneHistoryKind.Root : SceneHistoryKind.Push, parameter: parameter);
            InjectSceneManager(scene);
            SyncSceneCanvases();
            return Task.CompletedTask;
        }

        /// <summary>
        /// 既にロード済みのシーンインスタンスを、現在の最前面シーンを置き換え元として差し替え表示します。
        /// ルートシーンだけは戻り先がないため、履歴ごと置き換えます。
        /// </summary>
        public async Task ReplaceInstanceAsync(Scene scene, string transitionName = TransitionType.Default, UrsaScenePresentation presentation = UrsaScenePresentation.Fullscreen, ISceneParameter parameter = null)
        {
            if (_isTransitioning)
            {
                _logger.LogWarning("[Ursa] 遷移中のため、ReplaceInstanceAsync 要求を無視しました。");
                return;
            }

            RegisterInitialSceneIfNeeded();
            await ExecuteTransitionAsync(async () =>
            {
                if (scene.IsValid() && scene.isLoaded)
                    SceneManager.SetActiveScene(scene);

                bool replacingRootScene = _history.Count == 1;
                if (replacingRootScene)
                {
                    var oldEntry = _history[_history.Count - 1];
                    _history.Clear();
                    PushHistory(scene, ResolveSceneType(scene), presentation, SceneHistoryKind.Root, parameter: parameter);

                    if (oldEntry.Scene.handle != scene.handle &&
                        oldEntry.Scene.IsValid() &&
                        oldEntry.Scene.isLoaded)
                    {
                        _logger.Log($"<color=orange>[Ursa]</color> Replacing Instance: {oldEntry.Scene.name}");
                        await _sceneLoader.UnloadSceneAsync(oldEntry.Scene);
                    }
                }
                else
                {
                    var replacedEntry = _history[_history.Count - 1];
                    NotifyPauseScene();
                    _history.RemoveAt(_history.Count - 1);
                    await UnloadEntrySceneAsync(replacedEntry);
                    PushHistory(scene, ResolveSceneType(scene), presentation, SceneHistoryKind.Replace, replacedEntry, parameter);
                }

                InjectSceneManager(scene);
                SyncSceneCanvases();
            }, transitionName);
        }
    }
}
