using System.Threading.Tasks;
using UnityEngine;

namespace Ursa.Blur
{
    /// <summary>
    /// ブラーエフェクトを管理するシングルトン MonoBehaviour。
    /// DontDestroyOnLoad で常駐し、Ursaが自動で生成・管理します。
    /// 現在のシーンの <see cref="BlurController"/> が持つ <see cref="BlurEffectBase"/> に処理を委譲します。
    /// </summary>
    public class BlurCanvas : MonoBehaviour
    {
        private static BlurCanvas _instance;

        /// <summary>
        /// BlurCanvas がなければ自動生成して返します。
        /// </summary>
        public static BlurCanvas EnsureInstance()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("[Ursa] BlurCanvas");
            return go.AddComponent<BlurCanvas>();
        }

        private BlurController _controller;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// 遷移先シーンの <see cref="BlurController"/> を適用します。
        /// </summary>
        public void ApplyController(BlurController controller)
        {
            _controller = controller;
        }

        /// <summary>
        /// ブラーを開始します。Push時に一つ見た底のシーンに対して呼ばれます。
        /// </summary>
        public async Task PlayBlurAsync()
        {
            if (_controller?.Effect != null)
                await _controller.Effect.PlayBlurAsync();
        }

        /// <summary>
        /// ブラーを終了します。Pop時に呼ばれます。
        /// </summary>
        public async Task StopBlurAsync()
        {
            if (_controller?.Effect != null)
                await _controller.Effect.StopBlurAsync();
        }
    }
}
