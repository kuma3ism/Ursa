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
        /// ゲームのリスタートにも使えます（パラメーター不要な場合は <see cref="SceneManagerExtensions.RestartAsync{TBootScene}"/> も参照）。
        /// </summary>
        Task ResetAsync<TScene>(ISceneParameter parameter, string transitionName = TransitionType.Default) where TScene : MonoBehaviour;

        /// <summary>
        /// 現在のシーンの上に、指定したシーンを新しく重ねて（Additive）履歴に追加します。
        /// パラメーターの IsHistory が false の場合、シーンは表示されますが履歴には積まれません。
        /// </summary>
        Task PushAsync<TScene>(ISceneParameter parameter, string transitionName = TransitionType.Default) where TScene : MonoBehaviour;

        /// <summary>
        /// 履歴スタックの一番上にある最前面のシーンを破棄し、一つ前のシーンに戻ります。
        /// Pushされたシーンなら閉じ、Replaceされたシーンなら置き換え元を復帰します。
        /// </summary>
        Task PopAsync(string transitionName = TransitionType.Default);

        /// <summary>
        /// 現在の最前面シーンを閉じます。
        /// Replaceされたシーンなら置き換え元を復帰せず、置き換え履歴ごと破棄します。
        /// </summary>
        Task CloseAsync(string transitionName = TransitionType.Default);

        /// <summary>
        /// 指定されたシーンからの「自分を閉じる」要求を処理します。
        /// Replaceされたシーンなら置き換え元を復帰せず、置き換え履歴ごと破棄します。
        /// </summary>
        Task CloseAsync(UnityEngine.SceneManagement.Scene scene, string transitionName = TransitionType.Default);

        /// <summary>
        /// 現在の最前面シーンを置き換え元として保持し、新しいシーンを差し替え表示します。
        /// ルートシーンだけは戻り先がないため、履歴ごと置き換えます。
        /// </summary>
        Task ReplaceAsync<TScene>(ISceneParameter parameter, string transitionName = TransitionType.Default) where TScene : MonoBehaviour;

        /// <summary>
        /// 指定したシーンの実体が、現在履歴スタックの最前面（トップ）にいるかどうかを判定します。
        /// </summary>
        bool IsTopScene(UnityEngine.SceneManagement.Scene scene);

        /// <summary>シーン遷移中かどうかを返します。</summary>
        bool IsTransitioning { get; }

        /// <summary>Ursa が現在の最前面として扱っているシーンを返します。</summary>
        UnityEngine.SceneManagement.Scene CurrentScene { get; }

        /// <summary>
        /// シーンをロードし、対象のコンポーネントインスタンスを生成・取得します（履歴にはまだ追加されません）。
        /// パラメーターが ISceneResourcePreloader を実装している場合は、シーンのロードと並行して事前DLが走ります。
        /// </summary>
        Task<TScene> CreateSceneAsync<TScene>(ISceneParameter parameter = null, string transitionName = TransitionType.Default) where TScene : MonoBehaviour;

        /// <summary>
        /// 既にロード済みのシーンのインスタンスを、現在のシーンの上に重ねて履歴に追加します。
        /// </summary>
        Task PushInstanceAsync(UnityEngine.SceneManagement.Scene scene, string transitionName = TransitionType.Default, UrsaScenePresentation presentation = UrsaScenePresentation.Fullscreen, ISceneParameter parameter = null);

        /// <summary>
        /// 既にロード済みのシーンのインスタンスを、現在の最前面シーンを置き換え元として保持したまま差し替え表示します。
        /// ルートシーンだけは戻り先がないため、履歴ごと置き換えます。
        /// </summary>
        Task ReplaceInstanceAsync(UnityEngine.SceneManagement.Scene scene, string transitionName = TransitionType.Default, UrsaScenePresentation presentation = UrsaScenePresentation.Fullscreen, ISceneParameter parameter = null);

        /// <summary>
        /// 現在の履歴スタックを古い順（インデックス0が最も古い）で返します。
        /// </summary>
        IReadOnlyList<ISceneHistoryEntry> History { get; }

        /// <summary>
        /// 履歴スタック内で最も直近にある TScene 型のシーンまで一気にPopします。
        /// 自分自身（最前面の非履歴シーン）はそのまま残ります。
        /// 対象が見つからない場合は InvalidOperationException をスローします。
        /// </summary>
        Task JumpToAsync<TScene>(string transitionName = TransitionType.Default) where TScene : MonoBehaviour;

        /// <summary>
        /// 指定したインデックスのシーンまで一気にPopします（インデックス0が最も古い）。
        /// 範囲外の場合は ArgumentOutOfRangeException をスローします。
        /// </summary>
        Task JumpToIndexAsync(int index, string transitionName = TransitionType.Default);
    }

    /// <summary>
    /// ISceneManager の拡張メソッド。
    /// UrsaCore を使わない場合でも任意の ISceneManager 実装から呼び出せます。
    /// </summary>
    public static class SceneManagerExtensions
    {
        /// <summary>
        /// ゲームのリスタートに使えます。
        /// 全履歴を破棄し、指定したシーンを新しいルートとして読み込みます。
        /// パラメーターなしで <see cref="ISceneManager.ResetAsync{TScene}"/> を呼ぶショートハンドです。
        /// </summary>
        public static Task RestartAsync<TBootScene>(this ISceneManager sceneManager)
            where TBootScene : MonoBehaviour
        {
            return sceneManager.ResetAsync<TBootScene>(null);
        }
    }
}
