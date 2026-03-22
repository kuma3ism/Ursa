using UnityEngine;

namespace Ursa.Transitions
{
    /// <summary>
    /// トランジションエフェクトの設定をまとめる ScriptableObject。
    /// デザイナーがインスペクターから調整できます。
    /// </summary>
    [CreateAssetMenu(fileName = "TransitionSettings", menuName = "Ursa/Transition Settings")]
    public class TransitionSettings : ScriptableObject
    {
        [Header("アウト（暗転）")]
        [Range(0.05f, 2.0f)]
        [Tooltip("アウト演出の秒数")]
        public float OutDuration = 0.3f;

        [Header("イン（明転）")]
        [Range(0.05f, 2.0f)]
        [Tooltip("イン演出の秒数")]
        public float InDuration = 0.3f;

        [Header("共通")]
        [Tooltip("トランジションに使う色（デフォルト：黒）")]
        public Color Color = Color.black;

        [Tooltip("アニメーションカーブ（0→1 の速度変化）")]
        public AnimationCurve Curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }
}
