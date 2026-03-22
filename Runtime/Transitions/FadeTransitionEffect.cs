using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Ursa.Transitions
{
    /// <summary>
    /// 画面全体をフェードアウト→フェードインするシンプルな実装。
    /// コードのみで動作し、外部ライブラリ不要です。
    /// <c>CanvasGroup</c> をアサインして使用します。
    /// </summary>
    public class FadeTransitionEffect : TransitionEffectBase
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private TransitionSettings _settings;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        public override async Task PlayOutAsync()
        {
            if (_canvasGroup == null) return;
            float duration = _settings != null ? _settings.OutDuration : 0.3f;
            _canvasGroup.blocksRaycasts = true;
            await TweenAlphaAsync(0f, 1f, duration);
        }

        public override async Task PlayInAsync()
        {
            if (_canvasGroup == null) return;
            float duration = _settings != null ? _settings.InDuration : 0.3f;
            await TweenAlphaAsync(1f, 0f, duration);
            _canvasGroup.blocksRaycasts = false;
        }

        private async Task TweenAlphaAsync(float from, float to, float duration)
        {
            if (duration <= 0f) { _canvasGroup.alpha = to; return; }
            float elapsed = 0f;
            var curve = _settings?.Curve;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float curved = curve != null ? curve.Evaluate(t) : t;
                _canvasGroup.alpha = Mathf.Lerp(from, to, curved);
                await Task.Yield();
            }
            _canvasGroup.alpha = to;
        }
    }
}
