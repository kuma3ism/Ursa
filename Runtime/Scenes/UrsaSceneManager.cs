using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Ursa;

namespace Ursa.Scenes
{
    public class UrsaSceneManager : ISceneManager
    {
        private bool _isTransitioning;
        private Dictionary<string, Scene> _loadedScenes = new Dictionary<string, Scene>();
        private Stack<string> _history = new Stack<string>();

        /// <summary>
        /// 全履歴を捨てて、新しいシーンへ遷移 (Single)
        /// </summary>
        public async Task ResetAsync<TScene>(ISceneParameter parameter) where TScene : MonoBehaviour
        {
            if (_isTransitioning) return;

            _history.Clear();
            _loadedScenes.Clear();

            await InternalLoad(typeof(TScene).Name, parameter, LoadSceneMode.Single);
        }

        /// <summary>
        /// 現在のシーンの上に重ねる (Additive)
        /// </summary>
        public async Task PushAsync<TScene>(ISceneParameter parameter) where TScene : MonoBehaviour
        {
            if (_isTransitioning) return;

            if (_history.Count == 0) RegisterInitialScene();

            await InternalLoad(typeof(TScene).Name, parameter, LoadSceneMode.Additive);
        }

        /// <summary>
        /// 現在の最前面シーンを捨てて、新しいシーンに入れ替える
        /// </summary>
        public async Task ReplaceAsync<TScene>(ISceneParameter parameter) where TScene : MonoBehaviour
        {
            if (_isTransitioning) return;
            _isTransitioning = true; // 遷移開始

            try
            {
                // 1. 今の一番上を取り出す
                if (_history.Count > 1)
                {
                    string currentKey = _history.Pop(); // スタックから抜く
                    if (_loadedScenes.TryGetValue(currentKey, out Scene oldScene))
                    {
                        Debug.Log($"<color=orange>[Ursa]</color> Replacing: {oldScene.name}");
                        var unloadOp = SceneManager.UnloadSceneAsync(oldScene);
                        if (unloadOp != null)
                        {
                            while (!unloadOp.isDone) await Task.Yield();
                        }
                        _loadedScenes.Remove(currentKey);
                    }
                }

                // 2. 新しいシーンをロードする
                string sceneName = typeof(TScene).Name;
                var loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                while (!loadOp.isDone) await Task.Yield();

                // 3. 新しいシーンを登録する
                Scene newlyLoadedScene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
                string newGuid = Guid.NewGuid().ToString().Substring(0, 8);
                
                _loadedScenes.Add(newGuid, newlyLoadedScene);
                _history.Push(newGuid); // 新しいキーを積む

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
                Debug.LogWarning("[Ursa] 戻る先のシーンがありません。");
                return;
            }

            _isTransitioning = true;
            try
            {
                string currentKey = _history.Pop();
                if (_loadedScenes.TryGetValue(currentKey, out Scene scene))
                {
                    Debug.Log($"<color=cyan>[Ursa]</color> Pop: {scene.name}");
                    var op = SceneManager.UnloadSceneAsync(scene);
                    if (op != null)
                    {
                        while (!op.isDone) await Task.Yield();
                    }
                    _loadedScenes.Remove(currentKey);
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
                var op = SceneManager.LoadSceneAsync(sceneName, mode);
                if (op == null) return;

                while (!op.isDone) await Task.Yield();

                Scene newlyLoadedScene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
                string guid = Guid.NewGuid().ToString().Substring(0, 8);
                
                _loadedScenes.Add(guid, newlyLoadedScene);
                _history.Push(guid);

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

        private void RegisterInitialScene()
        {
            // すでに履歴があるなら何もしない
            if (_history.Count > 0) return;

            Scene active = SceneManager.GetActiveScene();
            // 固定のキーで登録
            string rootKey = "ROOT";
            
            _loadedScenes.Add(rootKey, active);
            _history.Push(rootKey);
            
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

            string topKey = _history.Peek();
            if (_loadedScenes.TryGetValue(topKey, out Scene activeScene))
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

            string topKey = _history.Peek();
            if (_loadedScenes.TryGetValue(topKey, out Scene topScene))
            {
                return topScene.handle == scene.handle;
            }
            return false;
        }

        // --- 新規追加: インスタンスベース機能 ---

        public async Task<TScene> CreateSceneAsync<TScene>() where TScene : MonoBehaviour
        {
            string sceneName = typeof(TScene).Name;
            _isTransitioning = true;
            try
            {
                Debug.Log($"<color=cyan>[Ursa]</color> Loading Instance: {sceneName}");
                var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                if (op != null)
                {
                    while (!op.isDone) await Task.Yield();
                }
                
                Scene newlyLoadedScene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
                foreach (var go in newlyLoadedScene.GetRootGameObjects())
                {
                    var comp = go.GetComponentInChildren<TScene>(true);
                    if (comp != null) return comp;
                }
                
                Debug.LogWarning($"[Ursa] {sceneName} シーンから {typeof(TScene).Name} が見つかりませんでした。");
                return null;
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        public async Task PushInstanceAsync(Scene scene)
        {
            if (_history.Count == 0) RegisterInitialScene();

            string guid = Guid.NewGuid().ToString().Substring(0, 8);
            _loadedScenes.Add(guid, scene);
            _history.Push(guid);
            await Task.CompletedTask;
        }

        public async Task ReplaceInstanceAsync(Scene scene)
        {
            _isTransitioning = true;
            try
            {
                if (_history.Count > 0)
                {
                    string currentKey = _history.Pop();
                    if (_loadedScenes.TryGetValue(currentKey, out Scene oldScene))
                    {
                        Debug.Log($"<color=orange>[Ursa]</color> Replacing Instance: {oldScene.name}");
                        var unloadOp = SceneManager.UnloadSceneAsync(oldScene);
                        if (unloadOp != null)
                        {
                            while (!unloadOp.isDone) await Task.Yield();
                        }
                        _loadedScenes.Remove(currentKey);
                    }
                }

                string newGuid = Guid.NewGuid().ToString().Substring(0, 8);
                _loadedScenes.Add(newGuid, scene);
                _history.Push(newGuid);
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        public async Task<TResult> OpenResultAsync<TScene, TParam, TResult>(TParam parameter) 
            where TScene : SceneBase<TParam, TResult> 
            where TParam : ISceneParameter
        {
            var sceneInstance = await CreateSceneAsync<TScene>();
            if (sceneInstance == null) return default;
            
            await sceneInstance.OpenAsync(parameter);
            return await sceneInstance.CloseResultAsync();
        }

    }
}