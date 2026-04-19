using UnityEngine;

namespace Ursa.UI
{
    /// <summary>
    /// Ursa 管理下のボタン全体に適用するグローバルゲート。
    /// 連打防止（Interval）と処理中ブロック（IsRunning）を一元管理します。
    ///
    /// 処理中は毎フレーム _lastPressedTime を更新することで、
    /// 処理完了まで他のボタンをブロックします。
    /// static float のみ使用するため、ゲーム再起動時の初期化漏れも安全です。
    /// </summary>
    internal static class UrsaButtonGate
    {
        private static float _lastPressedTime = float.MinValue;
        private static bool _isRunning;

        /// <summary>連打防止のインターバル（秒）。</summary>
        public static float Interval = 0.5f;

        /// <summary>
        /// ハンドラー実行中に毎フレーム呼ぶ。
        /// UrsaButton.Update() から呼ばれます。
        /// </summary>
        public static void Update()
        {
            if (_isRunning)
                _lastPressedTime = Time.unscaledTime;
        }

        /// <summary>
        /// ボタン押下時に呼ぶ。
        /// ブロック中または Interval 未満の場合は false を返します。
        /// </summary>
        public static bool TryEnter()
        {
            if (Time.unscaledTime - _lastPressedTime < Interval) return false;
            _lastPressedTime = Time.unscaledTime;
            _isRunning = true;
            return true;
        }

        /// <summary>ハンドラー完了時に呼ぶ。</summary>
        public static void Exit()
        {
            _isRunning = false;
        }
    }
}
