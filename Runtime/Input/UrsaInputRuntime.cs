using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ursa.Inputs
{
    [DefaultExecutionOrder(-32000)]
    internal sealed class UrsaInputRuntime : MonoBehaviour
    {
        private const string RuntimeName = "[Ursa] Input Runtime";

        private static readonly List<UrsaPointerDownEvent> PointerDownBuffer = new List<UrsaPointerDownEvent>(8);
        private static readonly LegacyUrsaInputProvider LegacyProvider = new LegacyUrsaInputProvider();

        private static UrsaInputRuntime _instance;
        private static int _backPressedFrame = -1;

        private bool _isDispatchingBack;
        private int _backDispatchedFrame = -1;

        internal static event Action<UrsaPointerDownEvent> PointerDown;

        internal static bool BackPressedThisFrame => _backPressedFrame == Time.frameCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            RequestEnsureExists();
        }

        internal static void RequestEnsureExists()
        {
            if (_instance != null)
                return;

            var existing = FindAnyObjectByType<UrsaInputRuntime>();
            if (existing != null)
            {
                _instance = existing;
                return;
            }

            var go = new GameObject(RuntimeName);
            UrsaDontDestroyOnLoadRoot.Attach(go);
            _instance = go.AddComponent<UrsaInputRuntime>();
        }

        internal static void ResetOwnedRuntimeObject()
        {
            _instance = null;
            _backPressedFrame = -1;
            PointerDownBuffer.Clear();
        }

        internal static void ResetStaticState()
        {
            ResetOwnedRuntimeObject();
            PointerDown = null;
            LegacyProvider.Reset();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void Update()
        {
            PointerDownBuffer.Clear();
            var provider = UrsaInputProviderRegistry.Current ?? LegacyProvider;
            provider.Poll(PointerDownBuffer, out var backPressed);

            if (backPressed)
                _backPressedFrame = Time.frameCount;

            for (var index = 0; index < PointerDownBuffer.Count; index++)
                PointerDown?.Invoke(PointerDownBuffer[index]);
        }

        private void LateUpdate()
        {
            if (!BackPressedThisFrame || _isDispatchingBack || _backDispatchedFrame == Time.frameCount)
                return;

            // One physical press must never close both a dialog and the scene behind it.
            _backDispatchedFrame = Time.frameCount;

            IUrsaBackHandler selected = null;
            var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var behaviour in behaviours)
            {
                if (!(behaviour is IUrsaBackHandler candidate) || !candidate.CanHandleBack())
                    continue;
                if (selected == null || candidate.BackPriority > selected.BackPriority)
                    selected = candidate;
            }

            if (selected != null)
                _ = DispatchBackAsync(selected);
        }

        private async System.Threading.Tasks.Task DispatchBackAsync(IUrsaBackHandler handler)
        {
            _isDispatchingBack = true;
            try
            {
                await handler.HandleBackAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                _isDispatchingBack = false;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
