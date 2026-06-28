using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

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
    /// 最前面ダイアログの直下に自動配置し、スタイル（Dimmed 等）を適用します。
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
        private Button     _barrierButton;

        /// <summary>Dimmed スタイル時のバリアの色。alpha で濃さを調整できます。</summary>
        public Color DimmedColor { get; set; } = new Color(0f, 0f, 0f, 0.5f);

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
                        BarrierStyle       = parameter?.BarrierStyle ?? BarrierStyle.Dimmed,
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
            }
        }

        // ---- Barrier 管理 ────────────────────────────────────────

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
                return;
            }

            var top          = _history[_history.Count - 1];
            var topTransform = top.Instance.transform;
            var topParent    = topTransform.parent;

            // 親が変わっていたら作り直す
            if (_barrierObject == null || _barrierObject.transform.parent != topParent)
            {
                if (_barrierObject != null)
                    UnityEngine.Object.Destroy(_barrierObject);
                CreateBarrier(topParent as RectTransform);
            }

            // スタイル適用
            switch (top.BarrierStyle)
            {
                case BarrierStyle.Dimmed:
                    _barrierObject.SetActive(true);
                    _barrierImage.color = DimmedColor;
                    break;

                case BarrierStyle.None:
                    _barrierObject.SetActive(false);
                    return; // 位置調整不要
            }

            // 最前面ダイアログの直下に配置:
            // Barrier を末尾 → top を末尾 とすると [..., Barrier, top] になる
            _barrierObject.transform.SetAsLastSibling();
            topTransform.SetAsLastSibling();
        }

        private void CreateBarrier(RectTransform parent)
        {
            var go = new GameObject("[UrsaBarrier]");
            go.transform.SetParent(parent, false);

            var rect      = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _barrierImage               = go.AddComponent<Image>();
            _barrierImage.color         = DimmedColor;
            _barrierImage.raycastTarget = true;

            // タップハンドラー（BarrierDismissible は tap 時に確認する）
            _barrierButton            = go.AddComponent<Button>();
            _barrierButton.transition = Selectable.Transition.None;
            _barrierButton.onClick.AddListener(OnBarrierTapped);

            _barrierObject = go;
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
            _ddolCanvas             = go.AddComponent<Canvas>();
            _ddolCanvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            _ddolCanvas.sortingOrder = 100;
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
}
