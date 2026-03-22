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
            if (_animator == null) return;
            _animator.Play(_outState);
            await Task.Yield();
            while (_animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
                await Task.Yield();
        }

        public override async Task PlayInAsync()
        {
            if (_animator == null) return;
            _animator.Play(_inState);
            await Task.Yield();
            while (_animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
                await Task.Yield();
        }
    }
}
