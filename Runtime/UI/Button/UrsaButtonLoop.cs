using UnityEngine;

namespace Ursa.UI
{
    /// <summary>
    /// UrsaButton のグローバルブロック状態を管理する内部ループコンポーネント。
    /// シーンロード時に自動生成され、ヒエラルキーには表示されません。
    /// ハンドラー実行中のみ GameObject がアクティブになり、Update() が動作します。
    /// </summary>
    internal sealed class UrsaButtonLoop : MonoBehaviour
    {
        /// <summary>グローバルブロックの解除時刻（unscaledTime）。</summary>
        internal float GlobalBlockUntil { get; set; } = float.MinValue;

        private void Awake()
        {
            // 初期状態は非アクティブ（Update を止めておく）
            gameObject.SetActive(false);
        }

        private void Update()
        {
            // ハンドラー実行中のみここに来る。ブロック時間を延長し続ける
            GlobalBlockUntil = Time.unscaledTime + UrsaButton.GlobalBlockBuffer;
        }
    }
}
