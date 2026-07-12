using System.Threading.Tasks;
using UnityEngine;
using Ursa.Transitions;

namespace Ursa
{
    /// <summary>
    /// トランジションエフェクトの MonoBehaviour 基底クラス。
    /// このクラスを継承して Inspector からアタッチすることで SceneBase に認識されます。
    /// </summary>
    public abstract class TransitionEffectBase : MonoBehaviour, ITransitionEffect
    {
        [SerializeField, Min(0f)]
        [Tooltip("PlayOut 後に画面が覆われた状態を最低限維持する秒数。シーン切り替え処理の実行時間も含みます。")]
        private float _minimumCoveredDuration = 0f;

        /// <summary>
        /// PlayOut 完了後、PlayIn 開始前までに画面が覆われた状態を最低限維持する秒数。
        /// シーンロードなどの処理時間を含み、足りない分だけ待機します。
        /// </summary>
        public float MinimumCoveredDuration => Mathf.Max(0f, _minimumCoveredDuration);

        public abstract Task PlayOutAsync();
        public abstract Task PlayInAsync();
    }
}
