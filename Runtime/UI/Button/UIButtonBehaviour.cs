using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Ursa.UI.Blocking;
using Ursa.UI.Core;
using Ursa.UI.Execution;

namespace Ursa.UI.Button
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class UIButtonBehaviour : MonoBehaviour, IUIButton
    {
        [SerializeField] private Button _button;

        private Func<Task> _handler = () => Task.CompletedTask;
        private IUIExecutionLock _lock;
        private IUIBlocker _blocker;

        private void Awake()
        {
            _button ??= GetComponent<Button>();

            if (!UrsaCore.TryResolve<IUIExecutionLock>(out _lock) ||
                !UrsaCore.TryResolve<IUIBlocker>(out _blocker))
            {
                throw new InvalidOperationException(
                    "Ursa UI services are not initialized. Call UrsaInitializer.Initialize() before using UIButtonBehaviour.");
            }

            _button.onClick.AddListener(InvokeHandler);
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(InvokeHandler);
        }

        public void SetOnClickAsync(Func<Task> handler)
        {
            _handler = handler ?? (() => Task.CompletedTask);
        }

        [Obsolete("Use SetOnClickAsync instead.")]
        public void OnClick(Func<Task> handler)
        {
            SetOnClickAsync(handler);
        }

        private void InvokeHandler() => _ = InvokeHandlerAsync();

        private async Task InvokeHandlerAsync()
        {
            if (_blocker.IsBlocked) return;
            if (!_lock.TryEnter(out var scope)) return;

            using (scope)
            {
                _blocker.Enter();
                try
                {
                    await _handler();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, this);
                }
                finally
                {
                    _blocker.Exit();
                }
            }
        }
    }
}
