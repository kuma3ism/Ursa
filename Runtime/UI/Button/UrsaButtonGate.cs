using UnityEngine;

namespace Ursa.UI
{
    /// <summary>
    /// Ursa 管理下のボタン全体に適用するグローバルゲート。
    /// static フィールドは float のみ使用するため、ゲーム再起動時の初期化漏れも安全です。
    /// 処理中ブロックは呼び出し元（UrsaButton）のインスタンスフィールドで管理します。
    /// </summary>
    internal static class UrsaButtonGate
    {
        private static float _nextAllowedTime = float.MinValue;
        private static float _runningBlockUntil = float.MinValue;

        /// <summary>実行中ブロックを維持するための延長幅（秒）。</summary>
        private const float RunningBlockWindow = 0.2f;

        /// <summary>
        /// ハンドラー開始時に呼ぶ。
        /// 実行中ブロックを開始します。
        /// </summary>
        public static void EnterRunning()
        {
            TouchRunningBlock();
        }

        /// <summary>
        /// ハンドラー実行中に定期的に呼ぶ。
        /// 実行中ブロック期限を現在時刻基準で延長します。
        /// </summary>
        public static void TouchRunningBlock()
        {
            _runningBlockUntil = Time.unscaledTime + RunningBlockWindow;
        }

        /// <summary>
        /// ハンドラー終了時に呼ぶ。
        /// 解除後も Interval 分は連打を防ぐため、終了時刻を記録します。
        /// </summary>
        public static void ExitRunning()
        {
            _runningBlockUntil = float.MinValue;
        }

        /// <summary>
        /// ボタン押下時に呼ぶ。
        /// Interval 未満の場合は false を返します。
        /// </summary>
        public static bool TryEnter(float interval)
        {
            var now = Time.unscaledTime;
            if (now < _runningBlockUntil) return false;
            if (now < _nextAllowedTime) return false;

            _nextAllowedTime = now + Mathf.Max(0f, interval);
            return true;
        }
    }
}
