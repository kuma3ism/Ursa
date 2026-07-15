using System.Collections.Generic;
using UnityEngine;

namespace Ursa.UI
{
    internal static class UrsaTapRippleState
    {
        internal const int MaximumRipples = 8;

        private sealed class Ripple
        {
            internal Vector2 Center;
            internal float StartDiameter;
            internal float EndDiameter;
            internal float RingThickness;
            internal float Strength;
            internal float Duration;
            internal float Elapsed;
        }

        private static readonly List<Ripple> Ripples = new List<Ripple>(MaximumRipples);
        private static int _lastRendererFeatureFrame = -1000;

        internal static bool HasActiveRipples => Ripples.Count > 0;
        internal static int ActiveCount => Ripples.Count;
        internal static int LastPlayFrame { get; private set; } = -1000;
        internal static bool WasRendererFeatureEnqueuedRecently =>
            Time.frameCount - _lastRendererFeatureFrame <= 2;

        internal static void Play(Vector2 screenPosition, TapEffectProfile profile)
        {
            if (profile == null || profile.DistortionStrength <= 0f)
                return;

            var width = Mathf.Max(1f, Screen.width);
            var height = Mathf.Max(1f, Screen.height);
            var limit = Mathf.Min(MaximumRipples, profile.MaxConcurrentEffects);

            while (Ripples.Count >= limit)
                Ripples.RemoveAt(0);

            Ripples.Add(new Ripple
            {
                Center = new Vector2(screenPosition.x / width, screenPosition.y / height),
                StartDiameter = profile.StartDiameter,
                EndDiameter = profile.EndDiameter,
                RingThickness = profile.RingThickness,
                Strength = profile.DistortionStrength,
                Duration = profile.Duration
            });
            LastPlayFrame = Time.frameCount;
        }

        internal static void Update(float unscaledDeltaTime)
        {
            for (var index = Ripples.Count - 1; index >= 0; index--)
            {
                var ripple = Ripples[index];
                ripple.Elapsed += Mathf.Max(0f, unscaledDeltaTime);
                if (ripple.Elapsed >= ripple.Duration)
                    Ripples.RemoveAt(index);
            }
        }

        internal static int CopyShaderData(Vector4[] centers, Vector4[] parameters, float targetHeight)
        {
            var count = Mathf.Min(Ripples.Count, Mathf.Min(centers.Length, parameters.Length));
            var height = Mathf.Max(1f, targetHeight);
            for (var index = 0; index < count; index++)
            {
                var ripple = Ripples[index];
                var progress = Mathf.Clamp01(ripple.Elapsed / ripple.Duration);
                var easedProgress = Mathf.SmoothStep(0f, 1f, progress);
                var radius = Mathf.Lerp(ripple.StartDiameter, ripple.EndDiameter, easedProgress) * 0.5f / height;
                var width = Mathf.Max(2f / height, radius * ripple.RingThickness * 2f);

                centers[index] = new Vector4(ripple.Center.x, ripple.Center.y, radius, width);
                parameters[index] = new Vector4(ripple.Strength * (1f - progress), progress, 0f, 0f);
            }
            return count;
        }

        internal static void MarkRendererFeatureEnqueued()
        {
            _lastRendererFeatureFrame = Time.frameCount;
        }

        internal static void Reset()
        {
            Ripples.Clear();
            LastPlayFrame = -1000;
            _lastRendererFeatureFrame = -1000;
        }
    }
}
