using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using Ursa.UI;

namespace Ursa.Dialogs
{
    /// <summary>
    /// 戻り値を持つダイアログのベースクラス。
    /// SceneBase&lt;T&gt; と同じ思想で、TParam でパラメーター、TResult で結果の型を指定します。
    ///
    /// 【！重要！】Unityの仕様上、Awake() / Start() は OnOpenAsync より先に呼ばれます。
    /// パラメーターを使った初期化処理は必ず OnOpenAsync() に記述してください。
    /// </summary>
    public abstract class DialogBase<TParam, TResult> : MonoBehaviour,
        IDialogReceiverBase,
        IDialogReceiver<TParam, TResult>,
        IOpenDialog<TResult>,
        IDialogLifecycleEvents,
        IUrsaButtonBlockScope
        where TParam : IDialogParameter
    {
        [SerializeField] private bool _handleBackKey = true;

        // ---- IUrsaButtonBlockScope ----
        // このダイアログインスタンス自体がスコープなので、中の UrsaButton 同士はこのブロック状態を共有する。
        // ダイアログを開いたボタン自体は別のスコープ（呼び出し側の Canvas / Scene / Dialog）に
        // 属するので、OpenWithCloseAsync のように「閉じるまで待つ」呼び出し方でも
        // ダイアログ自体の OK/Cancel ボタンをブロックしない。
        private readonly UrsaButtonBlockState _buttonBlockState = new UrsaButtonBlockState();
        bool IUrsaButtonBlockScope.IsBlocked(float now) => _buttonBlockState.IsBlocked(now);
        void IUrsaButtonBlockScope.Begin() => _buttonBlockState.Begin();
        void IUrsaButtonBlockScope.End(float now) => _buttonBlockState.End(now);

        /// <summary>バックキー（Escape）による自動Close処理を有効/無効にします。</summary>
        protected void SetBackKeyEnabled(bool enabled) => _handleBackKey = enabled;

        /// <summary>現在のパラメーター。OnOpenAsync が呼ばれるまで null です。</summary>
        protected TParam CurrentParam { get; private set; }

        private TaskCompletionSource<TResult> _tcs;
        private CancellationTokenSource _cts;

        // ---- IDialogLifecycleEvents ----

        public event Action Opened;
        public event Action<DialogCloseReason> Closing;
        public event Action<DialogCloseReason> Closed;
        public event Action<float> PreloadProgress;

        // ---- IDialogReceiverBase（UrsaDialogManager から型なしで呼ばれる） ----

        async Task IDialogReceiverBase.OnOpenAsync(IDialogParameter param)
        {
            CurrentParam = (TParam)param;

            _cts = new CancellationTokenSource();
            _tcs = new TaskCompletionSource<TResult>();
            // CancellationToken と TCS を連携:
            // CloseAll 等でキャンセルされたら WaitForCloseAsync が OperationCanceledException をスローする
            _cts.Token.Register(
                () => _tcs.TrySetCanceled(_cts.Token),
                useSynchronizationContext: false);

            // IDialogResourcePreloader の進捗を PreloadProgress イベントへ中継
            if (param is IDialogResourcePreloader preloader)
                _ = preloader.PreloadResourcesAsync(
                    new Progress<float>(p => PreloadProgress?.Invoke(p)));

            Opened?.Invoke();
            await OnOpenAsync((TParam)param);
        }

        async Task IDialogReceiverBase.OnCloseAsync(DialogCloseReason reason)
        {
            Closing?.Invoke(reason);
            await OnCloseAsync(reason);
            Closed?.Invoke(reason);

            // Resolve 済みでなければ（CloseAll 等の強制終了）OperationCanceledException にする
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            (CurrentParam as IDialogResourceUnloader)?.UnloadResources();
        }

        // ---- IDialogReceiver<TParam>（型付き版・明示的実装） ----

        Task IDialogReceiver<TParam>.OnOpenAsync(TParam param) =>
            ((IDialogReceiverBase)this).OnOpenAsync(param);

        // ---- IDialogReceiver<TParam, TResult> ----

        void IDialogReceiver<TParam, TResult>.Resolve(TResult result) =>
            _tcs?.TrySetResult(result);

        void IDialogReceiver<TParam, TResult>.Reject(Exception error) =>
            _tcs?.TrySetException(error);

        // ---- IOpenDialog<TResult> ----

        /// <summary>
        /// ユーザー操作で閉じるまで待機します。<br/>
        /// CloseAll 等の強制終了時は OperationCanceledException をスローします。
        /// </summary>
        public Task<TResult> WaitForCloseAsync()
        {
            if (_tcs == null)
                throw new InvalidOperationException(
                    "[Ursa] WaitForCloseAsync は OpenAsync の後に呼んでください。");
            return _tcs.Task;
        }

        // ---- 派生クラス向け override ----

        /// <summary>
        /// ダイアログが開かれた際の初期化処理を記述します。
        /// パラメーターはこのメソッド内で参照してください（Awake/Start より後に呼ばれます）。
        /// </summary>
        protected virtual Task OnOpenAsync(TParam param) => Task.CompletedTask;

        /// <summary>ダイアログが閉じられる直前に呼ばれます。</summary>
        protected virtual Task OnCloseAsync(DialogCloseReason reason) => Task.CompletedTask;

        // ---- Close API（派生クラスから呼ぶ） ----

        /// <summary>
        /// 結果を返してこのダイアログを閉じます。<br/>
        /// WaitForCloseAsync に result が渡されます。
        /// </summary>
        protected async Task CloseAsync(TResult result)
        {
            // 先に Resolve してから CloseTop を呼ぶ
            // → OnCloseAsync 内の TrySetCanceled が Resolve 済み TCS に対して空振りする
            _tcs?.TrySetResult(result);
            await UrsaCore.Dialog.CloseTopAsync(DialogCloseReason.Submit);
        }

        /// <summary>
        /// 結果なしでこのダイアログを閉じます。<br/>
        /// WaitForCloseAsync は OperationCanceledException をスローします。
        /// </summary>
        protected async Task CloseAsync(DialogCloseReason reason = DialogCloseReason.Programmatic)
        {
            await UrsaCore.Dialog.CloseTopAsync(reason);
        }

        // ---- バックキー ----

        private void LateUpdate()
        {
            if (!_handleBackKey) return;
            if (UrsaCore.Dialog?.IsTransitioning == true) return;
            if (!UrsaCore.Dialog.IsTopDialog(this)) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                _ = OnBackKeyPressed();
        }

        /// <summary>
        /// バックキー（Escape / Android バックキー）が押された際の処理。<br/>
        /// デフォルトは BackKey 理由で CloseAsync します。
        /// </summary>
        protected virtual async Task OnBackKeyPressed()
        {
            await CloseAsync(DialogCloseReason.BackKey);
        }

        protected virtual void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }

    /// <summary>
    /// 戻り値不要なダイアログのベースクラス（Unit を TResult に使います）。<br/>
    /// 通知ダイアログ・ローディングダイアログなどに向いています。
    /// </summary>
    public abstract class DialogBase<TParam> : DialogBase<TParam, Unit>
        where TParam : IDialogParameter
    {
        /// <summary>このダイアログを閉じます。</summary>
        protected new async Task CloseAsync(DialogCloseReason reason = DialogCloseReason.Programmatic)
        {
            await UrsaCore.Dialog.CloseTopAsync(reason);
        }
    }
}
