using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Ursa.UI;

namespace Ursa.Dialogs
{
    /// <summary>履歴エントリの内部実装</summary>
    internal class DialogHistoryEntry : IDialogHistoryEntry
    {
        public int    Index            { get; set; }
        public string DialogName       { get; set; }
        public Type   DialogType       { get; set; }
        public GameObject Instance     { get; set; }
        public IDialogReceiverBase ReceiverBase { get; set; }
        public bool   BarrierDismissible { get; set; }
        public BarrierStyle BarrierStyle { get; set; }
    }

    /// <summary>
    /// Ursaフレームワークにおける、ダイアログの生成・破棄および履歴（スタック）管理を行う実体クラス。
    ///
    /// バリアは各ダイアログのプレファブには含めず、マネージャーが1枚を管理します。
    /// 最前面ダイアログの直下に自動配置し、スタイル（Dimmed / RealtimeBlur / ScreenshotBlur）を適用します。
    /// </summary>
    public class UrsaDialogManager : IDialogManager
    {
        // ---- 依存 ----

        private readonly IDialogLoader _loader;
        private readonly IUrsaLogger   _logger;

        // ---- 状態 ----

        private int _transitionCount;
        private readonly List<DialogHistoryEntry> _history = new List<DialogHistoryEntry>();

        // ---- Canvas 管理 ----

        private readonly RectTransform _defaultParent;
        private Canvas _ddolCanvas;

        // ---- Barrier 管理 ----

        private GameObject _barrierObject;
        private Image      _barrierImage;
        private RawImage   _barrierRawImage;
        private Button     _barrierButton;
        private RenderTexture _screenshotBlurTexture;
        private Material   _realtimeBlurMaterial;
        private Coroutine _screenshotBlurCoroutine;

        /// <summary>
        /// 全ダイアログのデフォルトとなるバリアスタイル。
        /// IDialogParameter.BarrierStyle が Dimmed（未指定）の場合に使用されます。
        /// </summary>
        public BarrierStyle DefaultBarrierStyle { get; set; } = BarrierStyle.Dimmed;

        /// <summary>RealtimeBlur スタイル時に使用する実装方式。</summary>
        public RealtimeBlurMode RealtimeBlurMode { get; set; } = Ursa.RealtimeBlurMode.Auto;

        /// <summary>Dimmed スタイル時のバリアの色。alpha で濃さを調整できます。</summary>
        public Color DimmedColor { get; set; } = new Color(0f, 0f, 0f, 0.5f);

        /// <summary>RealtimeBlur スタイル時に使用するマテリアル。null の場合は Dimmed にフォールバックします。</summary>
        public Material BarrierMaterial { get; set; }

        /// <summary>ScreenshotBlur スタイル時に使用するマテリアル。null の場合は Dimmed にフォールバックします。</summary>
        public Material ScreenshotBlurMaterial { get; set; }

        /// <summary>RealtimeBlur / ScreenshotBlur 時の色乗算。alpha で濃さを調整できます。</summary>
        public Color BlurOverlayColor { get; set; } = new Color(0f, 0f, 0f, 0.3f);

        /// <summary>ScreenshotBlur 時のブラー強度。値が大きいほどぼかしが強くなります。</summary>
        public int ScreenshotBlurIterations { get; set; } = 2;

        // ---- ctor ----

        public UrsaDialogManager(
            RectTransform defaultParent = null,
            IDialogLoader loader        = null,
            IUrsaLogger   logger        = null)
        {
            _defaultParent = defaultParent;
            _loader = loader ?? new ResourcesDialogLoader();
#if URSA_LOG
            _logger = logger ?? new UnityDebugLogger();
#else
            _logger = logger ?? new NullUrsaLogger();
#endif
        }

        // ---- IDialogManager ----

        public bool IsTransitioning => _transitionCount > 0;
        public bool HasAnyDialog    => _history.Count > 0;
        public IReadOnlyList<IDialogHistoryEntry> History => _history;

        public bool IsTopDialog(MonoBehaviour dialog)
        {
            if (_history.Count == 0) return false;
            return _history[_history.Count - 1].Instance == dialog.gameObject;
        }

