using System.Threading.Tasks;
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

        public static void ResetOwnedEventSystem()
        {
            var eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var eventSystem in eventSystems)
            {
                if (eventSystem != null && eventSystem.gameObject.name == EventSystemName)
                    Object.Destroy(eventSystem.gameObject);
            }
        }

        public static async Task WaitUntilOwnedEventSystemDestroyedAsync()
        {
            for (var i = 0; i < 16; i++)
            {
                if (FindOwnedEventSystem() == null)
                    return;

                await Task.Yield();
            }
        }

        private static void EnsureExists()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
                return;

            if (FindAnyEventSystem() != null)
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

        private static EventSystem FindOwnedEventSystem()
        {
            var eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var eventSystem in eventSystems)
            {
                if (eventSystem != null && eventSystem.gameObject.name == EventSystemName)
                    return eventSystem;
            }

            return null;
        }

        private static EventSystem FindAnyEventSystem()
        {
            var eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var eventSystem in eventSystems)
            {
                if (eventSystem != null)
                    return eventSystem;
            }

            return null;
        }
    }
}
