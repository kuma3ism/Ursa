using System.Threading.Tasks;
using UnityEngine;

namespace Ursa.Scenes
{
    /// <summary>
    /// 戻り値を持たない、標準的なシーンのベースクラス。
    /// 一方通行の画面遷移や、結果を返す必要のないベース画面等で使用します。
    /// </summary>
    public abstract class SceneBase<T> : MonoBehaviour, ISceneReceiver<T> where T : ISceneParameter
    {
        [SerializeField] private bool _handleBackKey = true;

        protected T Parameter { get; private set; }

        protected bool IsTopScene => UrsaCore.Scene.IsTopScene(this.gameObject.scene);

        /// <summary>
        /// シーンがロードされた直後に呼ばれる初期化処理。
        /// 既存のインターフェースとの互換性のために実装されており、パラメーターの受け取りを行います。
        /// 
        /// 【！重要！】Unityの仕様上、Awake() や Start() はこの OpenAsync / OnEnterScene よりも先に（裏で）勝手に呼ばれます。
        /// その時点ではまだパラメーター (this.Parameter) は null であるため、設定値を使った描画・通信などの初期化処理は
        /// Start() ではなく、必ずこの OnEnterScene() の中に記述してください。（Awakeは非依存のボタン紐付け等のみ推奨）
        /// </summary>
        public virtual async Task OnEnterScene(T parameter)
        {
            this.Parameter = parameter;
            await Task.CompletedTask;
        }

        // 型なし ISceneReceiver の明示的実装（UrsaSceneManager からリフレクション不要で呼べる）
        async Task ISceneReceiver.OnEnterScene(ISceneParameter parameter)
        {
            await OnEnterScene((T)parameter);
        }

        /// <summary>
        /// すでにロード済みのこのシーンインスタンスを履歴（スタック）の最前面にPushし、
        /// パラメーターを渡して初期化処理を開始します。
        /// </summary>
        public virtual async Task OpenAsync(T parameter)
        {
            await UrsaCore.Scene.PushInstanceAsync(this.gameObject.scene);
            await OnEnterScene(parameter);
        }

        /// <summary>
        /// すでにロード済みのこのシーンインスタンスを現在の最前面のシーンと入れ替え（Replace）し、
        /// パラメーターを渡して初期化処理を開始します。
        /// </summary>
        public virtual async Task ReplaceAsync(T parameter)
        {
            await UrsaCore.Scene.ReplaceInstanceAsync(this.gameObject.scene);
            await OnEnterScene(parameter);
        }

        protected virtual void OnDestroy()
        {
            var unloader = this.Parameter as ISceneResourceUnloader;
            unloader?.UnloadResources();
        }

        /// <summary>
        /// 現在最前面にある自分自身のシーンを破棄し、一つ前のシーンに戻ります。
        /// </summary>
        public virtual async Task CloseAsync()
        {
            await UrsaCore.Scene.PopAsync();
        }

        /// <summary>
        /// 前面に重なっていた別のシーンが閉じられ、再びこのシーンが最前面（アクティブ）になった際に呼ばれます。
        /// </summary>
        public virtual void OnBackToScene()
        {
            // 子供が消えて自分が最前面になった時に呼ばれる
        }

        private void Update()
        {
            if (_handleBackKey && IsTopScene && Input.GetKeyDown(KeyCode.Escape))
                OnBackKeyPressed();

            var task = OnUpdateAsync();
            task.ContinueWith(
                t => Debug.LogException(t.Exception?.InnerException ?? t.Exception, this),
                System.Threading.CancellationToken.None,
                System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted,
                System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
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

        /// <summary>
        /// 毎フレーム呼び出される更新処理。Update()の代わりにここに更新処理を書いてください。
        /// 内部で発生した例外は自動的にデバッグログに出力されます。
        /// </summary>
        protected virtual Task OnUpdateAsync() => Task.CompletedTask;
    }

    /// <summary>
    /// ユーザーの選択結果や処理データなど、呼び出し元に戻り値（結果）を返すシーンのベースクラス。
    /// ポップアップダイアログや、確認画面などで使用します。
    /// </summary>
    public abstract class SceneBase<TParam, TResult> : SceneBase<TParam>
        where TParam : ISceneParameter
    {
        private TaskCompletionSource<TResult> _tcs;

        public override async Task OpenAsync(TParam parameter)
        {
            _tcs = new TaskCompletionSource<TResult>();
            await base.OpenAsync(parameter);
        }

        public override async Task ReplaceAsync(TParam parameter)
        {
            _tcs = new TaskCompletionSource<TResult>();
            await base.ReplaceAsync(parameter);
        }

        /// <summary>
        /// 呼び出し元へ戻り値をセットし、自分自身を閉じて一つ前のシーンに戻ります。
        /// （シーン自身が「自身を閉じる」アクションとして呼び出します）
        /// </summary>
        public async Task CloseAsync(TResult result)
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
            _ = CloseAsync(default);
        }
    }
}