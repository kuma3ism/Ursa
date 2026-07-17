using System.Threading.Tasks;
using UnityEngine;

namespace Ursa
{
    internal static class UrsaDontDestroyOnLoadRoot
    {
        private const string RootName = "[Ursa]";

        private static Transform _root;

        internal static void ResetStaticState()
        {
            _root = null;
        }

        public static Transform Ensure()
        {
            if (_root != null && _root.gameObject != null)
                return _root;

            var existing = GameObject.Find(RootName);
            if (existing != null)
                _root = existing.transform;

            if (_root == null)
            {
                var go = new GameObject(RootName);
                Object.DontDestroyOnLoad(go);
                _root = go.transform;
            }
            else
            {
                Object.DontDestroyOnLoad(_root.gameObject);
            }

            return _root;
        }

        public static void Attach(GameObject go)
        {
            if (go == null)
                return;

            Object.DontDestroyOnLoad(go);
            var root = Ensure();
            if (go.transform != root && go.transform.parent != root)
                go.transform.SetParent(root, false);
        }

        public static void DestroyOwnedRoot()
        {
            // Everything attached through UrsaDontDestroyOnLoadRoot.Attach is Ursa-owned runtime state.
            // UrsaCore.ResetAsync intentionally destroys this whole root so the boot scene starts clean.
            if (_root == null)
            {
                var existing = GameObject.Find(RootName);
                if (existing != null)
                    _root = existing.transform;
            }

            if (_root != null)
            {
                Object.Destroy(_root.gameObject);
                _root = null;
            }
        }

        internal static async Task WaitUntilOwnedRootDestroyedAsync()
        {
            for (var attempt = 0; attempt < 16; attempt++)
            {
                if (FindOwnedRoot() == null)
                    return;
                await Task.Yield();
            }
        }

        private static GameObject FindOwnedRoot()
        {
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var transform in transforms)
            {
                if (transform != null && transform.parent == null && transform.gameObject.name == RootName)
                    return transform.gameObject;
            }
            return null;
        }
    }
}