        // ---- Open ----

        public async Task<TDialog> OpenAsync<TDialog, TResult>(
            IDialogParameter parameter,
            CancellationToken ct = default)
            where TDialog : MonoBehaviour, IDialogReceiverBase, IOpenDialog<TResult>, IDialogLifecycleEvents
        {
            _transitionCount++;
            try
            {
                string name = typeof(TDialog).Name;
                _logger.Log($"<color=cyan>[Ursa]</color> Dialog Opening: {name}");

                Task<GameObject> prefabTask = _loader.LoadAsync(name);
                Task preloadTask = (parameter as IDialogResourcePreloader)
                    ?.PreloadResourcesAsync(null) ?? Task.CompletedTask;

                await Task.WhenAll(prefabTask, preloadTask);
                var prefab = prefabTask.Result;

                var parent = GetParent(parameter?.Placement ?? DialogPlacement.Scene);
                var go     = UnityEngine.Object.Instantiate(prefab, parent);

                var dialog = go.GetComponent<TDialog>();
                if (dialog == null)
                {
                    UnityEngine.Object.Destroy(go);
                    throw new InvalidOperationException(
                        $"[Ursa] Component {typeof(TDialog).Name} が Prefab のルートに見つかりませんでした。");
                }

                bool addToHistory = parameter == null || parameter.IsHistory;
                if (addToHistory)
                {
                    _history.Add(new DialogHistoryEntry
                    {
                        Index              = _history.Count,
                        DialogName         = name,
                        DialogType         = typeof(TDialog),
                        Instance           = go,
                        ReceiverBase       = dialog,
                        BarrierDismissible = parameter?.BarrierDismissible ?? false,
                        BarrierStyle       = ResolveBarrierStyle(parameter?.BarrierStyle ?? BarrierStyle.Dimmed),
                    });
                    RebuildIndices();
                    UpdateBarrier();
                }

                await dialog.OnOpenAsync(parameter);

                if (ct.CanBeCanceled)
                {
                    ct.Register(() =>
                    {
                        if (addToHistory)
                            _ = CloseAllAsync(DialogCloseReason.Programmatic);
                    });
                }

                _logger.Log($"<color=cyan>[Ursa]</color> Dialog Opened: {name}");
                return dialog;
            }
            finally
            {
                _transitionCount--;
            }
        }

        public Task<TDialog> OpenAsync<TDialog>(
            IDialogParameter parameter,
            CancellationToken ct = default)
            where TDialog : MonoBehaviour, IDialogReceiverBase, IOpenDialog<Unit>, IDialogLifecycleEvents
        {
            return OpenAsync<TDialog, Unit>(parameter, ct);
        }

        public async Task<TResult> OpenWithCloseAsync<TDialog, TResult>(
            IDialogParameter parameter,
            Func<TDialog, Task> configure = null,
            CancellationToken ct = default)
            where TDialog : MonoBehaviour, IDialogReceiverBase, IOpenDialog<TResult>, IDialogLifecycleEvents
        {
            var dialog = await OpenAsync<TDialog, TResult>(parameter, ct);
            if (configure != null)
                await configure(dialog);
            return await dialog.WaitForCloseAsync();
        }

        // ---- Close ----

        public async Task CloseTopAsync(DialogCloseReason reason = DialogCloseReason.Programmatic)
        {
            if (_history.Count == 0)
            {
                _logger.LogWarning("[Ursa] CloseTopAsync: 閉じるダイアログがありません。");
                return;
            }

            _transitionCount++;
            try
            {
                var entry = _history[_history.Count - 1];
                _history.RemoveAt(_history.Count - 1);
                RebuildIndices();

                _logger.Log($"<color=cyan>[Ursa]</color> Dialog Closing: {entry.DialogName} ({reason})");

                if (entry.ReceiverBase != null)
                    await entry.ReceiverBase.OnCloseAsync(reason);

                if (entry.Instance != null)
                    UnityEngine.Object.Destroy(entry.Instance);

                _loader.Unload(entry.DialogName);
                UpdateBarrier();
            }
            finally
            {
                _transitionCount--;
            }
        }

