using System;
using System.Threading.Tasks;

namespace Ursa
{
    /// <summary>
    /// シーン遷移時に渡すパラメーターに実装することで、シーンのロードと並行してアセットの事前ダウンロードを行うインターフェース。
    /// フレームワークにより、以下のタイミングで自動的に呼び出されます。
    /// <list type="bullet">
    ///   <item>UrsaCore.Scene.PushAsync() — シーンロードと並行して</item>
    ///   <item>UrsaCore.Scene.ReplaceAsync() — シーンロードと並行して</item>
    ///   <item>UrsaCore.Scene.CreateSceneAsync() — シーンロードと並行して</item>
    /// </list>
    /// </summary>
    public interface ISceneResourcePreloader
    {
        /// <param name="progress">プログレス通知。null の場合は通知なし。値は 0.0～1.0。</param>
        Task PreloadResourcesAsync(IProgress<float> progress = null);
    }
}
