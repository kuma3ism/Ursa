# Ursa

Unityの俺俺フレームワーク（まだいろいろ作成中）

いろんな現場で毎回同じような実装するのでフレームワークとして起こす

- シーン管理（完了）
  - 履歴管理：シーンの追加、差替え、戻る、等の遷移の履歴を管理
  - 引数：起動時に引数を渡せる
  - トランジション：複数の候補から選択可能
  - シーンジェネレーター：シーンを自動作成
- UIボタン管理（完成）
- **ダイアログ管理（着手）**
- 音声管理（未着手）
 
## UPM インストール

```
https://github.com/kuma3ism/Ursa.git
```

## セットアップ

Ursa では以下の 2 つの方法から選択して初期化できます。
混在させると混乱を招くため、プロジェクトで統一した運用を推奨します。

### A. UrsaCore のサービスロケーターを使う

ゲーム起動時に `UrsaCore.Initialize()` を呼んで、各管理システムを静的サービスロケーターに登録します。
`RuntimeInitializeOnLoadMethod` を使うと MonoBehaviour 不要で自動実行できます。

```csharp
public static class UrsaInitializer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        UrsaCore.Initialize(new UrsaSceneManager());
        UrsaCore.Initialize(new UrsaDialogManager());
    }
}
```

以降は `UrsaCore.Scene` / `UrsaCore.Dialog` 経由でアクセスできます。
登録されていない領域にアクセスすると `InvalidOperationException` がスローされます。

```csharp
await UrsaCore.Scene.PushAsync<NextScene>(new NextSceneParameter());
```

> **補足：UI 管理システムについて**
> `UrsaCore.UI` と `UrsaUIManager` は将来の拡張用に用意されていますが、
> 現状の Ursa 内部機能（シーン遷移・ダイアログ・UrsaButton）では使用していません。
> そのため、通常の利用では `UrsaCore.Initialize(new UrsaUIManager())` は不要です。
> 独自の UI 実行ロックやブロッカーを実装して差し込みたい場合に利用してください。

### B. DIコンテナからインターフェースを受け取る

既に DIコンテナ（例：VContainer、Zenject）を導入済みの場合は、
`ISceneManager` / `IDialogManager` の実装をコンテナに登録し、必要なクラスに注入して使います。

```csharp
// VContainer の例
public class UrsaLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterInstance<ISceneManager>(new UrsaSceneManager());
        builder.RegisterInstance<IDialogManager>(new UrsaDialogManager());
    }
}
```

注入先の例：

```csharp
public class SomePresenter
{
    private readonly ISceneManager _sceneManager;

    public SomePresenter(ISceneManager sceneManager)
    {
        _sceneManager = sceneManager;
    }
}
```

| 選び方 | おすすめのケース |
|--------|----------------|
| A. UrsaCore を使う | 小規模なプロジェクト、DI導入を検討していない |
| B. DI を使う | 既にDIコンテナを導入済み、テストや差し替えを重視する |


---

## メニュからシーンを作成

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

## シーン使い方

### 1. パラメーターの定義

シーンに渡すデータを `ISceneParameter` を実装したクラスで定義します。

```csharp
public class MySceneParameter : ISceneParameter
{
    public string Message;
}
```

`Presentation` を `Overlay` にすると、背面シーンを表示したまま重ねられます。
通常の画面遷移はデフォルトの `Fullscreen` を使います。

```csharp
public class OverlayParameter : ISceneParameter
{
    UrsaScenePresentation ISceneParameter.Presentation => UrsaScenePresentation.Overlay;
}
```

`IsHistory` を `false` にすると、シーンは表示されますが履歴スタックには積まれません。
通常は `Overlay` 表示でも履歴に積む方が、`CloseAsync()` や戻る操作と相性がよいです。

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
`transitionName` を省略するとデフォルトで **Fade** が使用されます。

### Push（重ねる）

