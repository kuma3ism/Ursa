using System.Threading.Tasks;
using UnityEngine;

namespace Ursa.Transitions
{
    /// <summary>
    /// Shader のプロパティで制御するトランジション実装。
    /// Material の float プロパティを 0→1（アウト）/ 1→0（イン）でアニメーションします。
    /// </summary>
    public class ShaderTransitionEffect : TransitionEffectBase
    {
        [SerializeField] private Material _material;
        [SerializeField] private string _property = "_Progress";
        [SerializeField] private float _outDuration = 0.3f;
        [SerializeField] private float _inDuration  = 0.3f;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        public override async Task PlayOutAsync() => await TweenAsync(0f, 1f, _outDuration);
        public override async Task PlayInAsync()  => await TweenAsync(1f, 0f, _inDuration);

        private async Task TweenAsync(float from, float to, float duration)
        {
            if (_material == null) return;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _material.SetFloat(_property, Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
                await Task.Yield();
            }
            _material.SetFloat(_property, to);
        }
    }
}
