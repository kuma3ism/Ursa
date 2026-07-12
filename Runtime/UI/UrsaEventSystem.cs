using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Ursa.UI
{
    internal static class UrsaEventSystem
    {
        private const string EventSystemName = "[Ursa] EventSystem";
        private static bool _requested;
        private static bool _afterFirstSceneLoad;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureAfterFirstSceneLoad()
        {
            _afterFirstSceneLoad = true;
            if (_requested || UrsaCore.IsSceneReady || UrsaCore.IsDialogReady || UrsaCore.IsUIReady)
                EnsureExists();
        }

        public static void RequestEnsureExists()
        {
            _requested = true;
            if (_afterFirstSceneLoad)
                EnsureExists();
        }

        private static void EnsureExists()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
                return;

            if (Object.FindAnyObjectByType<EventSystem>() != null)
                return;

            var go = new GameObject(EventSystemName);
            Object.DontDestroyOnLoad(go);
            go.AddComponent<EventSystem>();

            var inputModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputModuleType != null)
                go.AddComponent(inputModuleType);
            else
                go.AddComponent<StandaloneInputModule>();
        }
    }
}
