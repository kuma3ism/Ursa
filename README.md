# Ursa

UnityのオレオレFramework

いろんな現場で毎回同じような実装するのでフレームワークとして起こす

- シーン管理
  - 履歴管理：シーンの追加、差替え、戻る、等の遷移の履歴を管理
  - 引数：起動時に引数を渡せる
  - トランジション：複数の候補から選択可能
  - シーンジェネレーター：シーンを自動作成
- ポップアップ管理（実装率０％）
- 音声管理（実装率０％）
 
## UPM インストール

```
https://github.com/kuma3ism/Ursa.git
```

---

## エディターメニュー（シーンテンプレート自動生成）

`Ursa/Create Scene...` または Project ビュー右クリック → `Assets/Create/Ursa/Create Scene...`

| 項目 | 説明 |
|---|---|
| Top Domain | ルートとなるドメイン名（必須）。例: `Game` |
| Sub Domain | サブドメイン名（省略可）。例: `Gacha` |
| Namespace | Top + Sub から自動生成。手動入力で上書きも可 |
| Register to Build Settings | Build Settings に自動登録 |
| Scene Name | クラス名・ファイル名になる（必須）。例: `GachaTop` |

入力例と生成されるパス：

```
Top Domain : Game
Sub Domain : Gacha
Scene Name : GachaTop
↓
Assets/Game/Gacha/Scene/GachaTop.unity
Assets/Game/Gacha/Script/GachaTop.cs
Namespace  : Game.Gacha
```

Sub Domain を省略した場合：

```
Top Domain : Game
Sub Domain : （空）
Scene Name : Boot
↓
Assets/Game/Scene/Boot.unity
Assets/Game/Script/Boot.cs
Namespace  : Game
```

> **Note:** スクリプトのアタッチはコンパイル完了後に自動実行されます。

---

## セットアップ

ゲーム起動時に `UrsaCore.Initialize()` を呼んで初期化します。
`RuntimeInitializeOnLoadMethod` を使うと MonoBehaviour 不要で自動実行できます。

```csharp
public static class UrsaInitializer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        UrsaCore.Initialize(new UrsaSceneManager());
    }
}
```

> **補足：サービスロケーターとして動作します**
> `UrsaCore` は静的なサービスロケーターです。`Initialize()` で `ISceneManager` の実装を登録し、
> 以降はどこからでも `UrsaCore.Scene` 経由でアクセスできます。


## シーンの作り方

### 1. パラメーターの定義

シーンに渡すデータを `ISceneParameter` を実装したクラスで定義します。

```csharp
public class MySceneParameter : ISceneParameter
{
    public string Message;
}
```

`IsHistory` を `false` にすると、シーンは表示されますが履歴スタックには積まれません。
バックキーで戻れないオーバーレイ表示などに使います。

```csharp
public class OverlayParameter : ISceneParameter
{
    bool ISceneParameter.IsHistory => false; // 履歴に残さない
}
```

### 2. シーンクラスの定義

#### 戻り値なし（一方通行の画面遷移）

```csharp
public class MyScene : SceneBase<MySceneParameter>
{
    protected override async Task OnInitializeAsync(MySceneParameter parameter)
    {
        // この時点で PreloadResourcesAsync は完了済み
        Debug.Log(parameter.Message);
        await Task.CompletedTask;
    }
}
```

> **【重要】** Unityの仕様上、`Awake()` / `Start()` は `OnInitializeAsync` より先に呼ばれます。  
> `CurrentParam` を参照する初期化処理は必ず `OnInitializeAsync()` に書いてください。

---

## シーン遷移 API

すべての操作は `UrsaCore.Scene` 経由で行います。

### Push（重ねる）

```csharp
await UrsaCore.Scene.PushAsync<NextScene>(new NextSceneParameter());
```

### Pop（戻る）

```csharp
await CloseAsync(); // SceneBase メソッド。自身を閉じる
// または
await UrsaCore.Scene.PopAsync();
```

### Replace（入れ替え）

```csharp
await UrsaCore.Scene.ReplaceAsync<NextScene>(new NextSceneParameter());
```