```csharp
await UrsaCore.Scene.PushAsync<NextScene>(new NextSceneParameter());

// トランジションを指定する場合
await UrsaCore.Scene.PushAsync<NextScene>(new NextSceneParameter(), TransitionType.Dissolve);
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

解放処理が必要な場合は `ISceneResourceUnloader` も追加します。シーン破棄時（`OnDestroy`）に自動で呼ばれます。  
どちらか片方だけの実装も可能です。

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
デフォルトは **Fade** です。トランジションなしで遷移したい場合は `null` を渡してください。

```csharp
await UrsaCore.Scene.PushAsync<NextScene>(param);                      // Fade（デフォルト）
await UrsaCore.Scene.PushAsync<NextScene>(param, TransitionType.Spade); // Spade
await UrsaCore.Scene.PushAsync<NextScene>(param, null);                 // トランジションなし
```

### 組み込みトランジション名

`TransitionType` は文字列定数クラスです。`UrsaSettings` に登録された名前と対応します。

| 定数 | 文字列値 | 演出 |
|---|---|---|
| `TransitionType.Fade` | `"Fade"` | 画面全体がじわっと黒くなる（デフォルト） |
| `TransitionType.Wipe` | `"Wipe"` | 左から右に黒が流れる |
| `TransitionType.Circle` | `"Circle"` | 中心から黒い円が広がる |
| `TransitionType.Spade` | `"Spade"` | スペードが中央から拡大・縮小する（Animator） |

### UrsaSettings

`Assets/Resources/Ursa/UrsaSettings.asset` でトランジション名とプレハブのマッピングを管理しています。  
エディター初回起動時に同梱プレハブが自動登録されます。独自のトランジションを追加する場合は Inspector から直接登録できます。

### シーン固有のトランジション（TransitionController）

シーンの任意の GameObject に **`TransitionController`** コンポーネントを追加すると、  
`transitionName` が UrsaSettings に見つからない場合のフォールバックとして使用されます。

> `TransitionController` も UrsaSettings にも該当するエフェクトがない場合はトランジションなしで遷移します（エラーにはなりません）。

### カスタム演出を作る

`TransitionEffectBase` を継承して `PlayOutAsync` / `PlayInAsync` を実装します。

```csharp
public class MyTransition : TransitionEffectBase
{
    public override async Task PlayOutAsync() { /* 画面を覆う演出 */ }
    public override async Task PlayInAsync()  { /* 画面を開ける演出 */ }
}
```

作成したプレハブを UrsaSettings の Transitions リストに任意の名前で登録すると、その名前で呼び出せます。

```csharp
await UrsaCore.Scene.PushAsync<NextScene>(param, "MyCustomTransition");
```

### Prefab の再生成（開発者向け）

| メニュー | 用途 |
|---|---|
| `Ursa/Setup Spade Transition` | Spade トランジションのアニメーション・コントローラー・プレハブを再生成 |
| `Ursa/Create Transition Prefabs` | 全トランジションプレハブを再生成（`URSA_DEVELOPER` 定義が必要） |

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

## ダイアログ管理

シーンとは独立したスタック管理で、確認ダイアログ・ローディング表示・システムエラーなどに利用できます。

### セットアップ

`UrsaCore.Initialize(new UrsaDialogManager(...))` でダイアログ管理システムを初期化します。

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
private static void Initialize()
{
    UrsaCore.Initialize(new UrsaSceneManager());
    UrsaCore.Initialize(new UrsaDialogManager());
}
```

`UrsaDialogManager` のコンストラクタには以下の引数を渡せます。

| 引数 | 型 | 説明 |
|---|---|---|
| `defaultParent` | `RectTransform` | ダイアログを配置する親。省略すると DontDestroyOnLoad な Canvas が自動生成されます |
| `loader` | `IDialogLoader` | Prefab ローダー。省略すると `ResourcesDialogLoader`（後述）が使われます |
| `logger` | `IUrsaLogger` | ロガー。省略すると NullLogger（`URSA_LOG` 定義時は UnityDebugLogger） |

### Prefab の配置

デフォルトの `ResourcesDialogLoader` は `Resources/Dialogs/{ダイアログクラス名}.prefab` を読み込みます。

```
Assets/Resources/Dialogs/ConfirmDialog.prefab   ← ConfirmDialog クラスに対応
Assets/Resources/Dialogs/LoadingDialog.prefab   ← LoadingDialog クラスに対応
```

パスを変えたい場合はコンストラクタの `basePath` を指定します。

```csharp
new UrsaDialogManager(loader: new ResourcesDialogLoader("UI/Popups"));
```

### ダイアログの作り方

#### 1. パラメーターの定義