        public async Task CloseAllAsync(DialogCloseReason reason = DialogCloseReason.Programmatic)
        {
            _transitionCount++;
            try
            {
                while (_history.Count > 0)
                {
                    var entry = _history[_history.Count - 1];
                    _history.RemoveAt(_history.Count - 1);

                    _logger.Log($"<color=cyan>[Ursa]</color> Dialog CloseAll: {entry.DialogName} ({reason})");

                    if (entry.ReceiverBase != null)
                        await entry.ReceiverBase.OnCloseAsync(reason);

                    if (entry.Instance != null)
                        UnityEngine.Object.Destroy(entry.Instance);

                    _loader.Unload(entry.DialogName);
                }

                RebuildIndices();
                UpdateBarrier();
            }
            finally
            {
                _transitionCount--;
                if (_history.Count == 0)
                {
                    ReleaseScreenshotBlurTexture();
                    ReleaseRealtimeBlurMaterial();
                }
            }
        }

        // ---- Barrier 管理 ────────────────────────────────────────

        /// <summary>
        /// IDialogParameter からの指定がデフォルト値の場合、DefaultBarrierStyle を適用します。
        /// </summary>
        private BarrierStyle ResolveBarrierStyle(BarrierStyle parameterStyle)
        {
            // IDialogParameter で明示的に None や RealtimeBlur / ScreenshotBlur を指定していればそれを尊重
            // Dimmed は「未指定」として扱い、DefaultBarrierStyle を使用
            if (parameterStyle == BarrierStyle.Dimmed)
                return DefaultBarrierStyle;
            return parameterStyle;
        }

        /// <summary>
        /// 履歴の状態に合わせてバリアを更新する。
        /// Open / Close のたびに呼ぶ。
        /// </summary>
        private void UpdateBarrier()
        {
            // ダイアログが1件もなければ非表示
            if (_history.Count == 0)
            {
                if (_barrierObject != null)
                    _barrierObject.SetActive(false);
                ReleaseScreenshotBlurTexture();
                return;
            }

            var top          = _history[_history.Count - 1];
            var topTransform = top.Instance.transform;
            var topParent    = topTransform.parent as RectTransform;

            // ダイアログの親が null の場合は DDOL Canvas を使う
            if (topParent == null)
            {
                _logger.LogWarning("[Ursa] ダイアログの parent が null のため、DontDestroyOnLoad Canvas をバリアの親として使用します。");
                topParent = GetOrCreateDdolRoot();
            }

            // 親が変わっていたら作り直す
            if (_barrierObject == null || _barrierObject.transform.parent != topParent)
            {
                if (_barrierObject != null)
                    UnityEngine.Object.Destroy(_barrierObject);
                CreateBarrier(topParent);
            }

            // スタイル適用
            switch (top.BarrierStyle)
            {
                case BarrierStyle.Dimmed:
                    ReleaseScreenshotBlurTexture();
                    ReleaseRealtimeBlurMaterial();
                    SetBarrierImageActive(true);
                    _barrierImage.material = null;
                    _barrierImage.color = DimmedColor;
                    break;

                case BarrierStyle.RealtimeBlur:
                    ApplyRealtimeBlur();
                    break;

                case BarrierStyle.ScreenshotBlur:
                    SetBarrierImageActive(false);
                    ApplyScreenshotBlur();
                    break;

                case BarrierStyle.None:
                    ReleaseScreenshotBlurTexture();
                    ReleaseRealtimeBlurMaterial();
                    _barrierObject.SetActive(false);
                    _barrierImage.material = null;
                    return; // 位置調整不要
            }

            // 最前面ダイアログの直下に配置:
            // Barrier を末尾 → top を末尾 とすると [..., Barrier, top] になる
            _barrierObject.transform.SetAsLastSibling();
            topTransform.SetAsLastSibling();
        }

        private void SetBarrierImageActive(bool active)
        {
            _barrierObject.SetActive(true);
            _barrierImage.enabled = active;
            if (_barrierRawImage != null)
            {
                _barrierRawImage.enabled = !active;
                if (active)
                {
                    _barrierRawImage.texture = null;
                    _barrierRawImage.material = null;
                }
            }
        }

