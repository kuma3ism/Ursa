using UnityEngine;

namespace Ursa.UI
{
    internal sealed class UrsaTapRingEffect : TapEffectBase
    {
        private TapRingGraphic _graphic;

        protected override void Awake()
        {
            base.Awake();
            _graphic = GetComponent<TapRingGraphic>();
            if (_graphic == null)
                _graphic = gameObject.AddComponent<TapRingGraphic>();
            _graphic.raycastTarget = false;
        }

        protected override void OnPlayStarted(TapEffectProfile profile)
        {
            _graphic ??= GetComponent<TapRingGraphic>();
            _graphic.Configure(profile.Material, profile.RingThickness);
        }

        protected override void OnProgress(float progress)
        {
            var profile = Profile;
            var diameter = Mathf.Lerp(
                profile.StartDiameter,
                profile.EndDiameter,
                profile.EvaluateSize(progress));
            RectTransform.sizeDelta = new Vector2(diameter, diameter);

            var color = profile.Color;
            color.a *= Mathf.Clamp01(profile.EvaluateAlpha(progress));
            _graphic.color = color;
        }
    }
}
