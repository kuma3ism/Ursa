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
        private static float _lastPressedTime = float.MinValue;

        /// <summary>連打防止のインターバル（秒）。</summary>
        public static float Interval = 0.5f;

        /// <summary>
        /// ハンドラー実行中に毎フレーム呼ぶ。
        /// _lastPressedTime を現在時刻で更新し続けることで他ボタンをブロックします。
        /// </summary>
        public static void KeepBlocking()
        {
            _lastPressedTime = Time.unscaledTime;
        }

        /// <summary>
        /// ボタン押下時に呼ぶ。
        /// Interval 未満の場合は false を返します。
        /// </summary>
        public static bool TryEnter()
        {
            if (Time.unscaledTime - _lastPressedTime < Interval) return false;
            _lastPressedTime = Time.unscaledTime;
            return true;
        }
    }
}
