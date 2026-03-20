using System.Threading.Tasks;
using UnityEngine;

namespace Ursa.Scenes
{
    public abstract class SceneBase<T> : MonoBehaviour, ISceneReceiver<T> where T : ISceneParameter
    {
        protected T Parameter { get; private set; }

        protected bool IsActive => UrsaCore.Scene.IsActive(this.gameObject.scene);

        public virtual async Task OnEnterScene(T parameter)
        {
            this.Parameter = parameter;
            await Task.CompletedTask;
        }

        // 子供が消えて自分が最前面になった時に呼ばれる
        public virtual void OnBackToScene()
        {
            // 必要に応じて override して使う
        }
    }
}