```csharp
public class ConfirmDialogParameter : IDialogParameter
{
    public string Message;

    // バリア（背景）タップで閉じることを許可する場合
    bool IDialogParameter.BarrierDismissible => true;
}
```

`IDialogParameter` のプロパティ：

| プロパティ | デフォルト | 説明 |
|---|---|---|
| `IsHistory` | `true` | 履歴スタックに積むかどうか。`false` にすると積まれません |
| `BarrierDismissible` | `false` | バリア（背景）タップで閉じることを許可するか |
| `Placement` | `Scene` | `Scene`（defaultParent に配置）または `DontDestroyOnLoad` |
| `BarrierStyle` | `Dimmed` | バリアの見た目。`None` / `RealtimeBlur` / `ScreenshotBlur` は個別指定として扱われます |

> **BarrierStyle の注意**  
> 現在の API では `BarrierStyle.Dimmed` を「未指定」として扱い、`UrsaDialogManager.DefaultBarrierStyle` を適用します。  
> そのため `DefaultBarrierStyle = BarrierStyle.RealtimeBlur` の状態では、個別ダイアログだけを明示的に `Dimmed` に戻すことはできません。個別指定として使えるのは `None` / `RealtimeBlur` / `ScreenshotBlur` です。

#### 2. ダイアログクラスの定義

**戻り値あり**（確認ダイアログなど）：

```csharp
public class ConfirmDialog : DialogBase<ConfirmDialogParameter, bool>
{
    [SerializeField] private TMP_Text _messageText;

    protected override Task OnOpenAsync(ConfirmDialogParameter param)
    {
        _messageText.text = param.Message;
        return Task.CompletedTask;
    }

    // 「OK」ボタン
    public void OnOkPressed() => _ = CloseAsync(true);

    // 「キャンセル」ボタン
    public void OnCancelPressed() => _ = CloseAsync(false);
}
```

**戻り値なし**（通知・ローディングダイアログなど）：

```csharp
public class LoadingDialog : DialogBase<LoadingParameter>
{
    protected override Task OnOpenAsync(LoadingParameter param)
    {
        // ローディング表示の初期化など
        return Task.CompletedTask;
    }
}
```

> **【重要】** Unityの仕様上、`Awake()` / `Start()` は `OnOpenAsync` より先に呼ばれます。  
> パラメーターを参照する初期化処理は必ず `OnOpenAsync()` に書いてください。

### ダイアログ API

すべての操作は `UrsaCore.Dialog` 経由で行います。

#### OpenWithCloseAsync（開いて結果を受け取る）

ダイアログを開き、閉じられるまで待機してから結果を受け取ります。

```csharp
bool result = await UrsaCore.Dialog.OpenWithCloseAsync<ConfirmDialog, bool>(
    new ConfirmDialogParameter { Message = "本当に削除しますか？" }
);

if (result)
{
    // OK が押された
}
```

`configure` で開いた後の追加設定（非同期 UI 初期化など）も可能です：

```csharp
bool result = await UrsaCore.Dialog.OpenWithCloseAsync<ConfirmDialog, bool>(
    new ConfirmDialogParameter { Message = "削除しますか？" },
    configure: async dialog =>
    {
        await dialog.SetupAsync(); // 非同期の追加初期化
    }
);
```

#### OpenAsync（開くだけ）

ダイアログを開いたまま他の処理を続けたい場合に使います。

```csharp
// 戻り値あり
LoadingDialog loading = await UrsaCore.Dialog.OpenAsync<LoadingDialog>(new LoadingParameter());

await SomeHeavyWorkAsync();

await UrsaCore.Dialog.CloseTopAsync();

// 閉じるまで任意のタイミングで待機することも可能
bool result = await UrsaCore.Dialog.OpenAsync<ConfirmDialog, bool>(param)
                                    .WaitForCloseAsync(); // ← 別途待機
```

#### CloseTopAsync / CloseAllAsync

```csharp
// 最前面のダイアログを閉じる
await UrsaCore.Dialog.CloseTopAsync();

// 全ダイアログを閉じる
await UrsaCore.Dialog.CloseAllAsync();

// 理由を指定する場合
await UrsaCore.Dialog.CloseTopAsync(DialogCloseReason.Timeout);
```

#### 状態の参照

