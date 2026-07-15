using UnityEngine;

namespace Ursa.UI
{
    [CreateAssetMenu(fileName = "TapEffectProfile", menuName = "Ursa/Tap Effect Profile")]
    public sealed class TapEffectProfile : ScriptableObject
    {
        [SerializeField] private TapEffectBase _prefab;
        [SerializeField] private Material _material;
        [SerializeField] private Color _color = new Color(0.35f, 0.9f, 1f, 0.9f);
        [SerializeField, Min(0.01f)] private float _duration = 0.45f;
        [SerializeField, Min(1f)] private float _startDiameter = 18f;
        [SerializeField, Min(1f)] private float _endDiameter = 132f;
        [SerializeField, Range(0.01f, 0.25f)] private float _ringThickness = 0.065f;
        [SerializeField, Range(0f, 0.05f)] private float _distortionStrength;
        [SerializeField, Range(1, 32)] private int _maxConcurrentEffects = 8;
        [SerializeField] private AnimationCurve _sizeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve _alphaCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        public TapEffectBase Prefab => _prefab;
        public Material Material => _material;
        public Color Color => _color;
        public float Duration => Mathf.Max(0.01f, _duration);
        public float StartDiameter => Mathf.Max(1f, _startDiameter);
        public float EndDiameter => Mathf.Max(StartDiameter, _endDiameter);
        public float RingThickness => Mathf.Clamp(_ringThickness, 0.01f, 0.25f);
        public float DistortionStrength => Mathf.Clamp(_distortionStrength, 0f, 0.05f);
        public int MaxConcurrentEffects => Mathf.Clamp(_maxConcurrentEffects, 1, 32);

        internal float EvaluateSize(float progress)
        {
            return _sizeCurve == null ? progress : _sizeCurve.Evaluate(progress);
        }

        internal float EvaluateAlpha(float progress)
        {
            return _alphaCurve == null ? 1f - progress : _alphaCurve.Evaluate(progress);
        }

        internal static TapEffectProfile CreateBuiltInDefault()
        {
            var profile = CreateInstance<TapEffectProfile>();
            profile.name = "[Ursa] Built-in Tap Effect Profile";
            profile.hideFlags = HideFlags.HideAndDontSave;
            return profile;
        }
    }
}
