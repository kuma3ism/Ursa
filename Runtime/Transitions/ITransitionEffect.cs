using System.Threading.Tasks;

namespace Ursa.Transitions
{
    /// <summary>
    /// シーン遷移時のエフェクトを定義するインターフェース。
    /// PlayOutAsync でシーンが切り替わる前の演出（暗転など）、
    /// PlayInAsync で切り替わった後の演出（明転など）を実装してください。
    /// </summary>
    public interface ITransitionEffect
    {
        /// <summary>シーン切り替え前のアウト演出（例：フェードアウト）</summary>
        Task PlayOutAsync();

        /// <summary>シーン切り替え後のイン演出（例：フェードイン）</summary>
        Task PlayInAsync();
    }
}
