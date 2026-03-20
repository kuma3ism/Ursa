using System.Threading.Tasks;
using UnityEngine;

namespace Ursa.Scenes
{
    // A: 戻り値を持たない通常のシーンベース
    public abstract class SceneBase<T> : MonoBehaviour, ISceneReceiver<T> where T : ISceneParameter
    {
        protected T Parameter { get; private set; }

        protected bool IsTopScene => UrsaCore.Scene.IsTopScene(this.gameObject.scene);

        // 既存との互換性のためのOnEnterScene
        public virtual async Task OnEnterScene(T parameter)
        {
            this.Parameter = parameter;
            await Task.CompletedTask;
        }

        // 新機能: Load済みのインスタンスを自ら履歴(Push)に乗せて初期化する
        public virtual async Task OpenAsync(T parameter)
        {
            await UrsaCore.Scene.PushInstanceAsync(this.gameObject.scene);
            await OnEnterScene(parameter);
        }

        // 新機能: Load済みのインスタンスを自ら現在の最前面と入れ替え(Replace)て初期化する
        public virtual async Task ReplaceAsync(T parameter)
        {
            await UrsaCore.Scene.ReplaceInstanceAsync(this.gameObject.scene);
            await OnEnterScene(parameter);
        }

        // 新機能: 自分自身を閉じる (PopAsyncのエイリアス)
        public virtual async Task CloseAsync()
        {
            await UrsaCore.Scene.PopAsync();
        }

        public virtual void OnBackToScene()
        {
            // 子供が消えて自分が最前面になった時に呼ばれる
        }
    }

    // B: 呼び出し元に結果を返すシーンベース
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

        // 閉じる際に結果をセットして返す
        public async Task<TResult> CloseResultAsync(TResult result)
        {
            await UrsaCore.Scene.PopAsync();
            _tcs?.TrySetResult(result);
            return result;
        }

        // 呼び出し元が待機するための Task<TResult> を返す
        public new Task<TResult> CloseResultAsync()
        {
            return _tcs?.Task ?? Task.FromResult(default(TResult));
        }
    }
}