### Reset（全履歴を捨てて遷移）

```csharp
await UrsaCore.Scene.ResetAsync<TopScene>(new TopSceneParameter());
```

### Restart（ゲームを最初からやり直す）

全履歴を破棄してブートシーンを再ロードします。パラメーターなしで `ResetAsync` を呼ぶショートハンドです。

```csharp
await UrsaCore.Scene.RestartAsync<BootScene>();
```

### JumpTo（履歴内の指定シーンまで一気に戻る）

履歴スタック内で最も直近にある型のシーンまで、間にある全シーンを Pop して戻ります。
対象の型が見つからない場合は `InvalidOperationException` をスローします。

```csharp
await UrsaCore.Scene.JumpToAsync<GameScene>();
```

インデックス（0が最も古い）でも指定できます。範囲外は `ArgumentOutOfRangeException` をスローします。

```csharp
await UrsaCore.Scene.JumpToIndexAsync(0);
```

### IsTransitioning・History（状態の参照）

```csharp
if (UrsaCore.Scene.IsTransitioning) return;

foreach (var entry in UrsaCore.Scene.History)
    Debug.Log($"[{entry.Index}] {entry.SceneName} ({entry.SceneType.Name})");
```

---

## インスタンスベースの操作

シーンをロードしてから、タイミングを制御して Push / Replace することができます。

```csharp
MyScene scene = await UrsaCore.Scene.CreateSceneAsync<MyScene>();
await scene.ReplaceAsync(new MySceneParameter { Message = "Hello!" });
```

---

## override 可能なメソッド一覧

| メソッド | 呼ばれるタイミング |
|---|---|
| `OnInitializeAsync(T)` | シーン入場時（パラメーター注入後）。この時点で PreloadResourcesAsync は完了済み |
| `OnResumeScene()` | 前面シーンが閉じて自分が最前面に戻った時。トランジション有無に関わらず発火 |
| `OnPauseScene()` | 自分の上に別シーンが重なった時（OnResumeScene の逆）。トランジション有無に関わらず発火 |
| `OnSceneWillClose()` | CloseAsync() が呼ばれる直前 |
| `OnTransitionOutCompleted()` | トランジションのアウト演出完了後（画面が完全に隠れた後）。トランジションなしの場合は呼ばれない |
| `OnTransitionInStarted()` | トランジションのイン演出開始直前。トランジションなしの場合は呼ばれない |
| `OnBackKeyPressed()` | バックキー（Escape / Android バックキー）押下時 |
| `OnDestroy()` | GameObject が破棄される時（Unity標準） |

---

## コールバック例

```csharp
// 上にシーンが乗ったら自分を隠す
public override void OnPauseScene()
{
    SetSceneActive(false);
}

// 前面シーンが閉じて戻ってきたら再表示
public override void OnResumeScene()
{
    SetSceneActive(true);
}

// 閉じる直前に確認や保存処理
protected override async Task OnSceneWillClose()
{
    await SaveAsync();
}

// バックキーのカスタマイズ
protected override async Task OnBackKeyPressed()
{
    await CloseAsync();
}
```

---

## リソースの事前ダウンロード

パラメーターに `ISceneResourcePreloader` を追加すると、シーンロードと**並行して**事前DLが走ります。  
`PushAsync` / `ReplaceAsync` / `CreateSceneAsync` 呼び出し時に自動実行されます。

```csharp
public class MyParameter : ISceneParameter, ISceneResourcePreloader, ISceneResourceUnloader
{
    public Texture2D Icon;

    public async Task PreloadResourcesAsync(IProgress<float> progress = null)
    {
        Icon = await Resources.LoadAsync<Texture2D>("Icons/Hoge") as Texture2D;
    }

    public void UnloadResources()
    {
        Resources.UnloadAsset(Icon);
    }
}

// シーン側では OnInitializeAsync でアクセスできる
protected override async Task OnInitializeAsync(MyParameter parameter)
{
    _image.texture = parameter.Icon; // ロード済み
    await Task.CompletedTask;
}
```

---

## トランジション

シーン遷移時にフェードなどの演出を挟むことができます。

### セットアップ

