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
    /// Unity の Button に非同期ハンドラーを紐付けるコンポーネント。
    ///
    /// 【基本的な使い方】
    /// Button に AddComponent&lt;UrsaButton&gt;() して SetOnClickAsync() でハンドラーを登録します。
    ///
    /// 【ボタンブロックの種類】
    /// - セルフボタンブロック: このボタン自身の連打を防ぎます（GateInterval で秒数を設定）。
    /// - グローバルボタンブロック: いずれかのボタンのハンドラー実行中は全ボタンの入力を遮断します。
    ///   IgnoreGlobalBlock を有効にすると、このボタンはグローバルブロック中でも押せるようになります。
    ///
    /// 【長押し】
    /// SetOnHoldAsync() で長押しハンドラーを登録できます。
    /// 長押しが完了した場合、クリックハンドラーは発火しません。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class UrsaButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Button _button;

        [SerializeField, Min(0f), Tooltip("このボタン自身の連打防止インターバル（秒）です（セルフボタンブロック）。\n0 なら同ボタンの連打は許可します。\nなおどのボタンのハンドラーが実行中は interval に関わらず全ボタンがブロックされます（グローバルボタンブロック）。")]
        private float _gateInterval = 0.5f;

        [SerializeField, Tooltip("グローバルボタンブロックを無視するかどうか。\ntrue にすると他のボタンのハンドラー実行中でもこのボタンは押せるようになります。\nキャンセルボタンや緊急停止ボタンなどに使用してください。")]
        private bool _ignoreGlobalBlock = false;

        // ---- クリック ----

        private Func<CancellationToken, Task> _handler = _ => Task.CompletedTask;

        private CancellationTokenSource _destroyCts;
        private CancellationTokenSource _handlerCts;
        private float _selfBlockUntil = float.MinValue;

        // ---- 長押し ----

        private float _holdDuration;
        private Action<float> _onHolding;
        private Func<CancellationToken, Task> _onHoldComplete;
        private Coroutine _holdCoroutine;
        private bool _holdCompleted;

        // ---- 初期化 / 破棄 ----

        private void Awake()
        {
            _destroyCts = new CancellationTokenSource();
            _handlerCts = new CancellationTokenSource();
            _button ??= GetComponent<Button>();
            _button.onClick.AddListener(InvokeHandler);
        }

        private void OnValidate()
        {
            _gateInterval = Mathf.Max(0f, _gateInterval);
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
        /// クリック時に実行する非同期ハンドラーを登録します。
        /// 再度呼ぶと前のハンドラーはキャンセルされます。
        /// </summary>
        public void SetOnClickAsync(Func<CancellationToken, Task> handler)
        {
            _handlerCts?.Cancel();
            _handlerCts?.Dispose();
            _handlerCts = new CancellationTokenSource();
            _handler = handler ?? (_ => Task.CompletedTask);
        }

        /// <summary>連打防止インターバル（秒）を設定します（セルフボタンブロック）。</summary>
        public void SetGateInterval(float seconds) => _gateInterval = Mathf.Max(0f, seconds);

        /// <summary>
        /// グローバルボタンブロックを無視するかどうかを設定します。
        /// true にするとほかのボタン処理中でもこのボタンを押せるようになります。
        /// </summary>
        public void SetIgnoreGlobalBlock(bool ignore) => _ignoreGlobalBlock = ignore;

        // ---- 長押し API ----

        /// <summary>
        /// 長押しハンドラーを登録します。
        /// </summary>
        /// <param name="duration">長押しと判定する秒数。</param>
        /// <param name="onHolding">押している間、毎フレーム呼ばれるコールバック。引数は進捗(0.0〜1.0)。離したときに 0 で呼ばれます。</param>
        /// <param name="onHoldComplete">長押し完了時に一度だけ呼ばれる非同期ハンドラー。</param>
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

        private void InvokeHandler() => _ = InvokeHandlerAsync();

        private async Task InvokeHandlerAsync()
        {
            var now = Time.unscaledTime;
            if (_holdCompleted) return;
            if (now < _selfBlockUntil) return;
            if (!UrsaButtonGate.TryEnter(_ignoreGlobalBlock)) return;

            _selfBlockUntil = now + Mathf.Max(0f, _gateInterval);

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                _destroyCts.Token,
                _handlerCts.Token
            );
            var token = linkedCts.Token;
            try
            {
                var handlerTask = _handler(token);
                while (!handlerTask.IsCompleted)
                {
                    UrsaButtonGate.TouchRunningBlock();
                    await Task.WhenAny(handlerTask, Task.Delay(100, token));
                }
                await handlerTask;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
            }
        }
    }
}