```csharp
if (UrsaCore.Dialog.IsTransitioning) return;
if (UrsaCore.Dialog.HasAnyDialog) return;

foreach (var entry in UrsaCore.Dialog.History)
    Debug.Log($"[{entry.Index}] {entry.DialogName}");
```

### DialogCloseReason

`OnCloseAsync` や `CloseTopAsync` / `CloseAllAsync` に渡す、ダイアログが閉じられた理由です。

| 値 | 説明 |
|---|---|
| `Programmatic` | コードから明示的に閉じた（デフォルト） |
| `BackKey` | バックキー（Escape / Android バックキー）で閉じた |
| `BarrierTap` | バリア（背景）タップで閉じた |
| `Submit` | 確定操作（`CloseAsync(result)` 呼び出し）で閉じた |
| `Cancel` | キャンセル操作で閉じた |
| `Timeout` | タイムアウトで閉じた |

### override 可能なメソッド一覧

| メソッド | 呼ばれるタイミング |
|---|---|
| `OnOpenAsync(TParam)` | ダイアログが開かれた時（パラメーター注入後） |
| `OnCloseAsync(DialogCloseReason)` | ダイアログが閉じられる直前 |
| `OnBackKeyPressed()` | バックキー（Escape / Android バックキー）押下時 |
| `OnDestroy()` | GameObject が破棄される時（Unity標準） |

### リソースの事前ダウンロード

パラメーターに `IDialogResourcePreloader` を追加すると、ダイアログロードと**並行して**事前DLが走ります。

```csharp
public class MyDialogParameter : IDialogParameter, IDialogResourcePreloader, IDialogResourceUnloader
{
    public Sprite Icon;

    public async Task PreloadResourcesAsync(IProgress<float> progress = null)
    {
        Icon = await Resources.LoadAsync<Sprite>("Icons/Hoge") as Sprite;
    }

    public void UnloadResources()
    {
        Resources.UnloadAsset(Icon);
    }
}
```

### カスタムダイアログローダー

`IDialogLoader` を実装することで Addressables や AssetBundle に差し替えられます。

```csharp
UrsaCore.Initialize(new UrsaDialogManager(loader: new MyAddressablesDialogLoader()));
```

---

## UrsaButton

`UrsaButton` は Unity の `Button` コンポーネントに連打防止・グループブロック・長押しを追加する UI コンポーネントです。
`Button` コンポーネントと同じ GameObject に追加して使います。

### セットアップ

Prefab または GameObject に `UrsaButton` コンポーネントを追加するだけで動作します。  
同じ GameObject にある Unity 標準の `Button` と連携し、クリック処理・連打防止・長押しをまとめて扱えます。

### インターフェース（IUrsaButton）

Presenter や UseCase から操作する場合は `IUrsaButton` インターフェース経由での参照を推奨します。

```csharp
[SerializeField] private UrsaButton _button;

private IUrsaButton Button => _button;

private void Start()
{
    Button.SetOnClick(OnButtonClicked);
}
```

### クリックハンドラーの登録

#### 同期処理

```csharp
_button.SetOnClick(() =>
{
    Debug.Log("押された");
});
```

#### 非同期処理（CancellationToken あり）

```csharp
_button.SetOnClick(async ct =>
{
    await SomeAsyncTask(ct);
});
```

#### 外部 CancellationTokenSource とのリンク

外部 CTS が再生成されるケース（ループ処理など）では、`externalCtsProvider` オーバーロードを使うと  
UrsaButton 内部のトークンと安全にリンクできます。

```csharp
_button.SetOnClick(
    externalCtsProvider: () => _externalCts,
    handler: async ct =>
    {
        // UrsaButton の CT と外部 CT のどちらかがキャンセルされると止まる
        await SomeAsyncTask(ct);
    }
);
```

### IUrsaButtonAction

`IUrsaButtonAction` を実装したコンポーネントを同じ GameObject にアタッチすると、  
`SetOnClick` とは独立してクリック時に `Execute()` が呼ばれます。コードを書かずにボタンへ挙動を付与したい場合に使います。

```csharp
public class MyButtonEffect : MonoBehaviour, IUrsaButtonAction
{
    public void Execute()
    {
        // SE 再生や演出など
    }
}
```

