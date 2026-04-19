using System;

namespace Ursa.UI
{
    /// <summary>
    /// IUIBlocker の標準実装。参照カウント方式。
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
            if (_count <= 0)
                throw new InvalidOperationException(
                    "[Ursa] IUIBlocker.Exit() が Enter() より多く呼ばれています。Enter/Exit の対応を確認してください。");
            _count--;
        }
    }
}
