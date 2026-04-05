namespace Ursa.UI
{
    /// <summary>
    /// UI ランタイムの実行制御サービスへのアクセスを提供するインターフェース。
    /// UrsaCore.Initialize(IUIManager) で登録し、UrsaCore.UI からアクセスします。
    /// </summary>
    public interface IUIManager
    {
        /// <summary>ボタン等の非同期ハンドラーの二重実行を防ぐ実行ロック。</summary>
        IUIExecutionLock ExecutionLock { get; }

        /// <summary>画面遷移中などに UI 操作全体をブロックするブロッカー。</summary>
        IUIBlocker Blocker { get; }
    }
}
