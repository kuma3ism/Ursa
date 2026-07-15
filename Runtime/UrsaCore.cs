using System;
using System.Threading.Tasks;
using UnityEngine;
using Ursa.Dialogs;
using Ursa.Inputs;
using Ursa.Transitions;
using Ursa.UI;

namespace Ursa
{
    /// <summary>
    /// Ursaのコア機能へのアクセスを提供する静的クラス
    /// </summary>
    public static class UrsaCore
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Dispose();
            _isResetting = false;
            _settings = null;

            UrsaSettings.ResetStaticState();
            UrsaInputRuntime.ResetStaticState();
            UrsaSceneManager.ResetStaticState();
            UrsaDialogManager.ResetStaticState();
            UrsaTapEffectRuntime.ResetStaticState();
            TransitionCanvas.ResetStaticState();
            UrsaUICamera.ResetStaticState();
            UrsaEventSystem.ResetStaticState();
            UrsaDontDestroyOnLoadRoot.ResetStaticState();
        }

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
        private static bool _isResetting;

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

        // ---- Reset ----

        /// <summary>
        /// Ursa が管理する実行中の状態を畳み、指定したシーンを新しいルートとして読み込みます。
        /// Dialog が初期化済みの場合は全て閉じてから、Scene の ResetAsync を実行します。
        /// </summary>
        public static async Task ResetAsync<TBootScene>(
            ISceneParameter parameter = null,
            string transitionName = TransitionType.Default)
            where TBootScene : MonoBehaviour
        {
            if (_isResetting)
                throw new InvalidOperationException("UrsaCore.ResetAsync is already running.");

            var scene = Scene;
            _isResetting = true;
            try
            {
                ThrowIfSceneTransitioning(scene);

                if (_dialog != null)
                    await _dialog.CloseAllAsync(DialogCloseReason.Programmatic);

                ThrowIfSceneTransitioning(scene);

                await scene.ResetAsync<TBootScene>(parameter, transitionName);
                ResetManagerRuntimeState();
                ResetOwnedRuntimeObjects();
                await UrsaEventSystem.WaitUntilOwnedEventSystemDestroyedAsync();
                await UrsaDontDestroyOnLoadRoot.WaitUntilOwnedRootDestroyedAsync();
                UrsaInputRuntime.RequestEnsureExists();
                UrsaTapEffectRuntime.RequestEnsureExists();
                UrsaEventSystem.RequestEnsureExists();
            }
            finally
            {
                _isResetting = false;
            }
        }

        private static void ThrowIfSceneTransitioning(ISceneManager sceneManager)
        {
            if (sceneManager.IsTransitioning)
                throw new InvalidOperationException("UrsaCore.ResetAsync cannot run while a scene transition is in progress.");
        }

        private static void ResetOwnedRuntimeObjects()
        {
            UrsaTapEffectRuntime.ResetOwnedRuntimeObject();
            UrsaInputRuntime.ResetOwnedRuntimeObject();
            UrsaUICamera.ResetOwnedCameras();
            UrsaEventSystem.ResetOwnedEventSystem();
            UrsaDontDestroyOnLoadRoot.DestroyOwnedRoot();
        }

        private static void ResetManagerRuntimeState()
        {
            if (_dialog is UrsaDialogManager dialogManager)
                dialogManager.ResetOwnedRuntimeState();
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