        private void CreateBarrier(RectTransform parent)
        {
            if (parent == null)
            {
                _logger.LogWarning("[Ursa] CreateBarrier の parent が null のため、DontDestroyOnLoad Canvas を使用します。");
                parent = GetOrCreateDdolRoot();
            }

            var go = new GameObject("[UrsaBarrier]");
            go.transform.SetParent(parent, false);

            var rect      = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _barrierImage               = go.AddComponent<Image>();
            _barrierImage.color         = DimmedColor;
            _barrierImage.material      = null;
            _barrierImage.raycastTarget = true;

            // Image と RawImage は同じ GameObject に共存できないため、
            // RawImage は子 GameObject として分離して Image の上に重ねる
            var rawGo = new GameObject("[UrsaBarrierRawImage]");
            rawGo.transform.SetParent(go.transform, false);

            var rawRect      = rawGo.AddComponent<RectTransform>();
            rawRect.anchorMin = Vector2.zero;
            rawRect.anchorMax = Vector2.one;
            rawRect.offsetMin = Vector2.zero;
            rawRect.offsetMax = Vector2.zero;

            _barrierRawImage = rawGo.AddComponent<RawImage>();
            _barrierRawImage.color = BlurOverlayColor;
            _barrierRawImage.raycastTarget = false;
            _barrierRawImage.enabled = false;

            rawGo.transform.SetAsLastSibling();

            // タップハンドラー（BarrierDismissible は tap 時に確認する）
            _barrierButton            = go.AddComponent<Button>();
            _barrierButton.transition = Selectable.Transition.None;
            _barrierButton.onClick.AddListener(OnBarrierTapped);

            _barrierObject = go;
        }

        // ---- RealtimeBlur 管理 ─────────────────────────────────

        private void ApplyRealtimeBlur()
        {
            var mode = ResolveRealtimeBlurMode();
            if (mode == Ursa.RealtimeBlurMode.ScreenshotFallback)
            {
                SetBarrierImageActive(false);
                ApplyScreenshotBlur();
                return;
            }

            ReleaseScreenshotBlurTexture();

            var realtimeMatSource = GetRealtimeBlurMaterial(mode);
            if (realtimeMatSource != null)
            {
                ReleaseRealtimeBlurMaterial();
                _realtimeBlurMaterial = new Material(realtimeMatSource);
                _realtimeBlurMaterial.SetColor("_OverlayColor", BlurOverlayColor);

                if ((mode == Ursa.RealtimeBlurMode.CameraOpaqueTexture || mode == Ursa.RealtimeBlurMode.RendererFeature) && _barrierRawImage != null)
                {
                    _barrierObject.SetActive(true);
                    _barrierImage.enabled = true;
                    _barrierImage.material = null;
                    _barrierImage.color = Color.clear;
                    _barrierRawImage.enabled = true;
                    _barrierRawImage.texture = Texture2D.whiteTexture;
                    _barrierRawImage.material = _realtimeBlurMaterial;
                    _barrierRawImage.color = Color.white;
                }
                else
                {
                    SetBarrierImageActive(true);
                    _barrierImage.material = _realtimeBlurMaterial;
                    _barrierImage.color = Color.white;
                }
            }
            else
            {
                ReleaseRealtimeBlurMaterial();
                _barrierImage.material = null;
                _barrierImage.color = DimmedColor;
                _logger.LogWarning("[Ursa] BarrierStyle.RealtimeBlur が指定されましたが BarrierMaterial / デフォルトマテリアルが見つからないため Dimmed でフォールバックします。");
            }
        }

        private RealtimeBlurMode ResolveRealtimeBlurMode()
        {
            if (RealtimeBlurMode != Ursa.RealtimeBlurMode.Auto)
                return RealtimeBlurMode;

            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null)
                return Ursa.RealtimeBlurMode.ScreenshotFallback;

            return Ursa.RealtimeBlurMode.LegacyGrabPass;
        }

        // ---- ScreenshotBlur 管理 ─────────────────────────────────

