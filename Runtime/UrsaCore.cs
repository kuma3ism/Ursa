using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ursa
{
    /// <summary>
    /// シーン遷移時に渡すパラメーターのベースインターフェース
    /// </summary>
    public interface ISceneParameter
    {
        /// <summary>
        /// 新しいシーンをロードする際のモード。デフォルトは Single。
        /// </summary>
        LoadSceneMode Mode => LoadSceneMode.Single;
    }

    /// <summary>
    /// Ursaのコア機能へのアクセスを提供する静的クラス
    /// </summary>
    public static class UrsaCore
    {
        public static ISceneManager Scene { get; set; }
        public static bool IsReady => Scene != null;
        public static void Dispose() => Scene = null;
    }



    /// <summary>
    /// シーン遷移時に渡すパラメーターのうち、シーンのロードと同時並行でアセット等の事前DLを行いたい場合に追加実装するインターフェース
    /// </summary>
    public interface ISceneResourcePreloader
    {
        System.Threading.Tasks.Task PreloadResourcesAsync();
    }

    /// <summary>
    /// シーン終了時（OnDestroy）に、パラメータに紐づくアセットやリソースの解放（Release）を自動実行するためのインターフェース
    /// </summary>
    public interface ISceneResourceUnloader
    {
        void UnloadResources();
    }


    /// <summary>
    /// シーン遷移時にパラメーターを受け取り、初期化処理を行うためのインターフェース
    /// </summary>
    public interface ISceneReceiver<T> where T : ISceneParameter
    {
        System.Threading.Tasks.Task OnEnterScene(T parameter);
    }
}