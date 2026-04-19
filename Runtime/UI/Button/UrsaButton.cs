using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Ursa.UI
{
    /// <summary>
    /// Unity の Button に async ハンドラーを紐付けるコンポーネント。
    /// IUIManager が解決できる場合は ExecutionLock と Blocker で二重実行とブロック中の操作を防ぎます。
    /// IUIManager が未解決の場合はインスタンス単位のフラグで二重実行を防ぎ、ハンドラーのみ実行します。
    ///
    /// 【使い方】
    /// 1. UrsaCore.Initialize(new UrsaUIManager()) を起動時に呼ぶ（または VContainer 等で IUIManager を inject）
    /// 2. Button に AddComponent&lt;UrsaButton&gt;() して SetOnClickAsync() でハンドラーを渡す
    ///
    /// 【DI サポート】
    /// VContainer 等から Construct(IUIManager) で inject することで UrsaCore への依存を排除できます。
    /// inject されていない場合は UrsaCore.UI にフォールバックします。
    ///
    /// 【ハンドラーの差し替え】
    /// SetOnClickAsync() を再度呼ぶと実行中の前のハンドラーがキャンセルされます。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class UrsaButton : MonoBehaviour
    {
        [SerializeField] private Button _button;

        private IUIManager _uiManager;
        private Func<CancellationToken, Task> _handler = _ => Task.CompletedTask;

        /// <summary>MonoBehaviour 破棄時のキャンセル用。</summary>
        private CancellationTokenSource _destroyCts;

        /// <summary>ハンドラー差し替え時のキャンセル用。</summary>
        private CancellationTokenSource _handlerCts;

        private int _isRunning;

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
            _destroyCts = new CancellationTokenSource();
            _handlerCts = new CancellationTokenSource();
            _button ??= GetComponent<Button>();
            _button.onClick.AddListener(InvokeHandler);
        }

        private void OnDestroy()
        {
            _destroyCts?.Cancel();
            _destroyCts?.Dispose();
            _destroyCts = null;

            _handlerCts?.Cancel();
            _handlerCts?.Dispose();
            _handlerCts = null;

            if (_button != null)
                _button.onClick.RemoveListener(InvokeHandler);
        }

        /// <summary>
        /// クリック時に呼ばれる async ハンドラーをセットします。
        /// 実行中のハンドラーがある場合はキャンセルされます。
        /// </summary>
        public void SetOnClickAsync(Func<CancellationToken, Task> handler)
        {
            // 実行中のハンドラーをキャンセルして新しい CTS を発行
            _handlerCts?.Cancel();
            _handlerCts?.Dispose();
            _handlerCts = new CancellationTokenSource();

            _handler = handler ?? (_ => Task.CompletedTask);
        }

        private IUIManager ResolveUI()
        {
            if (_uiManager != null) return _uiManager;
            if (UrsaCore.IsUIReady) return UrsaCore.UI;
            return null;
        }

        private CancellationToken GetToken()
        {
            return CancellationTokenSource.CreateLinkedTokenSource(
                _destroyCts.Token,
                _handlerCts.Token
            ).Token;
        }

        private void InvokeHandler() => _ = InvokeHandlerAsync();

        private async Task InvokeHandlerAsync()
        {
            var ui = ResolveUI();
            var token = GetToken();

            if (ui != null)
            {
                if (ui.Blocker.IsBlocked) return;
                if (!ui.ExecutionLock.TryEnter(out var scope)) return;

                using (scope)
                {
                    ui.Blocker.Enter();
                    try
                    {
                        await _handler(token);
                    }
                    catch (OperationCanceledException)
                    {
                        // キャンセルは正常系のため無視
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
            else
            {
                // IUIManager 未解決: インスタンス単位のフラグで二重実行を防いでハンドラーのみ実行
                if (Interlocked.Exchange(ref _isRunning, 1) == 1) return;
                try
                {
                    await _handler(token);
                }
                catch (OperationCanceledException)
                {
                    // キャンセルは正常系のため無視
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, this);
                }
                finally
                {
                    Interlocked.Exchange(ref _isRunning, 0);
                }
            }
        }
    }
}
