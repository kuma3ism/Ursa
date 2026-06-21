using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ursa.UI
{
    /// <summary>
    /// UrsaButton の公開 API を表すインターフェース。
    /// Presenter や UseCase からはこのインターフェースを介して操作することを推奨します。
    /// </summary>
    public interface IUrsaButton
    {
        /// <summary>同期ハンドラーを登録します。</summary>
        void SetOnClick(Action handler);

        /// <summary>非同期ハンドラーを登録します。</summary>
        void SetOnClick(Func<CancellationToken, Task> handler);

        /// <summary>
        /// 実行時に外部の CancellationTokenSource を取得し、UrsaButton 内部の CancellationToken とリンクさせます。
        /// 外部 CTS が再生成される場合に利用します。
        /// </summary>
        /// <param name="externalCtsProvider">実行時に外部 CancellationTokenSource を返すファクトリ。</param>
        /// <param name="handler">リンクされた CancellationToken を受け取る非同期ハンドラー。</param>
        void SetOnClick(Func<CancellationTokenSource> externalCtsProvider, Func<CancellationToken, Task> handler);

        /// <summary>このボタンの連打防止インターバル（秒）を設定します。</summary>
        void SetGateInterval(float seconds);

        /// <summary>グローバルボタンブロックを無視するかどうかを設定します。</summary>
        void SetIgnoreGlobalBlock(bool ignore);

        /// <summary>非同期の長押し（ロングクリック）ハンドラーを登録します。</summary>
        void SetOnLongClickAsync(float duration, Action<float> onHolding = null, Func<CancellationToken, Task> onHoldComplete = null);

        /// <summary>同期の長押し（ロングクリック）ハンドラーを登録します。</summary>
        void SetOnLongClick(float duration, Action<float> onHolding = null, Action onHoldComplete = null);
    }
}