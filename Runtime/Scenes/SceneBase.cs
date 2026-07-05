using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ursa
{
    /// <summary>
    /// 戻り値を持たない、標準的なシーンのベースクラス。
    /// 一方通行の画面遷移や、結果を返す必要のないベース画面等で使用します。
    /// </summary>
    public abstract class SceneBase<T> : MonoBehaviour, ISceneReceiver<T>, ISceneBackHandler, ISceneManagerReceiver where T : ISceneParameter
    {
        [SerializeField] private bool _handleBackKey = true;

        /// <summary>バックキー（Escape）による自動戻り処理を有効/無効にします。</summary>
        protected void SetBackKeyEnabled(bool enabled) => _handleBackKey = enabled;

        protected T CurrentParam { get; private set; }

        private ISceneManager _sceneManager;

        protected bool IsTopScene => _sceneManager?.IsTopScene(this.gameObject.scene) ?? false;

        // ---- ISceneManagerReceiver ----

        void ISceneManagerReceiver.SetManager(ISceneManager sceneManager)
        {
            _sceneManager = sceneManager;
        }

        // ---- ISceneReceiver ----

        async Task ISceneReceiver.OnEnterScene(ISceneParameter parameter)
        {
            CurrentParam = (T)parameter;
            await OnInitializeAsync((T)parameter);
        }

        async Task ISceneReceiver<T>.OnEnterScene(T parameter)
        {
            CurrentParam = parameter;
            await OnInitializeAsync(parameter);
        }

        /// <summary>
        /// サブクラスでシーン固有の初期化処理を記述するためのイベントメソッド。
        /// <c>OpenAsync()</c> / <c>ReplaceAsync()</c> およびシーン遷移によるパラメーター注入時に呼ばれます。
        ///
        /// 【！重要！】Unityの仕様上、<c>Awake()</c> や <c>Start()</c> はこのメソッドよりも先に呼ばれます。
        /// その時点では <c>CurrentParam</c> はまだ null のため、パラメータを使った初期化処理はここに記述してください。
        /// </summary>
        protected virtual async Task OnInitializeAsync(T parameter)
        {
            await Task.CompletedTask;
        }

        /// <summary>
        /// すでにロード済みのこのシーンインスタンスを履歴（スタック）の最前面にPushし、
        /// パラメーターを渡して初期化処理を開始します。
        /// </summary>
        internal async Task OpenAsync(T parameter)
        {
            CurrentParam = parameter;
            await _sceneManager.PushInstanceAsync(this.gameObject.scene, presentation: GetPresentation(parameter));
            await OnInitializeAsync(parameter);
        }

        /// <summary>
        /// すでにロード済みのこのシーンインスタンスを現在の最前面のシーンと入れ替え（Replace）し、
        /// パラメーターを渡して初期化処理を開始します。
        /// </summary>
        public async Task ReplaceAsync(T parameter)
        {
            CurrentParam = parameter;
            await _sceneManager.ReplaceInstanceAsync(this.gameObject.scene, presentation: GetPresentation(parameter));
            await OnInitializeAsync(parameter);
        }

        /// <summary>
        /// シーン内の全 GameObject をまとめてアクティブ／非アクティブにします。
        /// Push で重ねた下のシーンを隠したい場合などに使います。
        /// 非アクティブにしても OnResumeScene() は正しく呼ばれます。
        /// </summary>
        public void SetSceneActive(bool active)
        {
            foreach (var go in gameObject.scene.GetRootGameObjects())
                go.SetActive(active);
        }

        protected virtual void OnDestroy()
        {
            var unloader = this.CurrentParam as ISceneResourceUnloader;
            unloader?.UnloadResources();
        }

        private static UrsaScenePresentation GetPresentation(ISceneParameter parameter)
        {
            return parameter?.Presentation ?? UrsaScenePresentation.Fullscreen;
        }

        /// <summary>
        /// 現在最前面にある自分自身のシーンを破棄し、一つ前のシーンに戻ります。
        /// </summary>
        public async Task CloseAsync()
        {
            await OnSceneWillClose();
            await _sceneManager.PopAsync();
        }

        public virtual void OnResumeScene() { }

        public virtual void OnPauseScene() { }

        /// <summary>
        /// <c>CloseAsync()</c> が呼ばれ、シーンが閉じられる直前に呼ばれます。
        /// 保存処理や確認ダイアログなどを挟みたい場合にオーバーライドしてください。
        /// </summary>
        protected virtual async Task OnSceneWillClose()
        {
            await Task.CompletedTask;
        }

        private void LateUpdate()
        {
            if (!_handleBackKey || !IsTopScene) return;
            if (_sceneManager?.IsTransitioning == true) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                _ = OnBackKeyPressed();
        }

        /// <summary>
        /// Androidのバックキー（Escape）が押された際の処理。
        /// デフォルトでは CloseAsync() を呼び出します。
        /// 必要に応じてサブクラスでオーバーライドしてください。
        /// </summary>
        protected virtual async Task OnBackKeyPressed()
        {
            await CloseAsync();
        }
    }
}
