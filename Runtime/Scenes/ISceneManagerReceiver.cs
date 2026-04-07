namespace Ursa
{
    /// <summary>
    /// UrsaSceneManager がシーンロード時に自分自身を注入するためのインターフェース。
    /// SceneBase がこれを実装することで、UrsaCore を直接参照せずに
    /// シーンの閉じる・開く・IsTopScene 等の操作を ISceneManager 経由で行えます。
    /// </summary>
    internal interface ISceneManagerReceiver
    {
        /// <summary>シーンロード後に UrsaSceneManager から呼ばれます。</summary>
        void SetManager(ISceneManager sceneManager);
    }
}
