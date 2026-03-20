using System.Threading.Tasks;
using UnityEngine;

namespace Ursa
{
    /// <summary>
    /// シーンのロードや履歴（スタック）管理を扱うマネージャーのインターフェース
    /// </summary>
    public interface ISceneManager
    {
        /// <summary>
        /// 全ての履歴を破棄し、指定したシーンを新しいルートとしてロードします。
        /// </summary>
        Task ResetAsync<TScene>(ISceneParameter parameter) where TScene : MonoBehaviour;

        /// <summary>
        /// 現在のシーンの上に、指定したシーンを新しく重ねて（Additive）履歴に追加します。
        /// </summary>
        Task PushAsync<TScene>(ISceneParameter parameter) where TScene : MonoBehaviour;

        /// <summary>
        /// 履歴スタックの一番上にある最前面のシーンを破棄し、一つ前のシーンに戻ります。
        /// </summary>
        Task PopAsync();
        
        /// <summary>
        /// 現在の最前面のシーンを破棄し、同じ階層に新しいシーンをロードして履歴を入れ替えます。
        /// </summary>
        Task ReplaceAsync<TScene>(ISceneParameter parameter) where TScene : MonoBehaviour;

        /// <summary>
        /// 指定したシーンの実体が、現在履歴スタックの最前面（トップ）にいるかどうかを判定します。
        /// </summary>
        bool IsTopScene(UnityEngine.SceneManagement.Scene scene);

        /// <summary>
        /// シーンをロードし、対象のコンポーネントインスタンスを生成・取得します（履歴にはまだ追加されません）。
        /// パラメーターが ISceneResourcePreloader を実装している場合は、シーンのロードと並行して事前DLが走ります。
        /// </summary>
        Task<TScene> CreateSceneAsync<TScene>(ISceneParameter parameter = null) where TScene : MonoBehaviour;

        /// <summary>
        /// 既にロード済みのシーンのインスタンスを、現在のシーンの上に重ねて履歴に追加します。
        /// </summary>
        Task PushInstanceAsync(UnityEngine.SceneManagement.Scene scene);

        /// <summary>
        /// 既にロード済みのシーンのインスタンスを、現在の最前面のシーンと入れ替えて履歴を更新します。
        /// </summary>
        Task ReplaceInstanceAsync(UnityEngine.SceneManagement.Scene scene);
        
        /// <summary>
        /// 戻り値を持つシーンをロードし、そのポップアップ等が終了して結果が返ってくるまで待機します。
        /// </summary>
        Task<TResult> OpenResultAsync<TScene, TParam, TResult>(TParam parameter)
            where TScene : Ursa.Scenes.SceneBase<TParam, TResult>
            where TParam : ISceneParameter;
    }
}
