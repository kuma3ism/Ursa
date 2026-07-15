using System;
using UnityEngine;

namespace Ursa.UI
{
    public static class UrsaTapEffect
    {
        public static bool Enabled
        {
            get => UrsaTapEffectRuntime.Enabled;
            set => UrsaTapEffectRuntime.Enabled = value;
        }

        public static void SetProfile(TapEffectProfile profile)
        {
            UrsaTapEffectRuntime.SetProfile(profile ?? throw new ArgumentNullException(nameof(profile)));
        }

        public static void ResetProfile()
        {
            UrsaTapEffectRuntime.ResetProfile();
        }

        public static void Play(Vector2 screenPosition)
        {
            UrsaTapEffectRuntime.Play(screenPosition);
        }

        public static void Play(Vector2 screenPosition, TapEffectProfile profile)
        {
            UrsaTapEffectRuntime.Play(screenPosition, profile ?? throw new ArgumentNullException(nameof(profile)));
        }
    }
}
