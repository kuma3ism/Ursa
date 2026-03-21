using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Ursa
{
    /// <summary>
    /// シーン遷移時に渡すパラメーターのベースインターフェース
    /// </summary>
    public interface ISceneParameter
    {
    }

    /// <summary>
    /// Ursaのコア機能へのアクセスを提供する静的クラス
    /// </summary>
    public static class UrsaCore
    {
        private static ISceneManager _scene;

        public static ISceneManager Scene =>
            _scene ?? throw new InvalidOperationException("UrsaCore is not initialized.");

        public static bool IsReady => _scene != null;

        public static void Initialize(ISceneManager sceneManager)
        {
            if (_scene != null) Dispose(); // エディタ再Play時の再初期化を許容
            _scene = sceneManager ?? throw new ArgumentNullException(nameof(sceneManager));
        }

        public static void Dispose()
        {
            _scene = null;
        }
    }



    /// <summary>
    /// パラメーターに実装することで、シーンのロードと並行してアセットの事前ダウンロードを行うインターフェース。
    /// フレームワークにより、以下のタイミングで自動的に呼び出されます。
    /// <list type="bullet">
    ///   <item>UrsaCore.Scene.PushAsync() — シーンロードと並行して</item>
    ///   <item>UrsaCore.Scene.ReplaceAsync() — シーンロードと並行して</item>
    ///   <item>UrsaCore.Scene.CreateSceneAsync() — シーンロードと並行して</item>
    /// </list>
    /// </summary>
    public interface ISceneResourcePreloader
    {
        System.Threading.Tasks.Task PreloadResourcesAsync();
    }

    /// <summary>
    /// パラメーターに実装することで、シーン破棄時にアセットの解放を自動実行するインターフェース。
    /// SceneBaseの OnDestroy() 内で自動的に呼び出されます。
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