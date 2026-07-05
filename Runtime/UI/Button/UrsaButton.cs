using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Ursa.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class UrsaButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IUrsaButton
    {
        [SerializeField] private Button _button;

        // ---- ブロック設定 (Gate Settings) ----

        [Header("Self Block (セルフボタンブロック)")]
        [SerializeField, Min(0f), Tooltip("このボタン自身の連打防止インターバル（秒）です。\n0 なら同ボタンの連打は許可します。")]
        private float _gateInterval = 0.5f;

        [Header("Group Block (グループボタンブロック)")]
        [SerializeField, Tooltip("グループボタンブロックを無視するかどうか。\ntrue にすると同じグループの他ボタンのハンドラー実行中でもこのボタンは押せるようになります。")]
        [FormerlySerializedAs("_ignoreGlobalBlock")]
        private bool _ignoreGroupBlock = false;

        /// <summary>
        /// 実行停止からブロック解除までの猶予時間（バッファ）。
        /// </summary>
        internal const float GroupBlockBuffer = 0.2f;

        // ---- 個別の状態管理 ----

        private Func<CancellationToken, Task> _handler = _ => Task.CompletedTask;
        private IUrsaButtonAction[] _buttonActions; // ★ SetOnClick と独立して管理
        private CancellationTokenSource _handlerCts;
        private CancellationTokenSource _destroyCts;
        private float _selfBlockUntil = float.MinValue;
        private bool _isHandlerRunning;

        // ---- グループブロックのスコープ（Dialog / Scene / UrsaButtonGroup 等が実装するインスタンス） ----
        private IUrsaButtonBlockScope _blockScope;
        private bool _hasBlockScope;
        private IUrsaButtonBlockScope _runningBlockScope;

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

            ResolveAndCacheBlockScope();
        }

        private void OnDisable()
        {
            if (_isHandlerRunning)
                _runningBlockScope?.End(Time.unscaledTime);
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

        private void OnEnable()
        {
            if (!_hasBlockScope)
                ResolveAndCacheBlockScope();
        }

        private void OnTransformParentChanged()
        {
            // 親が変わるとスコープ（Dialog / Scene / Group）も変わりうるので再解決する
            ResolveAndCacheBlockScope();
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
        public void SetOnClick(Func<CancellationToken, Task> handler)
        {
            _handlerCts?.Cancel();
            _handlerCts?.Dispose();
            _handlerCts = new CancellationTokenSource();
            _handler = handler ?? (_ => Task.CompletedTask);
        }

        /// <summary>
        /// 実行時に外部の CancellationTokenSource を取得し、UrsaButton 内部の CancellationToken とリンクさせます。
        /// 外部 CTS が再生成される場合に利用します。
        /// </summary>
        /// <param name="externalCtsProvider">実行時に外部 CancellationTokenSource を返すファクトリ。</param>
        /// <param name="handler">リンクされた CancellationToken を受け取る非同期ハンドラー。</param>
        public void SetOnClick(Func<CancellationTokenSource> externalCtsProvider, Func<CancellationToken, Task> handler)
        {
            if (externalCtsProvider == null) throw new ArgumentNullException(nameof(externalCtsProvider));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            SetOnClick(async ursaCt =>
            {
                var externalCts = externalCtsProvider();
                if (externalCts == null) throw new InvalidOperationException("外部 CancellationTokenSource が null です。");

                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ursaCt, externalCts.Token);
                await handler(linkedCts.Token);
            });
        }

        /// <summary>同期処理（Action）を登録します（内部で非同期として扱われます）。</summary>
        public void SetOnClick(Action handler)
        {
            SetOnClick(_ =>
            {
                handler?.Invoke();
                return Task.CompletedTask;
            });
        }

        public void SetGateInterval(float seconds) => _gateInterval = Mathf.Max(0f, seconds);
        public void SetIgnoreGlobalBlock(bool ignore) => SetIgnoreGroupBlock(ignore);
        public void SetIgnoreGroupBlock(bool ignore) => _ignoreGroupBlock = ignore;

        // ---- 長押し API ----

        /// <summary>非同期の長押し（ロングクリック）ハンドラーを登録します。</summary>
        public void SetOnLongClickAsync(float duration, Action<float> onHolding = null, Func<CancellationToken, Task> onHoldComplete = null)
        {
            _holdDuration = duration;
            _onHolding = onHolding;
            _onHoldComplete = onHoldComplete;
        }

        /// <summary>同期の長押し（ロングクリック）ハンドラーを登録します。</summary>
        public void SetOnLongClick(float duration, Action<float> onHolding = null, Action onHoldComplete = null)
        {
            SetOnLongClickAsync(duration, onHolding, _ =>
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

            // 同一グループのブロック中は長押しを開始しない。
            var now = Time.unscaledTime;
            if (IsGroupBlocked(now)) return;

            _isHoldingActive = true;
            if (_holdCoroutine != null) StopCoroutine(_holdCoroutine);
            _holdCoroutine = StartCoroutine(HoldCoroutine());
        }

        void IPointerUpHandler.OnPointerUp(PointerEventData eventData)
        {
            // 長押し判定が開始されていて、まだ成立していなければ通常クリックを発火する
            if (_isHoldingActive && !_holdCompleted)
            {
                _ = InvokeHandlerAsync();
            }
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
            // Time.unscaledTime（ブロック判定）と評価基準を合わせるため unscaledDeltaTime を使用
            // Time.timeScale = 0（ポーズ中）でも長押し判定が固まらない
            var elapsed = 0f;
            while (elapsed < _holdDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / _holdDuration);
                _onHolding?.Invoke(progress);

                // 1.0 に到達したら長押し成立としてループを抜ける
                // これで onHolding に 1.0 は1回だけ通知される
                if (progress >= 1f)
                {
                    _holdCompleted = true;
                    break;
                }

                yield return null;
            }

            _holdCoroutine = null;

            if (_holdCompleted && _onHoldComplete != null)
                _ = InvokeHoldCompleteAsync();
        }

        private async Task InvokeHoldCompleteAsync()
        {
            // 長押し完了もボタン実行の一種とみなし、連打ブロックを適用する
            var now = Time.unscaledTime;
            if (now < _selfBlockUntil) return;
            if (IsGroupBlocked(now)) return;

            _selfBlockUntil = now + Mathf.Max(0f, _gateInterval);
            _isHandlerRunning = true;
            _runningBlockScope = GetBlockScope();
            _runningBlockScope?.Begin();

            var token = _destroyCts?.Token ?? CancellationToken.None;
            try { await _onHoldComplete(token); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Debug.LogException(ex, this); }
            finally
            {
                _isHandlerRunning = false;
                _runningBlockScope?.End(Time.unscaledTime);
                _runningBlockScope = null;
            }
        }

        private void InvokeHandler()
        {
            // 長押しが登録されている場合、OnPointerUp で通常クリック判定を行うため、
            // ここでは長押し判定中または長押し成立済みの場合は無視する。
            if (_isHoldingActive || _holdCompleted) return;

            _ = InvokeHandlerAsync();
        }

        private async Task InvokeHandlerAsync()
        {
            var now = Time.unscaledTime;

            if (now < _selfBlockUntil) return;
            if (IsGroupBlocked(now)) return;

            _selfBlockUntil = now + Mathf.Max(0f, _gateInterval);
            _isHandlerRunning = true;
            _runningBlockScope = GetBlockScope();
            _runningBlockScope?.Begin();

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
                _runningBlockScope?.End(Time.unscaledTime);
                _runningBlockScope = null;
            }
        }

        // ---- スコープ解決 ----
        // 中央集権的な static ストアは持たない。ブロック状態は、祖先にある
        // Dialog / Scene / UrsaButtonGroup など IUrsaButtonBlockScope を実装するオブジェクト自身が持つ。
        // どれも見つからない場合のみ、最寄りの Canvas（無ければ自分自身）に
        // フォールバック用の小さなコンポーネントを自動追加する。

        private bool IsGroupBlocked(float now)
        {
            if (_ignoreGroupBlock)
                return false;

            var scope = GetBlockScope();
            return scope != null && scope.IsBlocked(now);
        }

        private IUrsaButtonBlockScope GetBlockScope()
        {
            if (!_hasBlockScope)
                ResolveAndCacheBlockScope();
            return _blockScope;
        }

        private void ResolveAndCacheBlockScope()
        {
            _blockScope = ResolveBlockScope();
            _hasBlockScope = true;
        }

        private IUrsaButtonBlockScope ResolveBlockScope()
        {
            // 祖先（自分自身を含む）で最も近い IUrsaButtonBlockScope 実装を採用する。
            // UrsaButtonGroup / Dialog（DialogBase） / Scene（SceneBase）はいずれもこれを実装している。
            var scope = GetComponentInParent<IUrsaButtonBlockScope>(true);
            if (scope != null)
                return scope;

            // どれも見つからない場合のフォールバック：最寄りの Canvas（無ければ自分自身）に
            // 小さな保持用コンポーネントを生やす。
            var canvas = GetComponentInParent<Canvas>(true);
            var anchorTarget = canvas != null ? canvas.gameObject : gameObject;

            var anchor = anchorTarget.GetComponent<UrsaButtonBlockAnchor>();
            if (anchor == null)
                anchor = anchorTarget.AddComponent<UrsaButtonBlockAnchor>();
            return anchor;
        }
    }
}
