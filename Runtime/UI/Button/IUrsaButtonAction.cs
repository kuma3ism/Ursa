namespace Ursa.UI
{
    /// <summary>
    /// UrsaButton が押されたときに実行されるアクションのインターフェース。
    /// 同じ GameObject にアタッチすることで、コードを書かずにボタンへ挙動を付与できます。
    /// </summary>
    public interface IUrsaButtonAction
    {
        /// <summary>ボタンが押されたときに呼ばれます。</summary>
        void Execute();
    }
}
