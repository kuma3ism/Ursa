using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Ursa.UI
{
    internal sealed class TapEffectPool
    {
        private const int AbsoluteMaximum = 32;

        private readonly RectTransform _parent;
        private readonly List<TapEffectBase> _effects = new List<TapEffectBase>(8);
        private long _playOrder;

        internal TapEffectPool(RectTransform parent)
        {
            _parent = parent;
        }

        internal int ActiveCount
        {
            get
            {
                var count = 0;
                foreach (var effect in _effects)
                {
                    if (effect != null && effect.IsPlaying)
                        count++;
                }
                return count;
            }
        }

        internal void Play(Vector2 localPosition, TapEffectProfile profile)
        {
            TrimActiveEffects(profile.MaxConcurrentEffects);

            var factoryKey = profile.Prefab == null ? 0 : profile.Prefab.GetInstanceID();
            var effect = ActiveCount >= profile.MaxConcurrentEffects
                ? FindOldestActive()
                : FindInactive(factoryKey);

            if (effect == null && _effects.Count >= AbsoluteMaximum)
                effect = FindOldestReusable();

            if (effect != null && effect.FactoryKey != factoryKey)
                effect = ReplaceEffect(effect, profile, factoryKey);
            else if (effect == null)
                effect = CreateEffect(profile, factoryKey);

            effect.PlayInternal(localPosition, profile, ++_playOrder, OnEffectCompleted);
            DisableRaycastTargets(effect.gameObject);
        }

        private void TrimActiveEffects(int maximum)
        {
            while (ActiveCount > maximum)
            {
                var oldest = FindOldestActive();
                if (oldest == null)
                    return;
                oldest.StopInternal(false);
            }
        }

        internal void Clear()
        {
            foreach (var effect in _effects)
            {
                if (effect == null)
                    continue;
                effect.StopInternal(false);
                Object.Destroy(effect.gameObject);
            }
            _effects.Clear();
        }

        private TapEffectBase FindInactive(int factoryKey)
        {
            foreach (var effect in _effects)
            {
                if (effect != null && !effect.IsPlaying && effect.FactoryKey == factoryKey)
                    return effect;
            }
            return null;
        }

        private TapEffectBase FindOldestActive()
        {
            TapEffectBase oldest = null;
            foreach (var effect in _effects)
            {
                if (effect == null || !effect.IsPlaying)
                    continue;
                if (oldest == null || effect.PlayOrder < oldest.PlayOrder)
                    oldest = effect;
            }
            return oldest;
        }

        private TapEffectBase FindOldestReusable()
        {
            TapEffectBase oldest = null;
            foreach (var effect in _effects)
            {
                if (effect == null)
                    continue;
                if (oldest == null || effect.PlayOrder < oldest.PlayOrder)
                    oldest = effect;
            }
            return oldest;
        }

        private TapEffectBase ReplaceEffect(TapEffectBase effect, TapEffectProfile profile, int factoryKey)
        {
            var index = _effects.IndexOf(effect);
            effect.StopInternal(false);
            Object.Destroy(effect.gameObject);
            var replacement = InstantiateEffect(profile, factoryKey);
            _effects[index] = replacement;
            return replacement;
        }

        private TapEffectBase CreateEffect(TapEffectProfile profile, int factoryKey)
        {
            var effect = InstantiateEffect(profile, factoryKey);
            _effects.Add(effect);
            return effect;
        }

        private TapEffectBase InstantiateEffect(TapEffectProfile profile, int factoryKey)
        {
            TapEffectBase effect;
            if (profile.Prefab != null)
            {
                effect = Object.Instantiate(profile.Prefab, _parent, false);
            }
            else
            {
                var go = new GameObject("[Ursa] Tap Ring", typeof(RectTransform));
                go.transform.SetParent(_parent, false);
                effect = go.AddComponent<UrsaTapRingEffect>();
            }

            effect.FactoryKey = factoryKey;
            effect.gameObject.SetActive(false);
            DisableRaycastTargets(effect.gameObject);
            return effect;
        }

        private static void DisableRaycastTargets(GameObject root)
        {
            foreach (var raycaster in root.GetComponentsInChildren<GraphicRaycaster>(true))
                raycaster.enabled = false;
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
        }

        private static void OnEffectCompleted(TapEffectBase effect)
        {
        }
    }
}
