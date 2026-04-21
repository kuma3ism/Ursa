using UnityEngine;

namespace Ursa.UI
{
    /// <summary>
    /// UrsaButton のボタン入力制御を担う内部クラスです。直接操作する必要はありません。
    ///
    /// 【セルフボタンブロック】
    /// 同じボタンの連続押下を interval 秒間防ぎます。interval = 0 なら連打を許可します。
    ///
    /// 【グローバルボタンブロック】
    /// いずれかのボタンのハンドラーが実行中は、全ボタンの入力を遮断します。
    /// IgnoreGlobalBlock が有効なボタンはこの制限を受けません。
    /// </summary>
    internal static class UrsaButtonGate
    {
        private static float _globalBlockUntil = float.MinValue;
        private static float _selfBlockUntil   = float.MinValue;

        private const float GlobalBlockWindow = 0.2f;

        public static void TouchRunningBlock()
        {
            _globalBlockUntil = Mathf.Max(_globalBlockUntil, Time.unscaledTime + GlobalBlockWindow);
        }

        /// <param name="interval">セルフボタンブロックの秒数。0 なら連打を許可。</param>
        /// <param name="ignoreGlobalBlock">true の場合、グローバルボタンブロックを無視します。</param>
        public static bool TryEnter(float interval, bool ignoreGlobalBlock = false)
        {
            var now = Time.unscaledTime;
            if (!ignoreGlobalBlock && now < _globalBlockUntil) return false;
            if (now < _selfBlockUntil) return false;

            _selfBlockUntil = now + Mathf.Max(0f, interval);
            return true;
        }
    }
}