        /// <summary>
        /// 現在の画面キャプチャを撮影し、ぼかしたテクスチャをバリアに適用します。
        /// 処理負荷が高いため、モバイルでは注意が必要です。
        /// </summary>
        private void ApplyScreenshotBlur()
        {
            if (_barrierRawImage == null) return;

            var screenshotMatSource = GetScreenshotBlurMaterial();
            if (screenshotMatSource == null)
            {
                ApplyDimmedFallback("[Ursa] BarrierStyle.ScreenshotBlur が指定されましたが ScreenshotBlurMaterial / デフォルトマテリアルが見つからないため Dimmed でフォールバックします。");
                return;
            }

            ReleaseScreenshotBlurTexture();
            _barrierImage.enabled = true;
            _barrierImage.material = null;
            _barrierImage.color = DimmedColor;
            _barrierRawImage.enabled = false;

            StartScreenshotBlurCoroutine(screenshotMatSource);
        }

        private IEnumerator ApplyScreenshotBlurAtEndOfFrame(Material screenshotMatSource)
        {
            yield return new WaitForEndOfFrame();

            if (_barrierObject == null || !_barrierObject.activeInHierarchy || _barrierRawImage == null)
            {
                _screenshotBlurCoroutine = null;
                yield break;
            }

            int width = Mathf.Max(1, Screen.width / 2);
            int height = Mathf.Max(1, Screen.height / 2);

            var rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Bilinear;
            rt.autoGenerateMips = false;
            rt.Create();

            var capture = ScreenCapture.CaptureScreenshotAsTexture();
            if (capture == null)
            {
                rt.Release();
                UnityEngine.Object.Destroy(rt);
                _screenshotBlurCoroutine = null;
                ApplyDimmedFallback("[Ursa] CaptureScreenshotAsTexture に失敗したため Dimmed でフォールバックします。");
                yield break;
            }

            Graphics.Blit(capture, rt);
            UnityEngine.Object.Destroy(capture);

            var temp = RenderTexture.GetTemporary(rt.width, rt.height, 0, rt.format);
            var blurMaterial = new Material(screenshotMatSource);
            blurMaterial.SetFloat("_BlurSize", 2.0f);
            blurMaterial.SetColor("_OverlayColor", BlurOverlayColor);

            for (int i = 0; i < ScreenshotBlurIterations; i++)
            {
                Graphics.Blit(rt, temp, blurMaterial, 0);
                Graphics.Blit(temp, rt, blurMaterial, 1);
            }

            UnityEngine.Object.Destroy(blurMaterial);
            RenderTexture.ReleaseTemporary(temp);

            _screenshotBlurTexture = rt;
            _barrierRawImage.texture = rt;
            _barrierRawImage.color = Color.white;
            _barrierRawImage.enabled = true;
            _barrierImage.enabled = true;
            _barrierImage.material = null;
            _barrierImage.color = Color.clear;
            _screenshotBlurCoroutine = null;
        }

        private void ReleaseRealtimeBlurMaterial()
        {
            if (_realtimeBlurMaterial != null)
            {
                UnityEngine.Object.Destroy(_realtimeBlurMaterial);
                _realtimeBlurMaterial = null;
            }
        }

        /// <summary>
        /// RealtimeBlur 用マテリアルを取得します。
        /// 明示的に設定されたものがなければ mode に応じた Resources/Ursa 配下のデフォルトマテリアルを読み込みます。
        /// </summary>
        private Material GetRealtimeBlurMaterial(RealtimeBlurMode mode)
        {
            if (BarrierMaterial != null) return BarrierMaterial;
            var resourcePath = mode switch
            {
                Ursa.RealtimeBlurMode.CameraOpaqueTexture => "Ursa/UrsaCameraOpaqueTextureBlur",
                Ursa.RealtimeBlurMode.RendererFeature => "Ursa/UrsaRendererFeatureBlur",
                _ => "Ursa/UrsaRealtimeBlur"
            };
            var fallback = Resources.Load<Material>(resourcePath);
            if (fallback == null)
                _logger.LogWarning($"[Ursa] BarrierMaterial も Resources/{resourcePath} も見つかりません。");
            return fallback;
        }

