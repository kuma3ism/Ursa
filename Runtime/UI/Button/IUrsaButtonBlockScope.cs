namespace Ursa.UI
{
    /// <summary>
    /// UrsaButton の「グループブロック」状態を保持するスコープ。
    ///
    /// Dialog・Scene・UrsaButtonGroup など、寿命が明確なオブジェクト自身がこれを実装し、
    /// 自分の子にある UrsaButton 同士のブロック状態を持ちます。
    /// 中央集権的な static ストアを持たないため、スコープ（＝実装しているオブジェクト）が
    /// 破棄されればブロック状態も一緒に消えます。
    /// </summary>
    public interface IUrsaButtonBlockScope
    {
        /// <summary>このスコープが現在ブロック中かどうか。</summary>
        bool IsBlocked(float now);

        /// <summary>このスコープ内のハンドラーが実行を開始したことを通知します。</summary>
        void Begin();

        /// <summary>このスコープ内のハンドラーが実行を終えたことを通知します。</summary>
        void End(float now);
    }
}
