using System;

namespace Ursa.UI.Blocking
{
    /// <summary>
    /// Reference-count blocker to avoid accidental premature unlock.
    /// </summary>
    public sealed class SimpleUIBlocker : IUIBlocker
    {
        private int _count;

        public bool IsBlocked => _count > 0;

        public void Enter()
        {
            checked { _count++; }
        }

        public void Exit()
        {
            if (_count == 0) return;
            _count--;
        }
    }
}