        /// <summary>
        /// ScreenshotBlur 用マテリアルを取得します。
        /// 明示的に設定されたものがなければ Resources/Ursa/UrsaScreenshotBlur から読み込みます。
        /// </summary>
        private Material GetScreenshotBlurMaterial()
        {
            if (ScreenshotBlurMaterial != null) return ScreenshotBlurMaterial;
            var fallback = Resources.Load<Material>("Ursa/UrsaScreenshotBlur");
            if (fallback == null)
                _logger.LogWarning("[Ursa] ScreenshotBlurMaterial も Resources/Ursa/UrsaScreenshotBlur も見つかりません。");
            return fallback;
        }

        private void ReleaseScreenshotBlurTexture()
        {
            StopScreenshotBlurCoroutine();

            if (_screenshotBlurTexture != null)
            {
                _screenshotBlurTexture.Release();
                UnityEngine.Object.Destroy(_screenshotBlurTexture);
                _screenshotBlurTexture = null;
            }
            if (_barrierRawImage != null)
                _barrierRawImage.texture = null;
        }

        private void StartScreenshotBlurCoroutine(Material screenshotMatSource)
        {
            StopScreenshotBlurCoroutine();
            _screenshotBlurCoroutine = UrsaDialogCoroutineRunner.Instance.StartCoroutine(
                ApplyScreenshotBlurAtEndOfFrame(screenshotMatSource));
        }

        private void StopScreenshotBlurCoroutine()
        {
            if (_screenshotBlurCoroutine == null) return;
            if (UrsaDialogCoroutineRunner.Current != null)
                UrsaDialogCoroutineRunner.Current.StopCoroutine(_screenshotBlurCoroutine);
            _screenshotBlurCoroutine = null;
        }

        private void ApplyDimmedFallback(string warning)
        {
            _logger.LogWarning(warning);
            ReleaseRealtimeBlurMaterial();
            ReleaseScreenshotBlurTexture();

            if (_barrierImage != null)
            {
                _barrierImage.enabled = true;
                _barrierImage.material = null;
                _barrierImage.color = DimmedColor;
            }
            if (_barrierRawImage != null)
            {
                _barrierRawImage.enabled = false;
                _barrierRawImage.texture = null;
            }
        }

        private void OnBarrierTapped()
        {
            if (_history.Count == 0 || IsTransitioning) return;
            if (_history[_history.Count - 1].BarrierDismissible)
                _ = CloseTopAsync(DialogCloseReason.BarrierTap);
        }

        // ---- 内部ユーティリティ ────────────────────────────────

        private RectTransform GetParent(DialogPlacement placement)
        {
            if (placement == DialogPlacement.Scene && _defaultParent != null)
                return _defaultParent;
            return GetOrCreateDdolRoot();
        }

        private RectTransform GetOrCreateDdolRoot()
        {
            if (_ddolCanvas != null && _ddolCanvas.gameObject != null)
                return (RectTransform)_ddolCanvas.transform;

            var go = new GameObject("[UrsaDialogRoot]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _ddolCanvas = go.AddComponent<Canvas>();
            UrsaUICanvasUtility.ConfigureDialogCanvas(_ddolCanvas);
            go.AddComponent<GraphicRaycaster>(); // Barrier のタップ検知に必要

            _logger.Log("<color=cyan>[Ursa]</color> Dialog root canvas created (DontDestroyOnLoad).");
            return (RectTransform)go.transform;
        }

        private void RebuildIndices()
        {
            for (int i = 0; i < _history.Count; i++)
                _history[i].Index = i;
        }
    }

    internal sealed class UrsaDialogCoroutineRunner : MonoBehaviour
    {
        private static UrsaDialogCoroutineRunner _instance;

        public static UrsaDialogCoroutineRunner Current => _instance;

        public static UrsaDialogCoroutineRunner Instance
        {
            get
            {
                if (_instance != null) return _instance;

                var go = new GameObject("[UrsaDialogCoroutineRunner]");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _instance = go.AddComponent<UrsaDialogCoroutineRunner>();
                return _instance;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
