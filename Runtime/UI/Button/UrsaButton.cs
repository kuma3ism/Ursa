using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Ursa.UI
{
    /// <summary>
    /// Unity の Button に async ハンドラーを紐付けるコンポーネント。
    /// ExecutionLock と Blocker を使って二重実行とブロック中の操作を防ぎます。
    ///
    /// 【使い方】
    /// 1. UrsaCore.Initialize(new UrsaUIManager()) を起動時に呼ぶ（または VContainer 等で IUIManager を inject）
    /// 2. Button に AddComponent&lt;UrsaButton&gt;() して SetOnClickAsync() でハンドラーを渡す
    ///
    /// 【DI サポート】
    /// VContainer 等から Construct(IUIManager) で inject することで UrsaCore への依存を排除できます。
    /// inject されていない場合は UrsaCore.UI にフォールバックします。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class UrsaButton : MonoBehaviour
    {
        [SerializeField] private Button _button;

        private IUIManager _uiManager;
        private Func<CancellationToken, Task> _handler = _ => Task.CompletedTask;
        private CancellationTokenSource _cts;

        /// <summary>
        /// VContainer 等の DI コンテナから IUIManager を inject します。
        /// 呼ばれた場合、UrsaCore.UI へのフォールバックは行いません。
        /// </summary>
        public void Construct(IUIManager uiManager)
        {
            _uiManager = uiManager;
        }

        private void Awake()
        {
            _cts = new CancellationTokenSource();
            _button ??= GetComponent<Button>();
            _button.onClick.AddListener(InvokeHandler);
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            if (_button != null)
                _button.onClick.RemoveListener(InvokeHandler);
        }

        /// <summary>クリック時に呼ばれる async ハンドラーをセットします。</summary>
        public void SetOnClickAsync(Func<CancellationToken, Task> handler)
        {
            _handler = handler ?? (_ => Task.CompletedTask);
        }

        private IUIManager ResolveUI()
        {
            if (_uiManager != null) return _uiManager;
            if (UrsaCore.IsUIReady) return UrsaCore.UI;
            return null;
        }

        private void InvokeHandler() => _ = InvokeHandlerAsync();

        private async Task InvokeHandlerAsync()
        {
            var ui = ResolveUI();
            if (ui == null)
            {
                Debug.LogWarning("[Ursa] IUIManager が未解決です。UrsaCore.Initialize(IUIManager) を呼ぶか、Construct(IUIManager) で inject してください。", this);
                return;
            }

            if (ui.Blocker.IsBlocked) return;
            if (!ui.ExecutionLock.TryEnter(out var scope)) return;

            using (scope)
            {
                ui.Blocker.Enter();
                try
                {
                    await _handler(_cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // OnDestroy によるキャンセルは正常系のため無視
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, this);
                }
                finally
                {
                    ui.Blocker.Exit();
                }
            }
        }
    }
}