シーンの任意の GameObject に **`TransitionController`** コンポーネントを追加します（Inspector 右クリック → `Ursa/Transition Controller`）。

追加すると `FadeTransitionEffect` Prefab が **Effect Prefab** フィールドに自動アサインされます。

> `TransitionController` が見つからない場合はトランジションなしで遷移します（エラーにはなりません）。

### 同梱 Prefab

| Prefab | 演出 |
|---|---|
| `FadeTransitionEffect` | 画面全体がじわっと黒くなる（デフォルト） |
| `AnimatorTransitionEffect` | Animator で制御するカスタム演出 |
| `ShaderWipeTransitionEffect` | 左から右に黒が流れる |
| `ShaderCircleTransitionEffect` | 中心から黒い円が広がる |
| `ShaderDissolveTransitionEffect` | ランダムにパラパラ黒くなる |

### カスタム演出を作る

`TransitionEffectBase` を継承して `PlayOutAsync` / `PlayInAsync` を実装します。

```csharp
public class MyTransition : TransitionEffectBase
{
    public override async Task PlayOutAsync() { /* 画面を覆う演出 */ }
    public override async Task PlayInAsync()  { /* 画面を開ける演出 */ }
}
```

### Prefab の再生成（開発者向け）

Scripting Define Symbols に `URSA_DEVELOPER` を追加すると `Ursa/Create Transition Prefabs` メニューが現れます。

---

## カスタムシーンローダー

`ISceneLoader` を実装することで Addressables や AssetBundle に差し替えられます。

```csharp
UrsaCore.Initialize(new UrsaSceneManager(new MyAddressablesSceneLoader()));
```

### エディターでの Build Settings 不要ロード

エディター上では `EditorSceneLoader` が自動登録されるため、Build Settings へのシーン登録なしにロードできます。

> **Note:** 同名シーンが複数存在する場合は最初に見つかったものがロードされ、警告が出ます。

---

## ログのカスタマイズ

デフォルトではログは出力されません。Scripting Define Symbols に `URSA_LOG` を追加すると有効になります。

`IUrsaLogger` を実装することで独自のログシステムに流すこともできます。

```csharp
public class MyLogger : IUrsaLogger
{
    public void Log(string message)        => MyLogSystem.Info(message);
    public void LogWarning(string message) => MyLogSystem.Warn(message);
}

UrsaCore.Initialize(new UrsaSceneManager(logger: new MyLogger()));
// Addressables と組み合わせる場合
UrsaCore.Initialize(new UrsaSceneManager(new MyAddressablesSceneLoader(), new MyLogger()));
```

---

## ダイアログ管理インターフェース案（Scene API 準拠）

シーン管理と同じ思想（履歴スタックと非同期制御）を活かしつつ、
ダイアログは `Open/Close` 中心で統一すると運用しやすいです。

### 目標

- シーンと同じ呼び方で学習コストを下げる
- ダイアログを結果付きで await できる（Confirm の OK/Cancel など）
- バックキー優先順位を「ダイアログ > シーン」に固定する
- 履歴に残す/残さない（トースト系）を選べる

