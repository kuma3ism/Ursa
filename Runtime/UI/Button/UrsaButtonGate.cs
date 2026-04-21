using UnityEngine;

namespace Ursa.UI
{
    /// <summary>
    /// Ursa 管理下のボタン全体に適用するグローバルゲート。
    ///
    /// ブロックの種類:
    /// ・グローバルボタンブロック: いずれかのボタンのハンドラー実行中は全ボタンの入力をブロックします。
    /// ・セルフボタンブロック: 同じボタンの連打を interval 秒間ブロックします（0 なら連打許可）。
    ///
    /// static フィールドは float のみ使用するため、ゲーム再起動時の初期化漏れも安全です
    /// （時間が過ぎれば自動的に解除されるだけ）。
    /// </summary>
    internal static class UrsaButtonGate
    {
        /// <summary>グローバルボタンブロック: ハンドラー実行中に TouchRunningBlock で延長し続ける期限。</summary>
        private static float _globalBlockUntil = float.MinValue;

        /// <summary>セルフボタンブロック: 同ボタンの次回押下を許可する時刻。</summary>
        private static float _selfBlockUntil = float.MinValue;

        /// <summary>グローバルボタンブロックを維持するための延長幅（秒）。</summary>
        private const float GlobalBlockWindow = 0.2f;

        /// <summary>
        /// ハンドラー実行中に定期的に呼ぶ。
        /// グローバルボタンブロックの期限を現在時刻基準で延長します。
        /// 呼び出しが止まれば GlobalBlockWindow 秒後に自動解除されます。
        /// </summary>
        public static void TouchRunningBlock()
        {
            _globalBlockUntil = Mathf.Max(_globalBlockUntil, Time.unscaledTime + GlobalBlockWindow);
        }

        /// <summary>
        /// ボタン押下時に呼ぶ。
        /// グローバルボタンブロック中、またはセルフボタンブロック中の場合は false を返します。
        /// interval = 0 の場合はセルフボタンブロックは掛かりません。
        /// </summary>
        public static bool TryEnter(float interval)
        {
            var now = Time.unscaledTime;
            if (now < _globalBlockUntil) return false;
            if (now < _selfBlockUntil)   return false;

            _selfBlockUntil = now + Mathf.Max(0f, interval);
            return true;
        }
    }
}
