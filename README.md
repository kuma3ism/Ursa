# Ursa

Unityの俺俺フレームワーク - シーン管理ライブラリ

## UPM インストール

```
https://github.com/kuma3ism/Ursa.git
```

---

## エディターメニュー（シーンテンプレート自動生成）

`Ursa/Create Scene...` または Project ビュー右クリック → `Assets/Create/Ursa/Create Scene...`

| 項目 | 説明 |
|---|---|
| Feature Name | 機能名（フォルダ名・クラス名になる） |
| Namespace | デフォルトは Root Folder から自動生成（例: `Game` → `Game`） |
| With Result | `SceneBaseWithResult<TParam, TResult>` 版を生成 |
| Register to Build Settings | Build Settings に自動登録 |

**生成されるフォルダ構成：**
```
{FeatureName}/
├── Script/  → {FeatureName}.cs（SceneBase継承）
├── Scene/   → {FeatureName}.unity（GameObjectアタッチ済み）
├── Prefab/  （空、.gitkeep あり）
└── Texture/ （空、.gitkeep あり）
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

---

## シーンの作り方

### 1. パラメーターの定義

シーンに渡すデータを `ISceneParameter` を実装したクラスで定義します。

```csharp
public class MySceneParameter : ISceneParameter
{
    public string Message;
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

---

## インスタンスベースの操作

シーンをロードしてから、タイミングを制御して開くことができます。

```csharp
// ロード（まだ履歴には積まれない）
MyScene scene = await UrsaCore.Scene.CreateSceneAsync<MyScene>();

// Pushして開く
await scene.OpenAsync(new MySceneParameter { Message = "Hello!" });

// Replaceして開く
await scene.ReplaceAsync(new MySceneParameter { Message = "Hello!" });
```

---

## 戻り値を受け取る（ポップアップ待機）

```csharp
// 呼び出し元で:
MyScene popup = await UrsaCore.Scene.CreateSceneAsync<MyScene>(param);
await popup.OpenAsync(param);

MySceneResult result = await popup.WaitForResultAsync(); // 閉じられるまで待機
Debug.Log(result.IsConfirmed);
```

または `OpenResultAsync` でまとめて行うこともできます:

```csharp
var result = await UrsaCore.Scene.OpenResultAsync<MyScene, MySceneParameter, MySceneResult>(param);
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

## override 可能なメソッド一覧

| メソッド | 修飾子 | 呼ばれるタイミング |
|---|---|---|
| `OnInitializeAsync(T)` | `protected virtual` | シーン入場時（パラメーター注入後） |
| `OnResumeScene()` | `public virtual` | 前面シーンが閉じて自分が最前面に戻った時 |
| `OnBackKeyPressed()` | `protected virtual` | バックキー（Escape）押下時 |
| `OnDestroy()` | `protected virtual` | GameObjectが破棄される時（Unity） |

> `OpenAsync` / `ReplaceAsync` / `CloseAsync` はコマンドメソッドのため override 不可です。
