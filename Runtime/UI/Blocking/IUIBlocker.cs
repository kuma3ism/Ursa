namespace Ursa.UI
{
    /// <summary>
    /// UI 操作全体をブロックするインターフェース。
    /// 参照カウント方式で Enter/Exit をペアで呼ぶことを想定しています。
    /// </summary>
    public interface IUIBlocker
    {
        /// <summary>1 つ以上の Enter が呼ばれ、対応する Exit がまだの場合 true。</summary>
        bool IsBlocked { get; }

        /// <summary>ブロック参照カウントを 1 増やします。</summary>
        void Enter();

        /// <summary>
        /// ブロック参照カウントを 1 減らします。
        /// Enter より多く呼ばれた場合は InvalidOperationException をスローします。
        /// </summary>
        void Exit();
    }
}
