using UnityEngine;

namespace Ursa.UI
{
    /// <summary>
    /// UrsaButtonGroup / Dialog / Scene のいずれも祖先に見つからない場合のフォールバックスコープ。
    ///
    /// 必要になったタイミングで UrsaButton が対象 GameObject（最寄りの Canvas、無ければ
    /// ボタン自身）に自動で AddComponent します。通常は明示的に触る必要はありません。
    /// </summary>
    internal sealed class UrsaButtonBlockAnchor : MonoBehaviour, IUrsaButtonBlockScope
    {
        private readonly UrsaButtonBlockState _state = new UrsaButtonBlockState();

        public bool IsBlocked(float now) => _state.IsBlocked(now);
        public void Begin() => _state.Begin();
        public void End(float now) => _state.End(now);
    }
}
