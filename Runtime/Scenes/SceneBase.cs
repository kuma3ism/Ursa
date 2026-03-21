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
        public async Task<TResult> CloseResultAsync(TResult result)
        {
            await UrsaCore.Scene.PopAsync();
            _tcs?.TrySetResult(result);
            return result;
        }

        /// <summary>
        /// このシーンが閉じられ、結果が返ってくるまで待機します。
        /// （呼び出し元のシーンが「結果を待つ」アクションとして呼び出します）
        /// </summary>
        public Task<TResult> CloseResultAsync()
        {
            return _tcs?.Task ?? Task.FromResult(default(TResult));
        }
    }
}