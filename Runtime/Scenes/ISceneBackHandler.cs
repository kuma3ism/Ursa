namespace Ursa
{
    /// <summary>
    /// 前面にあった別シーンが閉じられ、このシーンが再び最前面になった際の通知を受け取るインターフェース。
    /// SendMessage を使わずにインターフェース経由で型安全に呼び出されます。
    /// </summary>
    public interface ISceneBackHandler
    {
        /// <summary>前面シーンが閉じられ、自分が再び最前面になった時に呼ばれます。</summary>
        void OnResumeScene();

        /// <summary>
        /// 自分の上に別のシーンが重なった時に呼ばれます（OnResumeScene の逆）。
        /// トランジションの有無に関わらず発火します。
        /// </summary>
        void OnPauseScene();
    }
}
