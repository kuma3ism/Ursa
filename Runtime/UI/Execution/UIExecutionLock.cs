using System;
using System.Threading;

namespace Ursa.UI.Execution
{
    public sealed class UIExecutionLock : IUIExecutionLock
    {
        private int _isRunning;

        public bool TryEnter(out IDisposable scope)
        {
            if (Interlocked.Exchange(ref _isRunning, 1) == 1)
            {
                scope = null;
                return false;
            }

            scope = new Scope(this);
            return true;
        }

        private void Exit()
        {
            Interlocked.Exchange(ref _isRunning, 0);
        }

        private sealed class Scope : IDisposable
        {
            private UIExecutionLock _owner;
            private bool _disposed;

            public Scope(UIExecutionLock owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _owner.Exit();
            }
        }
    }
}
