using UnityEngine;

namespace Ursa.UI
{
    /// <summary>
    /// UrsaButton のグローバルブロック状態を管理する内部ループコンポーネント。
    /// シーンロード時に自動生成され、ヒエラルキーには表示されません。
    /// </summary>
    internal sealed class UrsaButtonLoop : MonoBehaviour
    {
        /// <summary>いずれかのボタンのハンドラーが実行中かどうか。</summary>
        internal bool IsRunning { get; set; }

        /// <summary>グローバルブロックの解除時刻（unscaledTime）。</summary>
        internal float GlobalBlockUntil { get; set; } = float.MinValue;

        private void Update()
        {
            // ハンドラー実行中はブロック時間を延長し続ける
            if (IsRunning)
                GlobalBlockUntil = Time.unscaledTime + UrsaButton.GlobalBlockBuffer;
        }
    }
}
