using UnityEngine;

namespace Ursa.UI
{
    /// <summary>
    /// UrsaButton のグローバルボタンブロックを担う内部クラスです。直接操作する必要はありません。
    ///
    /// 【グローバルボタンブロック】
    /// いずれかのボタンのハンドラーが実行中は、全ボタンの入力を遮断します。
    /// IgnoreGlobalBlock が有効なボタンはこの制限を受けません。
    /// </summary>
    internal static class UrsaButtonGate
    {
        private static float _globalBlockUntil = float.MinValue;

        private const float GlobalBlockWindow = 0.2f;

        public static void TouchRunningBlock()
        {
            _globalBlockUntil = Mathf.Max(_globalBlockUntil, Time.unscaledTime + GlobalBlockWindow);
        }

        /// <param name="ignoreGlobalBlock">true の場合、グローバルボタンブロックを無視します。</param>
        public static bool TryEnter(bool ignoreGlobalBlock = false)
        {
            var now = Time.unscaledTime;
            if (!ignoreGlobalBlock && now < _globalBlockUntil) return false;
            return true;
        }
    }
}
