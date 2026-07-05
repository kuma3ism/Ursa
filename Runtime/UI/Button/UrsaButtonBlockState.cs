using UnityEngine;

namespace Ursa.UI
{
    /// <summary>
    /// IUrsaButtonBlockScope の実装ロジックをまとめたヘルパー。
    ///
    /// Dialog・Scene・UrsaButtonGroup など、スコープになる側のクラスはこれをフィールドとして持ち、
    /// IUrsaButtonBlockScope の3メソッドをそのまま委譲するだけで実装できます。
    ///
    /// 同一スコープ内で複数の UrsaButton が同時にハンドラーを実行するケース（長押し＋通常クリックの
    /// タイミング競合など）を考慮して、実行中カウントで管理します。
    /// </summary>
    public sealed class UrsaButtonBlockState
    {
        private int _runningCount;
        private float _blockedUntil = float.MinValue;

        public bool IsBlocked(float now) => _runningCount > 0 || now < _blockedUntil;

        public void Begin() => _runningCount++;

        public void End(float now)
        {
            _runningCount = Mathf.Max(0, _runningCount - 1);
            _blockedUntil = now + UrsaButton.GroupBlockBuffer;
        }
    }
}
