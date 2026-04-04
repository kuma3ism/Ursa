# Ursa

Unityの俺俺フレームワーク

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
await UrsaCore.Scene.PushAsync<NextScene>(param);                         // Fade（デフォルト）
await UrsaCore.Scene.PushAsync<NextScene>(param, TransitionType.Dissolve); // Dissolve
await UrsaCore.Scene.PushAsync<NextScene>(param, null);                    // トランジションなし
```

### 組み込みトランジション名

`TransitionType` は文字列定数クラスです。`UrsaSettings` に登録された名前と対応します。

| 定数 | 文字列値 | 演出 |
|---|---|---|
| `TransitionType.Fade` | `"Fade"` | 画面全体がじわっと黒くなる（デフォルト） |
| `TransitionType.Wipe` | `"Wipe"` | 左から右に黒が流れる |
| `TransitionType.Circle` | `"Circle"` | 中心から黒い円が広がる |
| `TransitionType.Dissolve` | `"Dissolve"` | ランダムにパラパラ黒くなる |
| `TransitionType.Animator` | `"Animator"` | Animator で制御するカスタム演出 |

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