### 最小構成インターフェース

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Ursa.Dialogs
{
    public enum DialogPlacement
    {
        Scene,              // デフォルト: 現在シーン配下に配置
        DontDestroyOnLoad   // シーン跨ぎで維持（システムエラー表示など）
    }

    public interface IDialogParameter
    {
        bool IsHistory => true;
        bool BarrierDismissible => false;
        DialogPlacement Placement => DialogPlacement.Scene;
    }

    // Scene と同様に、Open と並行して必要リソースを先読みする
    public interface IDialogResourcePreloader
    {
        Task PreloadResourcesAsync(IProgress<float> progress = null);
    }

    // Close 時に後始末したい場合の任意フック
    public interface IDialogResourceUnloader
    {
        void UnloadResources();
    }

    public interface IDialogHistoryEntry
    {
        int Index { get; }
        string DialogName { get; }
        Type DialogType { get; }
    }

    // OpenAsync で返されるダイアログ本体が実装する共通契約
    // WaitForCloseAsync は「閉じる命令」ではなく「ユーザー操作で閉じるまで待つ」用途
    // ダイアログが CloseAll で一括終了された場合は OperationCanceledException をスローする
    public interface IOpenDialog<TResult>
    {
        Task<TResult> WaitForCloseAsync();
    }

    // ユーザーコードが購読できる公開イベント（Scene のコールバック思想に合わせる）
    // ※ SceneBase が ISceneBackHandler / ISceneTransitionHandler を実装するのと同様、
    //   ダイアログ側の実装クラスがこのインターフェースを実装してイベントを発火する
    public interface IDialogLifecycleEvents
    {
        event Action Opened;
        event Action<DialogCloseReason> Closing;
        event Action<DialogCloseReason> Closed;
        event Action<float> PreloadProgress;
    }

    public interface IDialogManager
    {
        bool IsTransitioning { get; }
        bool HasAnyDialog { get; }
        IReadOnlyList<IDialogHistoryEntry> History { get; }

        // 同種ダイアログの多重表示は常に許可（メッセージ違いの Confirm を連続で開ける）
        // parameter が IDialogResourcePreloader を実装していれば、
        // Dialog生成と並行して PreloadResourcesAsync を走らせる
        // TParam は TDialog が実装する IDialogReceiver<TParam, TResult> から解決されるため
        // 呼び出し側での明示は不要（Scene の PushAsync<TScene>(param) と同じ分担）
        Task<TDialog> OpenAsync<TDialog, TResult>(
            IDialogParameter parameter,
            CancellationToken ct = default)
            where TDialog : Component, IOpenDialog<TResult>, IDialogLifecycleEvents;

        Task<TDialog> OpenAsync<TDialog>(
            IDialogParameter parameter,
            CancellationToken ct = default)
            where TDialog : Component, IOpenDialog<Unit>, IDialogLifecycleEvents;

        // Open + (任意でインスタンスを設定) + Close待機 を1行で行うショートハンド
        // configure は非同期も扱えるよう Func<TDialog, Task>（不要なら null）
        Task<TResult> OpenWithCloseAsync<TDialog, TResult>(
            IDialogParameter parameter,
            Func<TDialog, Task> configure = null,
            CancellationToken ct = default)
            where TDialog : Component, IOpenDialog<TResult>, IDialogLifecycleEvents;

        Task CloseTopAsync(DialogCloseReason reason = DialogCloseReason.Programmatic);
        Task CloseAllAsync(DialogCloseReason reason = DialogCloseReason.Programmatic);
    }

    public readonly struct Unit { }

    public enum DialogCloseReason
    {
        Programmatic,
        BackKey,
        BarrierTap,
        Submit,
        Cancel,
        Timeout
    }

    public interface IDialogReceiver<TParam>
        where TParam : IDialogParameter
    {
        Task OnOpenAsync(TParam param);
        Task OnCloseAsync(DialogCloseReason reason);
    }

    public interface IDialogReceiver<TParam, TResult> : IDialogReceiver<TParam>
        where TParam : IDialogParameter
    {
        void Resolve(TResult result);
        void Reject(Exception error);
    }

    // View 側（dialog prefab）が実装する任意の public API 例
    public interface IConfirmDialogView
    {
        void SetTitle(string title);
        void SetMessage(string message);
        event Action OnOkClicked;
        event Action OnCancelClicked;
    }
}
```

### 実装方針（Ursa向け）

1. **`UrsaCore.Dialog` を追加**
   - `UrsaCore.Scene` と同じ参照導線にして利用感を揃える
2. **配置先をパラメーターで切り替え**
   - デフォルトは `DialogPlacement.Scene`（現在シーン配下）
   - フラグで `DialogPlacement.DontDestroyOnLoad` を選ぶとシーン跨ぎで維持
   - システムエラーのような全体通知ダイアログに向く
3. **Dialog 用の事前DLも用意する**
   - `IDialogResourcePreloader` を parameter に実装すると、Open と並行して事前DL
   - Close 時に `IDialogResourceUnloader` で解放可能
4. **Open/Close に寄せる**
   - `Push/Pop` ではなく `OpenAsync` / `WaitForCloseAsync` を正規 API にする
5. **Replace は作らない**
   - 必要なら `await WaitForCloseAsync(); await OpenAsync(...);` を明示的に実行
6. **インスタンスを外へ直接返す**
   - `OpenAsync` は `TDialog` 本体を返し、public メソッドやイベント購読を直接行える
7. **ユーザーに公開するライフサイクルイベントを持たせる**
   - SceneBase と同様に、イベント発火はダイアログ実装クラス側の責務にする
   - `Opened` / `Closing` / `Closed` / `PreloadProgress` を外部購読可能にする
   - パラメーター受け取りは `IDialogReceiver<TParam>.OnOpenAsync(TParam)` で統一
8. **同種ダイアログの多重表示は常に許可**
   - 制御フラグは設けず、同じ型のダイアログを複数同時に開ける前提とする
9. **`WaitForCloseAsync()` は待機 API・エラー時は例外**
   - ユーザー操作で閉じるまで待機し、結果を非nullable で返す
   - `CloseAll` などで外部から強制終了された場合は `OperationCanceledException` をスローする
10. **1行で完結するショートハンドも用意**
    - `OpenWithCloseAsync` で Open→設定→Close待機(WaitForClose) をまとめて実行できる
    - `configure` は `Func<TDialog, Task>` とし、非同期の UI 初期化にも対応
11. **強制クローズ API は Manager 側に分離**
    - `CloseTopAsync` / `CloseAllAsync` を管理系 API として提供

### 使用イメージ

```csharp
var confirmDialog = await UrsaCore.Dialog.OpenAsync<ConfirmDialog, bool>(
    new ConfirmParam { Title = "破棄しますか？" });

