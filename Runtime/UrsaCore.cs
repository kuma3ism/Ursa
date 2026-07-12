using System;
using Ursa.UI;

namespace Ursa
{
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
            UrsaEventSystem.RequestEnsureExists();
        }

        /// <summary>ダイアログ管理システムを初期化します。</summary>
        public static void Initialize(IDialogManager dialogManager)
        {
            if (_dialog != null) DisposeDialog();
            _dialog = dialogManager ?? throw new ArgumentNullException(nameof(dialogManager));
            UrsaEventSystem.RequestEnsureExists();
        }

        /// <summary>UI 管理システムを初期化します。</summary>
        public static void Initialize(IUIManager uiManager)
        {
            _ui = uiManager ?? throw new ArgumentNullException(nameof(uiManager));
            UrsaEventSystem.RequestEnsureExists();
        }

        // ---- Dispose ----

        public static void Dispose()
        {
            DisposeScene();
            DisposeDialog();
            DisposeUI();
        }

        private static void DisposeScene() => _scene = null;
        private static void DisposeDialog()
        {
            if (_dialog is IDisposable disposable)
                disposable.Dispose();
            _dialog = null;
        }
        private static void DisposeUI() => _ui = null;
    }
}
