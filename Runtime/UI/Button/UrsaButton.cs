using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ursa.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class UrsaButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private Button _button;

        // ---- ブロック設定 (Gate Settings) ----

        [Header("Self Block (セルフボタンブロック)")]
        [SerializeField, Min(0f), Tooltip("このボタン自身の連打防止インターバル（秒）です。\n0 なら同ボタンの連打は許可します。")]
        private float _gateInterval = 0.5f;

        [Header("Global Block (グローバルボタンブロック)")]
        [SerializeField, Tooltip("グローバルボタンブロックを無視するかどうか。\ntrue にすると他のボタンのハンドラー実行中でもこのボタンは押せるようになります。")]
        private bool _ignoreGlobalBlock = false;

        // ---- 全ボタン共通の管理（UrsaButtonLoop） ----

        private static UrsaButtonLoop _loop;

        /// <summary>
        /// 実行停止からブロック解除までの猶予時間（バッファ）。
        /// </summary>
        internal const float GlobalBlockBuffer = 0.2f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeLoop()
        {
            var go = new GameObject("[UrsaButtonLoop]");
            go.hideFlags = HideFlags.HideInHierarchy;
            _loop = go.AddComponent<UrsaButtonLoop>();
            // Awake で SetActive(false) されるので、ここでは何もしない
        }

        // ---- 個別の状態管理 ----

        private Func<CancellationToken, Task> _handler = _ => Task.CompletedTask;
        private CancellationTokenSource _handlerCts;
        private CancellationTokenSource _destroyCts;
        private float _selfBlockUntil = float.MinValue;
        private bool _isHandlerRunning;

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

        private void OnDisable()
        {
            if (_isHandlerRunning)
            {
                _loop.gameObject.SetActive(false);
                _loop.GlobalBlockUntil = Time.unscaledTime + GlobalBlockBuffer;
            }
            _isHandlerRunning = false;

            _handlerCts?.Cancel();
            _handlerCts?.Dispose();
            _handlerCts = new CancellationTokenSource();

            ResetHoldState();
        }

        private void OnDestroy()
        {
            _destroyCts?.Cancel();
            _destroyCts?.Dispose();
            _handlerCts?.Cancel();
            _handlerCts?.Dispose();

            if (_button != null)
                _button.onClick.RemoveListener(InvokeHandler);
        }

        // ---- クリック API ----

        /// <summary>非同期ハンドラーを登録します。</summary>
        public void SetOnClickAsync(Func<CancellationToken, Task> handler)
        {
            _handlerCts?.Cancel();
            _handlerCts?.Dispose();
            _handlerCts = new CancellationTokenSource();
            _handler = handler ?? (_ => Task.CompletedTask);
        }

        /// <summary>同期処理（Action）を登録します（内部で非同期として扱われます）。</summary>
        public void SetOnClick(Action handler)
        {
            SetOnClickAsync(_ =>
            {
                handler?.Invoke();
                return Task.CompletedTask;
            });
        }

        public void SetGateInterval(float seconds) => _gateInterval = Mathf.Max(0f, seconds);
        public void SetIgnoreGlobalBlock(bool ignore) => _ignoreGlobalBlock = ignore;

        // ---- 長押し API ----

        /// <summary>非同期の長押しハンドラーを登録します。</summary>
        public void SetOnHoldAsync(float duration, Action<float> onHolding = null, Func<CancellationToken, Task> onHoldComplete = null)
        {
            _holdDuration = duration;
            _onHolding = onHolding;
            _onHoldComplete = onHoldComplete;
        }

        /// <summary>同期の長押しハンドラーを登録します。</summary>
        public void SetOnHold(float duration, Action<float> onHolding = null, Action onHoldComplete = null)
        {
            SetOnHoldAsync(duration, onHolding, _ =>
            {
                onHoldComplete?.Invoke();
                return Task.CompletedTask;
            });
        }

        // ---- イベントハンドラー ----

        void IPointerDownHandler.OnPointerDown(PointerEventData eventData)
        {
            _holdCompleted = false;

            if (_onHolding == null && _onHoldComplete == null) return;
            if (_holdCoroutine != null) StopCoroutine(_holdCoroutine);
            _holdCoroutine = StartCoroutine(HoldCoroutine());
        }

        void IPointerUpHandler.OnPointerUp(PointerEventData eventData)
        {
            ResetHoldState();
        }

        void IPointerExitHandler.OnPointerExit(PointerEventData eventData)
        {
            // 指がボタン外に出たら、長押し進行をキャンセルする
            ResetHoldState();
        }

        private void ResetHoldState()
        {
            if (_holdCoroutine != null)
            {
                StopCoroutine(_holdCoroutine);
                _holdCoroutine = null;
            }
            if (!_holdCompleted) _onHolding?.Invoke(0f);
        }

        private System.Collections.IEnumerator HoldCoroutine()
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
            try { await _onHoldComplete(token); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Debug.LogException(ex, this); }
        }

        private void InvokeHandler() => _ = InvokeHandlerAsync();

        private async Task InvokeHandlerAsync()
        {
            var now = Time.unscaledTime;

            if (_holdCompleted) return;
            if (now < _selfBlockUntil) return;
            if (!_ignoreGlobalBlock && (_loop.gameObject.activeSelf || now < _loop.GlobalBlockUntil)) return;

            _selfBlockUntil = now + Mathf.Max(0f, _gateInterval);
            _loop.gameObject.SetActive(true); // Update を起動

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_destroyCts.Token, _handlerCts.Token);

            try { await _handler(linkedCts.Token); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Debug.LogException(ex, this); }
            finally
            {
                _isHandlerRunning = false;
                _loop.gameObject.SetActive(false); // Update を止める
                _loop.GlobalBlockUntil = Time.unscaledTime + GlobalBlockBuffer;
            }
        }
    }
}
