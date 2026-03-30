using System.Threading.Tasks;
using UnityEngine;

namespace Ursa.Blur
{
    /// <summary>
    /// ポップアップ背景ブラーエフェクトの基底クラス。
    /// TransitionEffectBase と同じパターンで、インスペクターから差し替え可能です。
    /// </summary>
    public abstract class BlurEffectBase : MonoBehaviour
    {
        /// <summary>
        /// ブラーを開始します。ポップアップが開く直前（OnPauseScene）に呼ばれます。
        /// </summary>
        public abstract Task PlayBlurAsync();

        /// <summary>
        /// ブラーを終了します。ポップアップが閉じた後（OnResumeScene）に呼ばれます。
        /// </summary>
        public abstract Task StopBlurAsync();
    }
}
