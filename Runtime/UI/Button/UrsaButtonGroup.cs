using UnityEngine;

namespace Ursa.UI
{
    /// <summary>
    /// このTransform配下のUrsaButton同士をブロックし合うグループとしてマークします。
    /// Dialog / Scene のどちらでもない、任意のUIの塊（パネル等）をひとまとめにブロックしたい場合に使います。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UrsaButtonGroup : MonoBehaviour, IUrsaButtonBlockScope
    {
        private readonly UrsaButtonBlockState _state = new UrsaButtonBlockState();

        public bool IsBlocked(float now) => _state.IsBlocked(now);
        public void Begin() => _state.Begin();
        public void End(float now) => _state.End(now);
    }
}
