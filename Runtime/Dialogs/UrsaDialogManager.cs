using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Ursa.Dialogs
{
    /// <summary>
    /// 履歴エントリの内部実装
    /// </summary>
    internal class DialogHistoryEntry : IDialogHistoryEntry
    {
        public int Index { get; set; }
        public string DialogName { get; set; }
        public Type DialogType { get; set; }
        public GameObject Instance { get; set; }
        public IDialogReceiverBase ReceiverBase { get; set; }
    }

    /// <summary>
    /// Ursaフレームワークにおける、ダイアログの生成・破棄および履歴（スタック）管理を行う実体クラス。<br/>
    /// IDialogManager の実装であり、UrsaCore.Initialize(IDialogManager) で登録して使用します。<br/>
    ///<br/>
    /// Prefab は IDialogLoader（デフォルト: ResourcesDialogLoader）で解決します。<br/>
    /// コンストラクタに RectTransform defaultParent を渡すとダイアログの配置先を指定できます。
    /// 渡さない場合は DontDestroyOnLoad な Canvas が自動生成されます。
    /// </summary>
    public class UrsaDialogManager : IDialogManager
    {
        // ---- 依存 ----

        private readonly IDialogLoader _loader;
        private readonly IUrsaLogger _logger;

        // ---- 状態 ----

        private int _transitionCount;
        private readonly List<DialogHistoryEntry> _history = new List<DialogHistoryEntry>();

        // ---- Canvas 管理 ----

        /// <summary>Scene 配置用のデフォルト親。null の場合は自動生成した DDOL Canvas を使用します。</summary>
        private readonly RectTransform _defaultParent;

        private Canvas _ddolCanvas;

        // ---- ctor ----

        /// <param name="defaultParent">ダイアログを配置する RectTransform。null なら DontDestroyOnLoad Canvas を自動生成。</param>
        /// <param name="loader">Prefab ローダー。null なら ResourcesDialogLoader（Resources/Dialogs/）を使用。</param>
        /// <param name="logger">ロガー。null なら NullUrsaLogger。</param>
        public UrsaDialogManager(
            RectTransform defaultParent = null,
            IDialogLoader loader = null,
            IUrsaLogger logger = null)
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
        public bool HasAnyDialog => _history.Count > 0;
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

                // 1. Prefab ロード と リソースプリロード を並行実行
                Task<GameObject> prefabTask = _loader.LoadAsync(name);
                Task preloadTask = (parameter as IDialogResourcePreloader)
                    ?.PreloadResourcesAsync(null) ?? Task.CompletedTask;

                await Task.WhenAll(prefabTask, preloadTask);
                var prefab = prefabTask.Result;

                // 2. Instantiate
                var parent = GetParent(parameter?.Placement ?? DialogPlacement.Scene);
                var go = UnityEngine.Object.Instantiate(prefab, parent);

                var dialog = go.GetComponent<TDialog>();
                if (dialog == null)
                {
                    UnityEngine.Object.Destroy(go);
                    throw new InvalidOperationException(
                        $"[Ursa] Component {typeof(TDialog).Name} が Prefab のルートに見つかりませんでした。");
                }

                // 3. 履歴に積む（IsHistory == false なら積まない）
                bool addToHistory = parameter == null || parameter.IsHistory;
                if (addToHistory)
                {
                    _history.Add(new DialogHistoryEntry
                    {
                        Index = _history.Count,
                        DialogName = name,
                        DialogType = typeof(TDialog),
                        Instance = go,
                        ReceiverBase = dialog,
                    });
                    RebuildIndices();
                }

                // 4. OnOpenAsync を呼ぶ
                await dialog.OnOpenAsync(parameter);

                // 5. CancellationToken が渡された場合は CloseAll に繋げる
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
            }
            finally
            {
                _transitionCount--;
                RebuildIndices();
            }
        }

        // ---- 内部ユーティリティ ----

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
            _ddolCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _ddolCanvas.sortingOrder = 100;

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
