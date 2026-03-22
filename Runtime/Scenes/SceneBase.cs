using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ursa.Scenes
{
    /// <summary>
    /// 戻り値を持たない、標準的なシーンのベースクラス。
    /// 一方通行の画面遷移や、結果を返す必要のないベース画面等で使用します。
    /// </summary>
    public abstract class SceneBase<T> : MonoBehaviour, ISceneReceiver<T>, ISceneBackHandler where T : ISceneParameter
    {
        [SerializeField] private bool _handleBackKey = true;

        /// <summary>バックキー（Escape）による自動戻り処理を有効/無効にします。</summary>
        protected void SetBackKeyEnabled(bool enabled) => _handleBackKey = enabled;

        protected T CurrentParam { get; private set; }

        protected bool IsTopScene => UrsaCore.Scene.IsTopScene(this.gameObject.scene);

        // 型なし ISceneReceiver の明示的実装（UrsaSceneManager からリフレクション不要で呼べる）
        async Task ISceneReceiver.OnEnterScene(ISceneParameter parameter)
        {
            CurrentParam = (T)parameter;
            await OnInitializeAsync((T)parameter);
        }

        // 型あり ISceneReceiver<T> の明示的実装
        async Task ISceneReceiver<T>.OnEnterScene(T parameter)
        {
            CurrentParam = parameter;
            await OnInitializeAsync(parameter);
        }

        /// <summary>
        /// サブクラスでシーン固有の初期化処理を記述するためのメソッド。
        /// OpenAsync / OnEnterScene から呼ばれます。
        ///
        /// 【！重要！】Unityの仕様上、Awake() や Start() はこのメソッドよりも先に呼ばれます。
        /// その時点では CurrentParam はまだ null のため、パラメータを使った初期化はここに記述してください。
        /// </summary>
        protected virtual async Task OnInitializeAsync(T parameter)
        {
            await Task.CompletedTask;
        }

        /// <summary>
        /// すでにロード済みのこのシーンインスタンスを履歴（スタック）の最前面にPushし、
        /// パラメーターを渡して初期化処理を開始します。
        /// </summary>
        public async Task OpenAsync(T parameter)
        {
            CurrentParam = parameter;
            await UrsaCore.Scene.PushInstanceAsync(this.gameObject.scene);
            await OnInitializeAsync(parameter);
        }

        /// <summary>
        /// すでにロード済みのこのシーンインスタンスを現在の最前面のシーンと入れ替え（Replace）し、
        /// パラメーターを渡して初期化処理を開始します。
        /// </summary>
        public async Task ReplaceAsync(T parameter)
        {
            CurrentParam = parameter;
            await UrsaCore.Scene.ReplaceInstanceAsync(this.gameObject.scene);
            await OnInitializeAsync(parameter);
        }

        protected virtual void OnDestroy()
        {
            var unloader = this.CurrentParam as ISceneResourceUnloader;
            unloader?.UnloadResources();
        }

        /// <summary>
        /// 現在最前面にある自分自身のシーンを破棄し、一つ前のシーンに戻ります。
        /// </summary>
        public async Task CloseAsync()
        {
            await UrsaCore.Scene.PopAsync();
        }

        /// <summary>
        /// 前面に重なっていた別のシーンが閉じられ、再びこのシーンが最前面（アクティブ）になった際に呼ばれます。
        /// </summary>
        public virtual void OnResumeScene()
        {
            // 子供が消えて自分が最前面になった時に呼ばれる
        }

        private void LateUpdate()
        {
            if (!_handleBackKey || !IsTopScene) return;
            if (UrsaCore.Scene?.IsTransitioning == true) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                OnBackKeyPressed();
        }

        /// <summary>
        /// Androidのバックキー（Escape）が押された際の処理。
        /// デフォルトでは CloseAsync() を呼び出します。
        /// 必要に応じてサブクラスでオーバーライドしてください。
        /// </summary>
        protected virtual void OnBackKeyPressed()
        {
            _ = CloseAsync();
        }
    }

    /// <summary>
    /// ユーザーの選択結果や処理データなど、呼び出し元に戻り値（結果）を返すシーンのベースクラス。
    /// ポップアップダイアログや、確認画面などで使用します。
    /// </summary>
    public abstract class SceneBaseWithResult<TParam, TResult> : SceneBase<TParam>
        where TParam : ISceneParameter
    {
        private TaskCompletionSource<TResult> _tcs;

        protected override async Task OnInitializeAsync(TParam parameter)
        {
            _tcs = new TaskCompletionSource<TResult>();
            await base.OnInitializeAsync(parameter);
        }

        /// <summary>
        /// 引数なしで閉じる場合も default(TResult) をセットして正しく閉じます。
        /// </summary>
        public new async Task CloseAsync()
        {
            await CloseAsync(default(TResult));
        }

        /// <summary>
        /// 呼び出し元へ戻り値をセットし、自分自身を閉じて一つ前のシーンに戻ります。
        /// （シーン自身が「自身を閉じる」アクションとして呼び出します）
        /// </summary>
        public async Task CloseAsync(TResult result = default)
        {
            await UrsaCore.Scene.PopAsync();
            _tcs?.TrySetResult(result);
        }

        /// <summary>
        /// このシーンが閉じられ、結果が返ってくるまで待機します。
        /// （呼び出し元のシーンが「結果を待つ」アクションとして呼び出します）
        /// 必ず OpenAsync() を呼んだ後に使用してください。
        /// </summary>
        public Task<TResult> WaitForResultAsync()
        {
            if (_tcs == null)
                throw new System.InvalidOperationException(
                    "[Ursa] WaitForResultAsync() は OpenAsync() を呼んだ後に使用してください。");
            return _tcs.Task;
        }

        /// <summary>
        /// Androidのバックキーによるキャンセル時は default(TResult) で閉じます。
        /// キャンセル時の戻り値を変えたい場合はオーバーライドしてください。
        /// </summary>
        protected override void OnBackKeyPressed()
        {
            _ = CloseAsync();
        }
    }
}