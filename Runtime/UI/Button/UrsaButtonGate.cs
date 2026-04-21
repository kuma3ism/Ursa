using UnityEngine;

namespace Ursa.UI
{
    /// <summary>
    /// Ursa 管理下のボタン全体に適用するグローバルゲート。
    /// ・ハンドラー実行中は全ボタンの入力をブロックします。
    /// ・static フィールドは float のみ使用するため、ゲーム再起動時の初期化漏れも安全です
    ///   （時間が過ぎれば自動的に解除されるだけ）。
    /// ・TouchRunningBlock を定期的に呼ぶことで「実行中ブロック」を延長し続けます。
    ///   呼び出しが止まれば RunningBlockWindow 秒後に自動解除されるため、ExitRunning は不要です。
    /// </summary>
    internal static class UrsaButtonGate
    {
        private static float _nextAllowedTime = float.MinValue;

        /// <summary>実行中ブロックを維持するための延長幅（秒）。</summary>
        private const float RunningBlockWindow = 0.2f;

        /// <summary>
        /// ハンドラー実行中に定期的に呼ぶ。
        /// ブロック期限を現在時刻基準で延長します。
        /// 呼び出しが止まれば RunningBlockWindow 秒後に自動解除されます。
        /// </summary>
        public static void TouchRunningBlock()
        {
            _nextAllowedTime = Mathf.Max(_nextAllowedTime, Time.unscaledTime + RunningBlockWindow);
        }

        /// <summary>
        /// ボタン押下時に呼ぶ。
        /// 他のボタンが処理中、または interval 未満の場合は false を返します。
        /// </summary>
        public static bool TryEnter(float interval)
        {
            var now = Time.unscaledTime;
            if (now < _nextAllowedTime) return false;

            _nextAllowedTime = now + Mathf.Max(interval, RunningBlockWindow);
            return true;
        }
    }
}
