using UnityEngine;
using UnityEngine.UI;
using Ursa.Inputs;

namespace Ursa.UI
{
    [DefaultExecutionOrder(-31000)]
    internal sealed class UrsaTapEffectRuntime : MonoBehaviour
    {
        private const string RuntimeName = "[Ursa] Tap Effect Runtime";
        private const string CanvasName = "[Ursa] TapEffectCanvas";

        private static UrsaTapEffectRuntime _instance;

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private TapEffectPool _pool;
        private TapEffectProfile _builtInProfile;
        private TapEffectProfile _profileOverride;
        private bool? _enabledOverride;
        private bool _hasWarnedRippleFeatureMissing;

        internal static bool Enabled
        {
            get => EnsureInstance().IsEnabled;
            set => EnsureInstance()._enabledOverride = value;
        }

        internal static int ActiveCount => _instance?._pool?.ActiveCount ?? 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            RequestEnsureExists();
        }

        internal static void RequestEnsureExists()
        {
            _ = EnsureInstance();
        }

        internal static void Play(Vector2 screenPosition, TapEffectProfile profile = null)
        {
            EnsureInstance().PlayInternal(screenPosition, profile);
        }

        internal static void SetProfile(TapEffectProfile profile)
        {
            EnsureInstance()._profileOverride = profile;
        }

        internal static void ResetProfile()
        {
            if (_instance != null)
                _instance._profileOverride = null;
        }

        internal static void ResetOwnedRuntimeObject()
        {
            if (_instance != null)
                _instance.PrepareForReset();
            _instance = null;
        }

        internal static void ResetStaticState()
        {
            ResetOwnedRuntimeObject();
        }

        private static UrsaTapEffectRuntime EnsureInstance()
        {
            if (_instance != null)
                return _instance;

            var existing = FindAnyObjectByType<UrsaTapEffectRuntime>();
            if (existing != null)
            {
                _instance = existing;
                return existing;
            }

            var go = new GameObject(RuntimeName);
            UrsaDontDestroyOnLoadRoot.Attach(go);
            _instance = go.AddComponent<UrsaTapEffectRuntime>();
            return _instance;
        }

        private bool IsEnabled => _enabledOverride ?? UrsaCore.Settings.TapEffectEnabled;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void OnEnable()
        {
            UrsaInputRuntime.PointerDown += OnPointerDown;
        }

        private void OnDisable()
        {
            UrsaInputRuntime.PointerDown -= OnPointerDown;
        }

        private void OnPointerDown(UrsaPointerDownEvent pointerEvent)
        {
            PlayInternal(pointerEvent.ScreenPosition, null);
        }

        private void Update()
        {
            UrsaTapRippleState.Update(Time.unscaledDeltaTime);
            if (_hasWarnedRippleFeatureMissing || !UrsaTapRippleState.HasActiveRipples)
                return;
            if (UrsaTapRippleState.WasRendererFeatureEnqueuedRecently)
                return;
            if (Time.frameCount - UrsaTapRippleState.LastPlayFrame <= 2)
                return;

            _hasWarnedRippleFeatureMissing = true;
            Debug.LogWarning(
                "[Ursa] Tap ripple distortion is enabled, but UrsaTapRippleRendererFeature is not active. " +
                "The standard ring effect will be used as a fallback.");
        }

        private void PlayInternal(Vector2 screenPosition, TapEffectProfile requestedProfile)
        {
            if (!IsEnabled)
                return;

            var profile = requestedProfile ?? _profileOverride ?? UrsaCore.Settings.DefaultTapEffectProfile ?? GetBuiltInProfile();
            EnsureCanvas();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect,
                    screenPosition,
                    null,
                    out var localPosition))
                return;

            _pool.Play(localPosition, profile);
            UrsaTapRippleState.Play(screenPosition, profile);
        }

        private TapEffectProfile GetBuiltInProfile()
        {
            if (_builtInProfile == null)
                _builtInProfile = TapEffectProfile.CreateBuiltInDefault();
            return _builtInProfile;
        }

        private void EnsureCanvas()
        {
            if (_canvas != null && _canvas.gameObject != null)
                return;
            if (TryAdoptExistingCanvas())
                return;

            var go = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            UrsaDontDestroyOnLoadRoot.Attach(go);
            _canvas = go.GetComponent<Canvas>();
            UrsaUICanvasUtility.ConfigureTapEffectCanvas(_canvas);

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            _canvasRect = (RectTransform)go.transform;
            _pool = new TapEffectPool(_canvasRect);
        }

        private bool TryAdoptExistingCanvas()
        {
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var canvas in canvases)
            {
                if (canvas == null || canvas.gameObject.name != CanvasName)
                    continue;
                if (canvas.transform.root == null || canvas.transform.root.name != "[Ursa]")
                    continue;

                _canvas = canvas;
                _canvasRect = (RectTransform)canvas.transform;
                UrsaUICanvasUtility.ConfigureTapEffectCanvas(canvas);
                _pool = new TapEffectPool(_canvasRect);
                return true;
            }
            return false;
        }

        private void PrepareForReset()
        {
            UrsaInputRuntime.PointerDown -= OnPointerDown;
            _pool?.Clear();
            _pool = null;
            _canvas = null;
            _canvasRect = null;
            _profileOverride = null;
            _enabledOverride = null;
            _hasWarnedRippleFeatureMissing = false;
            UrsaTapRippleState.Reset();

            if (_builtInProfile != null)
                Destroy(_builtInProfile);
            _builtInProfile = null;
        }

        private void OnDestroy()
        {
            UrsaInputRuntime.PointerDown -= OnPointerDown;
            _pool?.Clear();
            if (_builtInProfile != null)
                Destroy(_builtInProfile);
            if (_instance == this)
            {
                UrsaTapRippleState.Reset();
                _instance = null;
            }
        }
    }
}
