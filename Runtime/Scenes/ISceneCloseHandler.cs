using System;
using System.Threading.Tasks;

namespace Ursa
{
    /// <summary>
    /// シーンが自分自身を閉じたいときに、親（UrsaSceneManager）へ通知するインターフェース。
    /// SceneBase がこれを実装し、UrsaSceneManager がシーンロード時に購読します。
    /// これにより SceneBase は UrsaCore を直接参照せずに閉じる処理を委譲できます。
    /// </summary>
    internal interface ISceneCloseHandler
    {
        /// <summary>
        /// SceneBase.CloseAsync() が呼ばれた際に発火します。
        /// UrsaSceneManager がこのイベントを購読して PopAsync() を実行します。
        /// </summary>
        event Func<Task> CloseRequested;
    }
}
