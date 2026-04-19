using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Ursa.UI
{
    /// <summary>
    /// Unity の Button に async ハンドラーを紐付けるコンポーネント。
    /// UrsaCore.UI の ExecutionLock と Blocker を使って二重実行とブロック中の操作を防ぎます。
    ///
    /// 【使い方】
    /// 1. UrsaCore.Initialize(new UrsaUIManager()) を起動時に呼ぶ
    /// 2. Button に AddComponent&lt;UrsaButtonBehaviour&gt;() して SetOnClickAsync() でハンドラーを渡す
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class UrsaButton : MonoBehaviour
    {
        [SerializeField] private Button _button;

        private Func<Task> _handler = () => Task.CompletedTask;

        private void Awake()
        {
            _button ??= GetComponent<Button>();
            _button.onClick.AddListener(InvokeHandler);
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(InvokeHandler);
        }

        /// <summary>クリック時に呼ばれる async ハンドラーをセットします。</summary>
        public void SetOnClickAsync(Func<Task> handler)
        {
            _handler = handler ?? (() => Task.CompletedTask);
        }

        private void InvokeHandler() => _ = InvokeHandlerAsync();

        private async Task InvokeHandlerAsync()
        {
            if (!UrsaCore.IsUIReady)
            {
                Debug.LogWarning("[Ursa] UrsaCore.UI が未初期化です。UrsaCore.Initialize(IUIManager) を呼んでください。", this);
                return;
            }

            var ui = UrsaCore.UI;

            if (ui.Blocker.IsBlocked) return;
            if (!ui.ExecutionLock.TryEnter(out var scope)) return;

            using (scope)
            {
                ui.Blocker.Enter();
                try
                {
                    await _handler();
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
