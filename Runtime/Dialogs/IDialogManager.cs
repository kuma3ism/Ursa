using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Ursa
{
    /// <summary>
    /// ダイアログに渡すパラメーターのベースインターフェース
    /// </summary>
    public interface IDialogParameter
    {
        /// <summary>このダイアログを履歴（スタック）に積むかどうか。デフォルトは true。</summary>
        bool IsHistory => true;

        /// <summary>バリア（背景）タップで閉じることを許可するか。デフォルトは false。</summary>
        bool BarrierDismissible => false;

        /// <summary>ダイアログの配置先。デフォルトは Scene。</summary>
        DialogPlacement Placement => DialogPlacement.Scene;
    }

    /// <summary>ダイアログの配置先を指定します。</summary>
    public enum DialogPlacement
    {
        /// <summary>現在シーンに紐付いた配置（defaultParent に配置）</summary>
        Scene,
        /// <summary>DontDestroyOnLoad として維持（システムエラー表示など）</summary>
        DontDestroyOnLoad,
    }

    /// <summary>
    /// ダイアログ開始前に必要リソースの事前DLを行うインターフェース。
    /// IDialogParameter に合わせて実装すると、Open と並行して自動実行されます。
    /// </summary>
    public interface IDialogResourcePreloader
    {
        /// <param name="progress">進捗通知。null の場合は通知なし。値は 0.0〜1.0。</param>
        Task PreloadResourcesAsync(IProgress<float> progress = null);
    }

    /// <summary>
    /// ダイアログ破棄時にリソースの解放を自動実行するインターフェース。
    /// IDialogReceiverBase.OnCloseAsync 内で自動的に呼び出されます。
    /// </summary>
    public interface IDialogResourceUnloader
    {
        void UnloadResources();
    }

    /// <summary>履歴スタック上の1エントリを表すインターフェース</summary>
    public interface IDialogHistoryEntry
    {
        int Index { get; }
        string DialogName { get; }
        Type DialogType { get; }
    }

    /// <summary>戻り値なしダイアログで使う空の値型</summary>
    public readonly struct Unit { }

    /// <summary>ダイアログが閉じられた理由</summary>
    public enum DialogCloseReason
    {
        Programmatic,
        BackKey,
        BarrierTap,
        Submit,
        Cancel,
        Timeout,
    }

    /// <summary>
    /// UrsaDialogManager から型なしで OnOpenAsync / OnCloseAsync を呼ぶための非ジェネリック契約。
    /// ISceneReceiver と同じ役割。
    /// </summary>
    public interface IDialogReceiverBase
    {
        Task OnOpenAsync(IDialogParameter param);
        Task OnCloseAsync(DialogCloseReason reason);
    }

    /// <summary>
    /// 型付きパラメーターを受け取る契約（ISceneReceiver&lt;T&gt; と同じ分担）
    /// </summary>
    public interface IDialogReceiver<TParam> : IDialogReceiverBase
        where TParam : IDialogParameter
    {
        Task OnOpenAsync(TParam param);
    }

    /// <summary>結果付きの型付きパラメーターを受け取る契約</summary>
    public interface IDialogReceiver<TParam, TResult> : IDialogReceiver<TParam>
        where TParam : IDialogParameter
    {
        void Resolve(TResult result);
        void Reject(Exception error);
    }

    /// <summary>
    /// ユーザー操作で閉じるまで await できる契約。
    /// CloseAll 等の強制終了時は OperationCanceledException をスローします。
    /// </summary>
    public interface IOpenDialog<TResult>
    {
        Task<TResult> WaitForCloseAsync();
    }

    /// <summary>
    /// ダイアログのライフサイクルイベントを外部へ公開する契約。
    /// SceneBase の ISceneBackHandler と同じ思想。
    /// </summary>
    public interface IDialogLifecycleEvents
    {
        event Action Opened;
        event Action<DialogCloseReason> Closing;
        event Action<DialogCloseReason> Closed;
        event Action<float> PreloadProgress;
    }

    /// <summary>ダイアログの生成・破棄・履歴管理を行うマネージャーのインターフェース</summary>
    public interface IDialogManager
    {
        /// <summary>Open/Close 処理中かどうかを返します。</summary>
        bool IsTransitioning { get; }

        /// <summary>現在表示中のダイアログが1件以上あるかどうかを返します。</summary>
        bool HasAnyDialog { get; }

        /// <summary>現在の履歴スタックを古い順（インデックス0が最も古い）で返します。</summary>
        IReadOnlyList<IDialogHistoryEntry> History { get; }

        /// <summary>指定したダイアログインスタンスが現在最前面（履歴のトップ）かどうかを返します。</summary>
        bool IsTopDialog(MonoBehaviour dialog);

        /// <summary>
        /// ダイアログを生成してオープンします。<br/>
        /// parameter が IDialogResourcePreloader を実装していれば生成と並行してプリロードが走ります。<br/>
        /// 同種ダイアログの多重表示は常に許可されます。
        /// </summary>
        Task<TDialog> OpenAsync<TDialog, TResult>(
            IDialogParameter parameter,
            CancellationToken ct = default)
            where TDialog : MonoBehaviour, IDialogReceiverBase, IOpenDialog<TResult>, IDialogLifecycleEvents;

        /// <summary>戻り値なし（Unit）ダイアログのショートハンド</summary>
        Task<TDialog> OpenAsync<TDialog>(
            IDialogParameter parameter,
            CancellationToken ct = default)
            where TDialog : MonoBehaviour, IDialogReceiverBase, IOpenDialog<Unit>, IDialogLifecycleEvents;

        /// <summary>
        /// Open → configure → WaitForClose を1行で行うショートハンド。<br/>
        /// configure は非同期 UI 初期化にも対応するため Func&lt;TDialog, Task&gt;。<br/>
        /// CloseAll 等の強制終了時は OperationCanceledException をスローします。
        /// </summary>
        Task<TResult> OpenWithCloseAsync<TDialog, TResult>(
            IDialogParameter parameter,
            Func<TDialog, Task> configure = null,
            CancellationToken ct = default)
            where TDialog : MonoBehaviour, IDialogReceiverBase, IOpenDialog<TResult>, IDialogLifecycleEvents;

        Task CloseTopAsync(DialogCloseReason reason = DialogCloseReason.Programmatic);
        Task CloseAllAsync(DialogCloseReason reason = DialogCloseReason.Programmatic);
    }
}
