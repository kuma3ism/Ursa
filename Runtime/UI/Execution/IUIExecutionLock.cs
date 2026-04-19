using System;

namespace Ursa.UI
{
    /// <summary>
    /// 非再入の実行ロックインターフェース。
    /// ボタンの async ハンドラーが二重に実行されないよう保護します。
    /// </summary>
    public interface IUIExecutionLock
    {
        /// <summary>
        /// ロックの取得を試みます。
        /// 取得に成功した場合は true を返し、scope を Dispose することでロックを解放します。
        /// 既にロック中の場合は false を返します（scope は null）。
        /// </summary>
        bool TryEnter(out IDisposable scope);
    }
}