> **Note:** `IUrsaButtonAction.Execute()` は `SetOnClick` ハンドラーより先に呼ばれます。  
> グループブロック・セルフブロックは両方に同様に適用されます。

### ゲート設定（連打防止）

#### セルフブロック

同じボタンの連打を防止するインターバルを設定します。デフォルトは `0.5` 秒です。

```csharp
_button.SetGateInterval(1.0f); // 1秒間は同じボタンを押せない
_button.SetGateInterval(0f);   // 連打を許可する
```

Inspector の **Self Block** ヘッダーからも設定できます。

#### グループブロック

同じグループに属するボタンのハンドラーが実行中は、そのグループ内の他のボタンも押せなくなります（デフォルト有効）。

グループは自動判定されます。親に `UrsaButtonGroup` があればそれを使い、なければ `DialogBase` / `SceneBase` / Canvas / 自分自身の順にフォールバックします。
Dialog と Scene はそれぞれ独立したスコープになるため、シーン側のボタンで `OpenWithCloseAsync` のようにダイアログが閉じるまで待っていても、開いたダイアログ内の OK / Cancel ボタンは別スコープとして押せます。

任意の UI パネル内だけをひとまとめにしたい場合は、親 GameObject に `UrsaButtonGroup` を追加します。

```csharp
_button.SetIgnoreGroupBlock(true);       // グループブロックを無視する
_button.SetIgnoreGlobalBlock(true);      // 互換用の旧名
```

Inspector の **Group Block** ヘッダーからも設定できます。

### キャンセル

#### ボタンを無効化したときの自動キャンセル

`gameObject.SetActive(false)` や `enabled = false` でボタンが無効になると、実行中のハンドラーは自動的にキャンセルされます。

```csharp
_button.SetOnClick(async ct =>
{
    await Task.Delay(5000, ct); // ← ボタン無効化でキャンセルされる
});

// 別の処理でボタンを無効化
gameObject.SetActive(false); // 実行中のタスクがキャンセルされる
```

#### ハンドラー再登録によるキャンセル

`SetOnClick` を再度呼ぶと、実行中のハンドラーがキャンセルされます。

```csharp
// 最初のハンドラーを登録
_button.SetOnClick(async ct =>
{
    await Task.Delay(10000, ct);
    Debug.Log("完了");
});

// 再登録すると実行中のタスクがキャンセルされる
_button.SetOnClick(async ct =>
{
    Debug.Log("新しいハンドラー");
    await Task.CompletedTask;
});
```

#### CancellationToken を使った明示的なキャンセル

```csharp
private CancellationTokenSource _cts;

private void Start()
{
    _cts = new CancellationTokenSource();

    _button.SetOnClick(async ct =>
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
        await SomeAsyncTask(linked.Token);
    });
}

// 任意のタイミングでキャンセル
public void CancelTask()
{
    _cts.Cancel();
    _cts.Dispose();
    _cts = new CancellationTokenSource();
}

private void OnDestroy()
{
    _cts?.Cancel();
    _cts?.Dispose();
}
```

> **Note:** 外部 CTS を毎回使い回すケースでは、`externalCtsProvider` オーバーロードの使用も検討してください。

### 長押し

長押し完了までの進行度（0〜1）を受け取りながら、完了時に処理を実行できます。

#### 同期

```csharp
_button.SetOnLongClick(
    duration: 2.0f,                              // 長押し判定までの秒数
    onHolding: progress =>
    {
        _gauge.fillAmount = progress;            // 0〜1 で進行度を受け取る
    },
    onHoldComplete: () =>
    {
        Debug.Log("長押し完了");
    }
);
```

#### 非同期（CancellationToken あり）

```csharp
_button.SetOnLongClickAsync(
    duration: 2.0f,
    onHolding: progress =>
    {
        _gauge.fillAmount = progress;
    },
    onHoldComplete: async ct =>
    {
        await DeleteDataAsync(ct);
    }
);
```

> **Note:** 長押し成立後にボタンを離しても通常のクリックハンドラーは発火しません。

### Inspector 設定一覧

| 項目 | デフォルト | 説明 |
|---|---|---|
| Gate Interval | `0.5` | セルフブロックのインターバル（秒）。0 で無効 |
| Ignore Group Block | `false` | true にすると同じグループのブロックを無視する |

## License

MIT

Copyright (c) 2026 kuma3ism
