using System;
using UnityEngine;
using Ursa.Transitions;
using Ursa.UI;

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
        // ---- Scene ----

        private static ISceneManager _scene;

        public static ISceneManager Scene =>
            _scene ?? throw new InvalidOperationException(
                "UrsaCore.Scene is not initialized. Call UrsaCore.Initialize(ISceneManager) first.");

        // ---- Dialog ----

        private static IDialogManager _dialog;

        public static IDialogManager Dialog =>
            _dialog ?? throw new InvalidOperationException(
                "UrsaCore.Dialog is not initialized. Call UrsaCore.Initialize(IDialogManager) first.");

        // ---- UI ----

        private static IUIManager _ui;

        public static IUIManager UI =>
            _ui ?? throw new InvalidOperationException(
                "UrsaCore.UI is not initialized. Call UrsaCore.Initialize(IUIManager) first.");

        // ---- Settings ----

        private static UrsaSettings _settings;

        public static UrsaSettings Settings
        {
            get
            {
                if (_settings == null)
                    _settings = UrsaSettings.Instance;
                return _settings;
            }
            set => _settings = value;
        }

        // ---- IsReady ----

        public static bool IsSceneReady => _scene != null;
        public static bool IsDialogReady => _dialog != null;
        public static bool IsUIReady => _ui != null;

        /// <summary>後方互換性のために残します。Scene が初期化済みかどうかを返します。</summary>
        public static bool IsReady => _scene != null;

        // ---- Initialize ----

        /// <summary>シーン管理システムを初期化します。</summary>
        public static void Initialize(ISceneManager sceneManager)
        {
            if (_scene != null) DisposeScene();
            _scene = sceneManager ?? throw new ArgumentNullException(nameof(sceneManager));
        }

        /// <summary>ダイアログ管理システムを初期化します。</summary>
        public static void Initialize(IDialogManager dialogManager)
        {
            if (_dialog != null) DisposeDialog();
            _dialog = dialogManager ?? throw new ArgumentNullException(nameof(dialogManager));
        }

        /// <summary>UI 管理システムを初期化します。</summary>
        public static void Initialize(IUIManager uiManager)
        {
            _ui = uiManager ?? throw new ArgumentNullException(nameof(uiManager));
        }

        // ---- Dispose ----

        public static void Dispose()
        {
            DisposeScene();
            DisposeDialog();
            DisposeUI();
        }

        private static void DisposeScene() => _scene = null;
        private static void DisposeDialog() => _dialog = null;
        private static void DisposeUI() => _ui = null;
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
        /// <summary>前面シーンが閉じられ、自分が再び最前面になった時に呼ばれます。</summary>
        void OnResumeScene();

        /// <summary>
        /// 自分の上に別のシーンが重なった時に呼ばれます（OnResumeScene の逆）。
        /// トランジションの有無に関わらず発火します。
        /// </summary>
        void OnPauseScene();
    }

}
