# Ursa

Unityの俺俺フレームワーク

いろんな現場で毎回同じような実装するのでフレームワークとして起こす

- シーン管理
  - 履歴管理：シーンの追加、差替え、戻る、等の遷移の履歴を管理
  - 引数：起動時に引数を渡せる
  - 戻り値：シーンの終了時に戻り値も設定できる（あまり使われないと思う）
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
| With Result | `SceneBaseWithResult<TParam, TResult>` 版を生成 |
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
        // パラメーターを使った初期化処理（CurrentParam もここから使える）
        Debug.Log(parameter.Message);
        await Task.CompletedTask;
    }
}
```

#### 戻り値あり（ポップアップ・確認ダイアログなど）

```csharp
// 戻り値の型を定義
public class MySceneResult
{
    public bool IsConfirmed;
    public string Message;
}

public class MyScene : SceneBaseWithResult<MySceneParameter, MySceneResult>
{
    protected override async Task OnInitializeAsync(MySceneParameter parameter)
    {
        Debug.Log(parameter.Message);
        await Task.CompletedTask;
    }

    // 結果を返して閉じる（シーン自身から呼ぶ）
    async void OnConfirmButton()
    {
        await CloseAsync(new MySceneResult { IsConfirmed = true });
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

全履歴を破棄してブートシーンを再ロードします。`ResetAsync` と同じ動きですが、意図を明示したい場合に使います。
パラメーターは渡せないため、ブートシーンがパラメーターを必要としない場合に適しています。

```csharp
// UrsaCore 経由
await UrsaCore.Scene.RestartAsync<BootScene>();

// 自前のシングルトンや DI で ISceneManager を持っている場合も同様に呼べます
await mySceneManager.RestartAsync<BootScene>();
```

### JumpTo（履歴内の指定シーンまで一気に戻る）

履歴スタック内で最も直近にある型のシーンまで、間にある全シーンを Pop して戻ります。
対象の型が見つからない場合は `InvalidOperationException` をスローします。

```csharp
await UrsaCore.Scene.JumpToAsync<GameScene>();
```

インデックス（0が最も古い）でも指定できます。範囲外は `ArgumentOutOfRangeException` をスローします。

```csharp
await UrsaCore.Scene.JumpToIndexAsync(0); // 最初のシーンまで戻る
```

### IsTransitioning・History（状態の参照）

```csharp
// 遷移中かどうか
if (UrsaCore.Scene.IsTransitioning) return;

// 現在の履歴スタック（0が最も古い）
foreach (var entry in UrsaCore.Scene.History)
{
    Debug.Log($"[{entry.Index}] {entry.SceneName} ({entry.SceneType.Name})");
}
```

---

## インスタンスベースの操作

シーンをロードしてから、タイミングを制御して Push / Replace することができます。

```csharp
// ロード（まだ履歴には積まれない）
MyScene scene = await UrsaCore.Scene.CreateSceneAsync<MyScene>();

// Replace して開く（現在のシーンと入れ替え）
await scene.ReplaceAsync(new MySceneParameter { Message = "Hello!" });
```

---

## 戻り値を受け取る（ポップアップ待機）

```csharp
var result = await UrsaCore.Scene.OpenResultAsync<MyScene, MySceneParameter, MySceneResult>(param);
Debug.Log(result.IsConfirmed);
```

---

## コールバック

### `OnResumeScene()`

子シーンが閉じられ、自分が再び最前面になったときに呼ばれます。

```csharp
public override void OnResumeScene()
{
    base.OnResumeScene();
    // 再表示時の処理（リスト再取得など）
}
```

### `OnBackKeyPressed()`（Android バックキー・Escape 対応）

インスペクターの `Handle Back Key` がON（デフォルト）かつ最前面のシーンのとき、バックキーで呼ばれます。

```csharp
// デフォルト動作: CloseAsync() が呼ばれる
// カスタマイズしたい場合はオーバーライド:
protected override void OnBackKeyPressed()
{
    _ = CloseAsync(new MySceneResult { IsConfirmed = false }); // 戻り値ありの場合
}
```

---

## リソースの事前ダウンロード

パラメーターに `ISceneResourcePreloader` を追加すると、シーンロードと**並行して**事前DLが走ります。  
`PushAsync` / `ReplaceAsync` / `CreateSceneAsync` 呼び出し時に自動実行されます。

```csharp
public class MyParameter : ISceneParameter, ISceneResourcePreloader
{
    public async Task PreloadResourcesAsync(IProgress<float> progress = null)
    {
        // Addressables.LoadAssetAsync(...) など
        await Task.Delay(1000); // 例
    }
}
```

## リソースの自動解放

パラメーターに `ISceneResourceUnloader` を追加すると、シーン破棄（`OnDestroy`）時に自動解放されます。

```csharp
public class MyParameter : ISceneParameter, ISceneResourceUnloader
{
    public void UnloadResources()
    {
        // Addressables.Release(...) など
    }
}
```

---

## トランジション

シーン遷移時にフェードなどの演出を挟むことができます。

### セットアップ

シーンの任意の GameObject に **`TransitionController`** コンポーネントを追加します（Inspector 右クリック → `Ursa/Transition Controller`）。

追加すると `FadeTransitionEffect` Prefab が **Effect Prefab** フィールドに自動アサインされます。

```
SampleScene (GameObject)
  └─ TransitionController
       └─ Effect Prefab: FadeTransitionEffect (自動アサイン)
```

> `TransitionController` が見つからない場合はトランジションなしで遷移します（エラーにはなりません）。

### 同梱 Prefab

| Prefab | 演出 |
|---|---|
| `FadeTransitionEffect` | 画面全体がじわっと黒くなる（デフォルト） |
| `AnimatorTransitionEffect` | Animator で制御するカスタム演出 |
| `ShaderWipeTransitionEffect` | 左から右に黒が流れる |
| `ShaderCircleTransitionEffect` | 中心から黒い円が広がる |
| `ShaderDissolveTransitionEffect` | ランダムにパラパラ黒くなる |

別の演出に切り替えるには **Effect Prefab** フィールドを差し替えるだけです。

### カスタム演出を作る

`TransitionEffectBase` を継承して `PlayOutAsync` / `PlayInAsync` を実装します。

```csharp
public class MyTransition : TransitionEffectBase
{
    public override async Task PlayOutAsync()
    {
        // 画面を覆う演出
    }

    public override async Task PlayInAsync()
    {
        // 画面を開ける演出
    }
}
```

### Prefab の再生成（開発者向け）

Scripting Define Symbols に `URSA_DEVELOPER` を追加すると `Ursa/Create Transition Prefabs` メニューが現れ、Prefab を再生成できます。

---

## カスタムシーンローダー

`ISceneLoader` を実装することで Addressables や AssetBundle に差し替えられます。

```csharp
UrsaCore.Initialize(new UrsaSceneManager(new MyAddressablesSceneLoader()));
```

---

## ログのカスタマイズ

`IUrsaLogger` を実装することで、フレームワーク内部のログ出力を差し替えられます。

```csharp
// デフォルト：Debug.Log / Debug.LogWarning に出力
UrsaCore.Initialize(new UrsaSceneManager());

// リリースビルドでログを全て抑制
UrsaCore.Initialize(new UrsaSceneManager(logger: new NullUrsaLogger()));

// 独自のログシステムに流す
public class MyLogger : IUrsaLogger
{
    public void Log(string message) => MyLogSystem.Info(message);
    public void LogWarning(string message) => MyLogSystem.Warn(message);
}

UrsaCore.Initialize(new UrsaSceneManager(logger: new MyLogger()));
```

> **Note:** `NullUrsaLogger` はフレームワーク同梱の空実装です。Addressables と組み合わせる場合は両方指定できます。
> ```csharp
> UrsaCore.Initialize(new UrsaSceneManager(new MyAddressablesSceneLoader(), new NullUrsaLogger()));
> ```

---

## override 可能なメソッド一覧

| メソッド | 修飾子 | 呼ばれるタイミング |
|---|---|---|
| `OnInitializeAsync(T)` | `protected virtual` | シーン入場時（パラメーター注入後） |
| `OnResumeScene()` | `public virtual` | 前面シーンが閉じて自分が最前面に戻った時 |
| `OnBackKeyPressed()` | `protected virtual` | バックキー（Escape）押下時 |
| `OnDestroy()` | `protected virtual` | GameObjectが破棄される時（Unity） |

> `ReplaceAsync` / `CloseAsync` はコマンドメソッドのため override 不可です。

---

## シーン全体の表示・非表示

`SetSceneActive(bool)` でシーン内の全 GameObject をまとめて切り替えられます。
Push で上に重ねた下のシーンの描画コストを省きたい場合などに使います。

```csharp
// 上にシーンを重ねるタイミングで自分を隠す
await UrsaCore.Scene.PushAsync<NextScene>(new NextScene.Parameter());
SetSceneActive(false);

// 前面シーンが閉じて戻ってきたら再表示
public override void OnResumeScene()
{
    base.OnResumeScene();
    SetSceneActive(true);
}
```

> 非アクティブにしても `OnResumeScene()` は正しく呼ばれます。
