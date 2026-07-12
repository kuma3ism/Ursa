using UnityEngine;

namespace Ursa
{
    internal static class UrsaDontDestroyOnLoadRoot
    {
        private const string RootName = "[Ursa]";

        private static Transform _root;

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
    }
}
