namespace Ursa.UI
{
    /// <summary>
    /// IUIManager のデフォルト実装。
    /// UIExecutionLock と SimpleUIBlocker を組み合わせて提供します。
    /// </summary>
    public sealed class UrsaUIManager : IUIManager
    {
        public IUIExecutionLock ExecutionLock { get; }
        public IUIBlocker Blocker { get; }

        /// <summary>カスタム実装を注入する場合に使います。</summary>
        public UrsaUIManager(IUIExecutionLock executionLock, IUIBlocker blocker)
        {
            ExecutionLock = executionLock ?? throw new System.ArgumentNullException(nameof(executionLock));
            Blocker = blocker ?? throw new System.ArgumentNullException(nameof(blocker));
        }

        /// <summary>デフォルト実装（UIExecutionLock + SimpleUIBlocker）で初期化します。</summary>
        public UrsaUIManager() : this(new UIExecutionLock(), new SimpleUIBlocker()) { }
    }
}
