using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ursa.UI
{
    /// <summary>
    /// Unity の Button に async ハンドラーを紐付けるコンポーネント。
    /// UrsaButtonGate により、全ボタン共通の連打防止・処理中ブロックを行います。
    /// IUIManager への依存はありません。
    ///
    /// 【使い方】
    /// Button に AddComponent&lt;UrsaButton&gt;() して SetOnClickAsync() でハンドラーを渡す
    ///
    /// 【ハンドラーの差し替え】
    /// SetOnClickAsync() を再度呼ぶと実行中の前のハンドラーがキャンセルされます。
    ///
    /// 【長押し】
    /// SetOnHoldAsync() で長押しハンドラーをセットします。
    /// onHolding は押している間毎フレーム progress(0.0〜1.0) を受け取ります。
    /// onHoldComplete は設定時間に達したときに一度だけ呼ばれます。
    /// 長押し完了後は onClick は発火しません。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class UrsaButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Button _button;

        // ---- クリック ----

        private Func<CancellationToken, Task> _handler = _ => Task.CompletedTask;

        /// <summary>MonoBehaviour 破棄時のキャンセル用。</summary>
        private CancellationTokenSource _destroyCts;

        /// <summary>ハンドラー差し替え時のキャンセル用。</summary>
        private CancellationTokenSource _handlerCts;

        /// <summary>
        /// ハンドラー実行中フラグ。インスタンスフィールドのため MonoBehaviour 再生成で自動リセット。
        /// </summary>
        private bool _isRunning;

        // ---- 長押し ----

        private float _holdDuration;
        private Action<float> _onHolding;
        private Func<CancellationToken, Task> _onHoldComplete;
        private Coroutine _holdCoroutine;

        /// <summary>長押し完了済みフラグ。次の PointerDown でリセット。</summary>
        private bool _holdCompleted;

        // ---- 初期化 / 破棄 ----

        private void Awake()
        {
            _destroyCts = new CancellationTokenSource();
            _handlerCts = new CancellationTokenSource();
            _button ??= GetComponent<Button>();
            _button.onClick.AddListener(InvokeHandler);
        }

        private void Update()
        {
            if (_isRunning) UrsaButtonGate.KeepBlocking();
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

        // ---- クリック API ----

        /// <summary>
        /// クリック時に呼ばれる async ハンドラーをセットします。
        /// 実行中のハンドラーがある場合はキャンセルされます。
        /// 長押し完了後は発火しません。
        /// </summary>
        public void SetOnClickAsync(Func<CancellationToken, Task> handler)
        {
            _handlerCts?.Cancel();
            _handlerCts?.Dispose();
            _handlerCts = new CancellationTokenSource();

            _handler = handler ?? (_ => Task.CompletedTask);
        }

        // ---- 長押し API ----

        /// <summary>
        /// 長押しハンドラーをセットします。
        /// </summary>
        /// <param name="duration">長押しと判定する秒数。</param>
        /// <param name="onHolding">押している間毎フレーム呼ばれるコールバック。引数は進捗(0.0〜1.0)。離したときに 0 で呼ばれます。</param>
        /// <param name="onHoldComplete">duration に達したときに一度だけ呼ばれる async ハンドラー。</param>
        public void SetOnHoldAsync(
            float duration,
            Action<float> onHolding = null,
            Func<CancellationToken, Task> onHoldComplete = null)
        {
            _holdDuration = duration;
            _onHolding = onHolding;
            _onHoldComplete = onHoldComplete;
        }

        // ---- IPointerDownHandler / IPointerUpHandler ----

        void IPointerDownHandler.OnPointerDown(PointerEventData eventData)
        {
            _holdCompleted = false;

            if (_onHolding == null && _onHoldComplete == null) return;
            if (_holdCoroutine != null) StopCoroutine(_holdCoroutine);
            _holdCoroutine = StartCoroutine(HoldCoroutine());
        }

        void IPointerUpHandler.OnPointerUp(PointerEventData eventData)
        {
            if (_holdCoroutine == null) return;
            StopCoroutine(_holdCoroutine);
            _holdCoroutine = null;
            _onHolding?.Invoke(0f);
        }

        private IEnumerator HoldCoroutine()
        {
            var elapsed = 0f;
            while (elapsed < _holdDuration)
            {
                elapsed += Time.deltaTime;
                _onHolding?.Invoke(Mathf.Clamp01(elapsed / _holdDuration));
                yield return null;
            }

            _holdCompleted = true;
            _onHolding?.Invoke(1f);
            _holdCoroutine = null;

            if (_onHoldComplete != null)
                _ = InvokeHoldCompleteAsync();
        }

        private async Task InvokeHoldCompleteAsync()
        {
            var token = _destroyCts?.Token ?? CancellationToken.None;
            try
            {
                await _onHoldComplete(token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
            }
        }

        // ---- 内部 ----

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
            if (_holdCompleted) return;
            if (!UrsaButtonGate.TryEnter()) return;

            _isRunning = true;
            var token = GetToken();
            try
            {
                await _handler(token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
            }
            finally
            {
                _isRunning = false;
            }
        }
    }
}
