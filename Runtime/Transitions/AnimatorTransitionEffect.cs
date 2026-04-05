using System.Threading.Tasks;
using UnityEngine;

namespace Ursa.Transitions
{
    /// <summary>
    /// Animator のステートで制御するトランジション実装。
    /// Animator に「Out」「In」のステートを作成して使用します。
    /// </summary>
    public class AnimatorTransitionEffect : TransitionEffectBase
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private string _outState = "Out";
        [SerializeField] private string _inState  = "In";

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        public override async Task PlayOutAsync()
        {
            await PlayStateAsync(_outState);
        }

        public override async Task PlayInAsync()
        {
            await PlayStateAsync(_inState);
        }

        private async Task PlayStateAsync(string stateName)
        {
            if (_animator == null || _animator.runtimeAnimatorController == null) return;

            _animator.Play(stateName, 0, 0f);

            // AnimatorController が有効になりターゲットステートに遷移するまで待つ
            await Task.Yield();
            while (!_animator.GetCurrentAnimatorStateInfo(0).IsName(stateName))
                await Task.Yield();

            // ステート再生が完了するまで待つ
            while (_animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
                await Task.Yield();
        }
    }
}
