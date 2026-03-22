using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Ursa.Transitions
{
    /// <summary>
    /// 遷移エフェクトを <see cref="TransitionEffectBase"/> の実装に委譲するコンポーネント。
    /// Prefab をアサインすると Awake 時に自動でインスタンス化します。
    /// </summary>
    [AddComponentMenu("Ursa/Transition Controller")]
    public class TransitionController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("使用するトランジションエフェクトのPrefab（TransitionEffectBase を継承したもの）")]
        private TransitionEffectBase _effectPrefab;

        public TransitionEffectBase Effect { get; private set; }

        private void Awake()
        {
            if (_effectPrefab != null)
                Effect = Instantiate(_effectPrefab);
        }

#if UNITY_EDITOR
        private void Reset()
        {
            var guids = AssetDatabase.FindAssets("FadeTransitionEffect t:Prefab");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _effectPrefab = AssetDatabase.LoadAssetAtPath<TransitionEffectBase>(path);
            }
        }
#endif
    }
}
