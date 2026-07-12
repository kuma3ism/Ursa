namespace Ursa
{
    /// <summary>
    /// シーン遷移時に渡すパラメーターのベースインターフェース
    /// </summary>
    public interface ISceneParameter
    {
        /// <summary>
        /// このシーンを履歴（スタック）に積むかどうか。
        /// デフォルトは true。false にすると、シーンは表示されるが履歴には残りません。
        /// </summary>
        bool IsHistory => true;

        /// <summary>
        /// このシーンを前面に出した時、背面のシーンを隠すかどうか。
        /// </summary>
        UrsaScenePresentation Presentation => UrsaScenePresentation.Fullscreen;
    }
}
