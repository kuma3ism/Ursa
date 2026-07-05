using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Ursa;
using Ursa.UI;

namespace Ursa.Transitions
{
    /// <summary>
    /// シーン遷移エフェクトを描画する Canvas を管理するシングルトン MonoBehaviour。
    /// DontDestroyOnLoad で常駐します。
    /// 現在のシーンの <see cref="TransitionController"/> が持つ <see cref="TransitionEffectBase"/> に処理を委譲します。
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasGroup))]
    public class TransitionCanvas : MonoBehaviour
    {
        private static TransitionCanvas _instance;

        /// <summary>
        /// シーン上に <c>TransitionCanvas</c> がなければ自動生成して返します。
        /// </summary>
        public static TransitionCanvas EnsureInstance()
        {
            if (_instance != null) return _instance;

            var go = new GameObject("[Ursa] TransitionCanvas");
            go.AddComponent<Canvas>();
            go.AddComponent<CanvasGroup>();
            return go.AddComponent<TransitionCanvas>();
        }

        private TransitionController _controller;
        private TransitionEffectBase _directEffect;
        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            var canvas = GetComponent<Canvas>();
            UrsaUICanvasUtility.ConfigureTransitionCanvas(canvas);

            _canvasGroup = GetComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// 遷移元シーンの <see cref="TransitionController"/> を適用します。
        /// <see cref="ApplyEffect"/> で設定した直接エフェクトをクリアします。
        /// </summary>
        public void ApplyController(TransitionController controller)
        {
            _controller = controller;
            _directEffect = null;
        }

        /// <summary>
        /// ライブラリ等から生成したエフェクトそのものを直接適用します。
        /// </summary>
        public void ApplyEffect(TransitionEffectBase effect)
        {
            _directEffect = effect;
        }

        public async Task PlayOutAsync()
        {
            var effect = _directEffect != null ? _directEffect : _controller?.Effect;
            if (effect != null) await effect.PlayOutAsync();
        }

        public async Task PlayInAsync()
        {
            var effect = _directEffect != null ? _directEffect : _controller?.Effect;
            if (effect != null) await effect.PlayInAsync();

            // 遷移完了後にリセット（次の遷移で前回のエフェクトが残らないよう）
            _directEffect = null;
            _controller = null;
        }
    }
}
