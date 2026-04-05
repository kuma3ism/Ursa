using System.Threading.Tasks;
using UnityEngine;

namespace Ursa.Transitions
{
    /// <summary>
    /// 画面全体をフェードアウト→フェードインするシンプルな実装。
    /// </summary>
    public class FadeTransitionEffect : TransitionEffectBase
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _outDuration = 0.3f;
        [SerializeField] private float _inDuration  = 0.3f;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        public override async Task PlayOutAsync()
        {
            if (_canvasGroup == null) return;
            _canvasGroup.blocksRaycasts = true;
            await TweenAlphaAsync(0f, 1f, _outDuration);
        }

        public override async Task PlayInAsync()
        {
            if (_canvasGroup == null) return;
            await TweenAlphaAsync(1f, 0f, _inDuration);
            _canvasGroup.blocksRaycasts = false;
        }

        private async Task TweenAlphaAsync(float from, float to, float duration)
        {
            if (duration <= 0f) { _canvasGroup.alpha = to; return; }
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                await Task.Yield();
            }
            _canvasGroup.alpha = to;
        }
    }
}
