using System;
using System.Collections.Generic;
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
        /// パラメーターの IsHistory が false の場合、シーンは表示されますが履歴には積まれません。
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

        /// <summary>シーン遷移中かどうかを返します。</summary>
        bool IsTransitioning { get; }

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
            where TScene : Ursa.Scenes.SceneBaseWithResult<TParam, TResult>
            where TParam : ISceneParameter;

        /// <summary>
        /// 現在の履歴スタックを古い順（インデックス0が最も古い）で返します。
        /// </summary>
        IReadOnlyList<ISceneHistoryEntry> History { get; }

        /// <summary>
        /// 履歴スタック内で最も直近にある TScene 型のシーンまで一気にPopします。
        /// 自分自身（最前面の非履歴シーン）はそのまま残ります。
        /// 対象が見つからない場合は InvalidOperationException をスローします。
        /// </summary>
        Task JumpToAsync<TScene>() where TScene : MonoBehaviour;

        /// <summary>
        /// 指定したインデックスのシーンまで一気にPopします（インデックス0が最も古い）。
        /// 範囲外の場合は ArgumentOutOfRangeException をスローします。
        /// </summary>
        Task JumpToIndexAsync(int index);
    }
}