confirmDialog.SetMessage("この操作は取り消せません");
confirmDialog.Opened += () => Debug.Log("Confirm opened");
confirmDialog.Closed += reason => Debug.Log($"Confirm closed: {reason}");

try
{
    bool result = await confirmDialog.WaitForCloseAsync(); // ユーザーが閉じるまで待機
    if (result)
    {
        await UrsaCore.Scene.PopAsync();
    }
}
catch (OperationCanceledException)
{
    // CloseAll などで強制終了された場合
}
```

ショートハンド版（`OpenWithCloseAsync`）:

```csharp
try
{
    bool isOk = await UrsaCore.Dialog.OpenWithCloseAsync<ConfirmDialog, bool>(
        new ConfirmParam { Title = "破棄しますか？" },
        async dialog => await dialog.SetMessageAsync("この操作は取り消せません"));

    if (isOk)
    {
        await UrsaCore.Scene.PopAsync();
    }
}
catch (OperationCanceledException)
{
    // CloseAll などで強制終了された場合
}
```

シーン跨ぎのシステムエラー表示例（`DontDestroyOnLoad` 配置）:

```csharp
await UrsaCore.Dialog.OpenWithCloseAsync<SystemErrorDialog, Unit>(
    new SystemErrorParam
    {
        Message = "通信に失敗しました",
        Placement = DialogPlacement.DontDestroyOnLoad
    });
```

Dialog用の事前DL例（Scene と同じ思想）:

```csharp
public class ConfirmParam : IDialogParameter, IDialogResourcePreloader, IDialogResourceUnloader
{
    public string Title;
    public Sprite Icon;

    public async Task PreloadResourcesAsync(IProgress<float> progress = null)
    {
        Icon = await Addressables.LoadAssetAsync<Sprite>("confirm_icon").Task;
        progress?.Report(1f);
    }

    public void UnloadResources()
    {
        if (Icon != null) Addressables.Release(Icon);
    }
}
```

### 先に決めると良い設計ポイント

- 同時表示数の上限（無制限か、上限Nか）
- タイムアウト時の扱い（`Timeout` を `Cancel` 扱いにするか）
- 背面ダイアログの入力ロックポリシー（最前面のみ操作可能にするか）
- `DontDestroyOnLoad` ダイアログの寿命管理（いつ自動Closeするか）
- 事前DL失敗時の方針（リトライ/フォールバック/即Close）
