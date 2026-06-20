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

        // ドメインリロード無効時に静的フィールドが残るため、再生前に必ずリセットする
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            _loop = null;
        }

        /// <summary>
        /// グローバルボタンブロック管理オブジェクトを生成する（遅延生成）。
        /// シーンに紐づけて生成するため、DontDestroyOnLoad は使用しない。
        /// シーン遷移で破棄された場合は次の UrsaButton.Awake 時に再生成される。
        /// </summary>
        private static void InitializeLoop()
        {
            // 既に生成済みなら何もしない（二重生成防止）
            if (_loop != null) return;

            var go = new GameObject("[UrsaButtonLoop]");
            go.hideFlags = HideFlags.HideInHierarchy;
            // DontDestroyOnLoad は使用しない。シーン遷移で破棄される前提。
            _loop = go.AddComponent<UrsaButtonLoop>();
            // Awake で SetActive(false) されるので、ここでは何もしない
        }

        /// <summary>
        /// 必要に応じてループを遅延生成する（_loop が null なら生成）。
        /// 各 UrsaButton の Awake 時に呼ばれる。
        /// </summary>
        private static void EnsureLoop()
        {
            if (_loop == null) InitializeLoop();
        }

        // ---- 個別の状態管理 ----

        private Func<CancellationToken, Task> _handler = _ => Task.CompletedTask;
        private IUrsaButtonAction[] _buttonActions; // ★ SetOnClick と独立して管理
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
        private bool _isHoldingActive; // ★ 長押しが開始されたかどうかの判定用

        // ---- 初期化 / 破棄 ----

        private void Awake()
        {
            _destroyCts = new CancellationTokenSource();
            _handlerCts = new CancellationTokenSource();
            _button ??= GetComponent<Button>();
            _button.onClick.AddListener(InvokeHandler);

            // ★ IUrsaButtonAction をキャッシュ（SetOnClick とは独立して InvokeHandlerAsync 内で実行される）
            _buttonActions = GetComponents<IUrsaButtonAction>();

            // グローバルループを遅延生成（最初の UrsaButton.Awake で生成される）
            EnsureLoop();
        }

        private void OnDisable()
        {
            if (_isHandlerRunning)
            {
                if (_loop != null)
                {
                    _loop.gameObject.SetActive(false);
                    _loop.GlobalBlockUntil = Time.unscaledTime + GlobalBlockBuffer;
                }
            }
            _isHandlerRunning = false;

            // _handlerCts を再生成する設計:
            // 実行中のハンドラーは _destroyCts とリンクしているため、
            // コンポーネント破棄時（OnDestroy）には _destroyCts.Cancel() で確実に止まる。
            // ここでは SetOnClick 等で次回登録されるハンドラー用の新しいトークンを用意する。
            _handlerCts?.Cancel();
            _handlerCts?.Dispose();
            _handlerCts = new CancellationTokenSource();

            ResetHoldState();
        }

        private void OnDestroy()
        {
            if (_destroyCts != null)
            {
                _destroyCts.Cancel();
                _destroyCts.Dispose();
                _destroyCts = null; // 安全対策
            }

            if (_handlerCts != null)
            {
                _handlerCts.Cancel();
                _handlerCts.Dispose();
                _handlerCts = null; // 安全対策
            }

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
            _isHoldingActive = false;

            if (_onHolding == null && _onHoldComplete == null) return;
            _isHoldingActive = true; // 長押しアクションが存在する場合にフラグを立てる
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
            if (!_holdCompleted)
            {
                _onHolding?.Invoke(0f);
                _isHoldingActive = false; // キャンセルされたら長押し扱いを解除
            }
        }

        private System.Collections.IEnumerator HoldCoroutine()
        {
            // Time.unscaledTime（グローバルブロック）と評価基準を合わせるため unscaledDeltaTime を使用
            // Time.timeScale = 0（ポーズ中）でも長押し判定が固まらない
            var elapsed = 0f;
            while (elapsed < _holdDuration)
            {
                elapsed += Time.unscaledDeltaTime;
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
            // 長押し完了もボタン実行の一種とみなし、連打ブロックを適用する
            var now = Time.unscaledTime;
            if (now < _selfBlockUntil) return;
            if (!_ignoreGlobalBlock && _loop != null && (_loop.gameObject.activeSelf || now < _loop.GlobalBlockUntil)) return; // ★ _loop null チェック追加

            _selfBlockUntil = now + Mathf.Max(0f, _gateInterval);
            _isHandlerRunning = true;
            _loop.gameObject.SetActive(true);

            var token = _destroyCts?.Token ?? CancellationToken.None;
            try { await _onHoldComplete(token); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Debug.LogException(ex, this); }
            finally
            {
                _isHandlerRunning = false;
                if (_loop != null)
                {
                    _loop.gameObject.SetActive(false);
                    _loop.GlobalBlockUntil = Time.unscaledTime + GlobalBlockBuffer;
                }
            }
        }

        private void InvokeHandler()
        {
            // ★ 長押しをしようとして途中で指を離した場合、クリックは無視する
            if (_isHoldingActive)
            {
                _isHoldingActive = false;
                return;
            }

            _ = InvokeHandlerAsync();
        }

        private async Task InvokeHandlerAsync()
        {
            var now = Time.unscaledTime;

            if (_holdCompleted) return;
            if (now < _selfBlockUntil) return;
            // _loop は DontDestroyOnLoad しないため、シーン遷移直後は fake null になる可能性がある
            if (_loop == null) EnsureLoop();
            if (!_ignoreGlobalBlock && _loop != null && (_loop.gameObject.activeSelf || now < _loop.GlobalBlockUntil)) return;

            _selfBlockUntil = now + Mathf.Max(0f, _gateInterval);
            _isHandlerRunning = true; // ★ 実行中フラグのセット漏れを修正
            _loop.gameObject.SetActive(true); // Update を起動

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                _destroyCts?.Token ?? CancellationToken.None,
                _handlerCts?.Token ?? CancellationToken.None
            );

            try
            {
                // ★ IUrsaButtonAction は SetOnClick より先にクリック時即実行（両方確実に呼ばれる）
                if (_buttonActions != null)
                {
                    foreach (var action in _buttonActions)
                        action?.Execute();
                }

                await _handler(linkedCts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Debug.LogException(ex, this); }
            finally
            {
                _isHandlerRunning = false;
                if (_loop != null)
                {
                    _loop.gameObject.SetActive(false); // Update を止める
                    _loop.GlobalBlockUntil = Time.unscaledTime + GlobalBlockBuffer;
                }
            }
        }
    }
}
