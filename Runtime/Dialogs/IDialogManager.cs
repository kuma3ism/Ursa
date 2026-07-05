using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Ursa
{
    /// <summary>
    /// ダイアログに渡すパラメーターのベースインターフェース
    /// </summary>
    public interface IDialogParameter
    {
        /// <summary>このダイアログを履歴（スタック）に積むかどうか。デフォルトは true。</summary>
        bool IsHistory => true;

        /// <summary>バリアタップでダイアログを閉じることを許可するか。デフォルトは false。</summary>
        bool BarrierDismissible => false;

        /// <summary>ダイアログの配置先。デフォルトは Scene。</summary>
        DialogPlacement Placement => DialogPlacement.Scene;

        /// <summary>
        /// バリアの表示スタイル。
        /// Dimmed は「未指定」として扱われ、UrsaDialogManager.DefaultBarrierStyle が適用されます。
        /// 個別ダイアログで確実に指定できるのは None / RealtimeBlur / ScreenshotBlur です。
        /// </summary>
        BarrierStyle BarrierStyle => BarrierStyle.Dimmed;
    }

    /// <summary>ダイアログの配置先を指定します。</summary>
    public enum DialogPlacement
    {
        /// <summary>現在シーンに紐付いた配置（defaultParent に配置）</summary>
        Scene,
        /// <summary>DontDestroyOnLoad として維持（システムエラー表示など）</summary>
        DontDestroyOnLoad,
    }

    /// <summary>
    /// ダイアログの後ろに表示するバリアのスタイル。
    /// UrsaDialogManager が最前面ダイアログの直下に自動配置します。
    /// </summary>
    public enum BarrierStyle
    {
        /// <summary>バリアを表示しません。</summary>
        None,
        /// <summary>
        /// 黒半透明のバリアを表示します。
        /// IDialogParameter.BarrierStyle では「未指定」として扱われ、DefaultBarrierStyle が適用されます。
        /// </summary>
        Dimmed,
        /// <summary>GrabPass を使ったリアルタイムブラーを表示します。UrsaDialogManager.BarrierMaterial の設定が必要です。</summary>
        RealtimeBlur,
        /// <summary>画面キャプチャをぼかして表示します。処理負荷が高い場合があります。</summary>
        ScreenshotBlur,
    }

    /// <summary>
    /// RealtimeBlur の実装方式を指定します。
    /// BarrierStyle は見た目の意図、RealtimeBlurMode は実装・品質方針を表します。
    /// </summary>
    public enum RealtimeBlurMode
    {
        /// <summary>
        /// 環境に応じて UrsaDialogManager が方式を選択します。
        /// Built-in RP → LegacyGrabPass、URP → RendererFeature。
        /// </summary>
        Auto,

        /// <summary>Built-in Render Pipeline 向けの GrabPass ベース実装を使用します。</summary>
        LegacyGrabPass,

        /// <summary>
        /// URP の _CameraOpaqueTexture を使った実装を使用します。
        /// URP Asset または Camera 設定で Opaque Texture を有効にする必要があります。
        /// </summary>
        CameraOpaqueTexture,

        /// <summary>
        /// URP の ScriptableRendererFeature でコピーしたカメラカラーを使用します。
        /// 使用する Universal Renderer Data に UrsaDialogBlurRendererFeature を追加する必要があります。
        /// </summary>
        RendererFeature,

        /// <summary>
        /// リアルタイムブラーが使えない環境向けのフォールバック。
        /// BarrierStyle.ScreenshotBlur と同じ処理を行うため、本来は BarrierStyle 側で
        /// "RealtimeBlurWithFallback" のような値として表現する方が設計として自然。
        /// TODO: BarrierStyle にフォールバック付きバリアントを追加し、このモードを廃止する。
        /// </summary>
        ScreenshotFallback,
    }

    /// <summary>
    /// ダイアログ開始前に必要リソースの事前DLを行うインターフェース。
    /// IDialogParameter に合わせて実装すると、Open と並行して自動実行されます。
    /// </summary>
    public interface IDialogResourcePreloader
    {
        /// <param name="progress">進捗通知。null の場合は通知なし。値は 0.0〜1.0。</param>
        Task PreloadResourcesAsync(IProgress<float> progress = null);
    }

    /// <summary>
    /// ダイアログ破棄時にリソースの解放を自動実行するインターフェース。
    /// IDialogReceiverBase.OnCloseAsync 内で自動的に呼び出されます。
    /// </summary>
    public interface IDialogResourceUnloader
    {
        void UnloadResources();
    }

    /// <summary>履歴スタック上の1エントリを表すインターフェース</summary>
    public interface IDialogHistoryEntry
    {
        int Index { get; }
        string DialogName { get; }
        Type DialogType { get; }
    }

    /// <summary>戻り値なしダイアログで使う空の値型</summary>
    public readonly struct Unit { }

    /// <summary>ダイアログが閉じられた理由</summary>
    public enum DialogCloseReason
    {
        Programmatic,
        BackKey,
        BarrierTap,
        Submit,
        Cancel,
        Timeout,
    }

    /// <summary>
    /// UrsaDialogManager から型なしで OnOpenAsync / OnCloseAsync を呼ぶための非ジェネリック契約。
    /// </summary>
    public interface IDialogReceiverBase
    {
        Task OnOpenAsync(IDialogParameter param);
        Task OnCloseAsync(DialogCloseReason reason);
    }

    /// <summary>型付きパラメーターを受け取る契約</summary>
    public interface IDialogReceiver<TParam> : IDialogReceiverBase
        where TParam : IDialogParameter
    {
        Task OnOpenAsync(TParam param);
    }

    /// <summary>結果付きの型付きパラメーターを受け取る契約</summary>
    public interface IDialogReceiver<TParam, TResult> : IDialogReceiver<TParam>
        where TParam : IDialogParameter
    {
        void Resolve(TResult result);
        void Reject(Exception error);
    }

    /// <summary>
    /// ユーザー操作で閉じるまで await できる契約。
    /// CloseAll 等の強制終了時は OperationCanceledException をスローします。
    /// </summary>
    public interface IOpenDialog<TResult>
    {
        Task<TResult> WaitForCloseAsync();
    }

    /// <summary>ダイアログのライフサイクルイベントを外部へ公開する契約。</summary>
    public interface IDialogLifecycleEvents
    {
        event Action Opened;
        event Action<DialogCloseReason> Closing;
        event Action<DialogCloseReason> Closed;
        event Action<float> PreloadProgress;
    }

    /// <summary>ダイアログの生成・破棄・履歴管理を行うマネージャーのインターフェース</summary>
    public interface IDialogManager
    {
        bool IsTransitioning { get; }
        bool HasAnyDialog { get; }
        IReadOnlyList<IDialogHistoryEntry> History { get; }
        bool IsTopDialog(MonoBehaviour dialog);

        Task<TDialog> OpenAsync<TDialog, TResult>(
            IDialogParameter parameter,
            CancellationToken ct = default)
            where TDialog : MonoBehaviour, IDialogReceiverBase, IOpenDialog<TResult>, IDialogLifecycleEvents;

        Task<TDialog> OpenAsync<TDialog>(
            IDialogParameter parameter,
            CancellationToken ct = default)
            where TDialog : MonoBehaviour, IDialogReceiverBase, IOpenDialog<Unit>, IDialogLifecycleEvents;

        Task<TResult> OpenWithCloseAsync<TDialog, TResult>(
            IDialogParameter parameter,
            Func<TDialog, Task> configure = null,
            CancellationToken ct = default)
            where TDialog : MonoBehaviour, IDialogReceiverBase, IOpenDialog<TResult>, IDialogLifecycleEvents;

        Task CloseTopAsync(DialogCloseReason reason = DialogCloseReason.Programmatic);
        Task CloseAllAsync(DialogCloseReason reason = DialogCloseReason.Programmatic);
    }
}
