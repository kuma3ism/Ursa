using System.Threading.Tasks;
using UnityEngine;

namespace Ursa.Transitions
{
    /// <summary>
    /// トランジションエフェクトの MonoBehaviour 基底クラス。
    /// このクラスを継承して Inspector からアタッチすることで SceneBase に認識されます。
    /// </summary>
    public abstract class TransitionEffectBase : MonoBehaviour, ITransitionEffect
    {
        public abstract Task PlayOutAsync();
        public abstract Task PlayInAsync();
    }
}
