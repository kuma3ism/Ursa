using System;
using UnityEngine;

namespace Ursa.UI
{
    [RequireComponent(typeof(RectTransform))]
    public abstract class TapEffectBase : MonoBehaviour
    {
        private Action<TapEffectBase> _completed;
        private float _elapsed;
        private float _duration;

        public bool IsPlaying { get; private set; }
        internal long PlayOrder { get; private set; }
        internal int FactoryKey { get; set; }

        protected TapEffectProfile Profile { get; private set; }
        protected RectTransform RectTransform { get; private set; }

        protected virtual void Awake()
        {
            RectTransform = (RectTransform)transform;
        }

        internal void PlayInternal(
            Vector2 localPosition,
            TapEffectProfile profile,
            long playOrder,
            Action<TapEffectBase> completed)
        {
            StopInternal(false);
            Profile = profile;
            PlayOrder = playOrder;
            _duration = profile.Duration;
            _elapsed = 0f;
            _completed = completed;
            IsPlaying = true;

            RectTransform ??= (RectTransform)transform;
            RectTransform.anchoredPosition = localPosition;
            transform.SetAsLastSibling();
            gameObject.SetActive(true);

            OnPlayStarted(profile);
            OnProgress(0f);
        }

        internal void StopInternal(bool notifyCompleted)
        {
            if (!IsPlaying && !gameObject.activeSelf)
                return;

            IsPlaying = false;
            OnPlayStopped();
            gameObject.SetActive(false);

            var completed = _completed;
            _completed = null;
            if (notifyCompleted)
                completed?.Invoke(this);
        }

        private void Update()
        {
            if (!IsPlaying)
                return;

            _elapsed += Time.unscaledDeltaTime;
            var progress = Mathf.Clamp01(_elapsed / _duration);
            OnProgress(progress);
            if (progress >= 1f)
                StopInternal(true);
        }

        protected abstract void OnPlayStarted(TapEffectProfile profile);
        protected abstract void OnProgress(float progress);
        protected virtual void OnPlayStopped() { }
    }
}
