using System;
using UnityEngine;
using Ursa.Transitions;

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
    }

    /// <summary>
    /// 履歴スタック上の1エントリを表すインターフェース
    /// </summary>
    public interface ISceneHistoryEntry
    {
        /// <summary>履歴内のインデックス（0が最も古い）</summary>
        int Index { get; }

        /// <summary>シーン名</summary>
        string SceneName { get; }

        /// <summary>シーンの型</summary>
        Type SceneType { get; }
    }

    /// <summary>
    /// Ursaのコア機能へのアクセスを提供する静的クラス
    /// </summary>
    public static class UrsaCore
    {
        private static ISceneManager _scene;

        public static ISceneManager Scene =>
            _scene ?? throw new InvalidOperationException("UrsaCore is not initialized.");

        private static UrsaSettings _settings;

        public static UrsaSettings Settings
        {
            get
            {
                // 手動でセットされていなければ、裏で自動生成・保存されているマスタデータをロードする
                if (_settings == null)
                {
                    _settings = UrsaSettings.Instance;
                }
                return _settings;
            }
            set => _settings = value;
        }

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
        System.Threading.Tasks.Task PreloadResourcesAsync(System.IProgress<float> progress = null);
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
    /// シーン遷移時にパラメーターを受け取り、初期化処理を行うための基底インターフェース。
    /// リフレクション不要で GetComponentsInChildren から直接取得できます。
    /// </summary>
    public interface ISceneReceiver
    {
        System.Threading.Tasks.Task OnEnterScene(ISceneParameter parameter);
    }

    /// <summary>
    /// シーン遷移時にパラメーターを受け取り、初期化処理を行うためのインターフェース
    /// </summary>
    public interface ISceneReceiver<T> : ISceneReceiver where T : ISceneParameter
    {
        System.Threading.Tasks.Task OnEnterScene(T parameter);
    }

    /// <summary>
    /// 前面にあった別シーンが閉じられ、このシーンが再び最前面になった際の通知を受け取るインターフェース。
    /// SendMessage を使わずにインターフェース経由で型安全に呼び出されます。
    /// </summary>
    public interface ISceneBackHandler
    {
        void OnResumeScene();
    }
}
