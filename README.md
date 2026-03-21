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
| Namespace | デフォルトは Feature Name と同じ |
| With Result | `SceneBase<TParam, TResult>` 版を生成 |
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

```csharp
public class UrsaInitializer : MonoBehaviour
{
    void Awake()
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
    public override async Task OpenAsync(MySceneParameter parameter)
    {
        await base.OpenAsync(parameter);
        // 初期化処理（this.Parameter はここから使える）
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

public class MyScene : SceneBase<MySceneParameter, MySceneResult>
{
    public override async Task OpenAsync(MySceneParameter parameter)
    {
        await base.OpenAsync(parameter);
    }

    // 結果を返して閉じる（シーン自身から呼ぶ）
    async void OnConfirmButton()
    {
        await CloseAsync(new MySceneResult { IsConfirmed = true });
    }
}
```

> **【重要】** Unityの仕様上、`Awake()` / `Start()` は `OpenAsync` より先に呼ばれます。  
> `this.Parameter` を参照する初期化処理は必ず `OpenAsync()` or `OnEnterScene()` に書いてください。

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

### `OnBackToScene()`

子シーンが閉じられ、自分が再び最前面になったときに呼ばれます。

```csharp
public override void OnBackToScene()
{
    base.OnBackToScene();
    // 再表示時の処理
}
```

### `OnBackKeyPressed()`（Android バックキー対応）

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
    public async Task PreloadResourcesAsync()
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

## カスタムシーンローダー

`ISceneLoader` を実装することで Addressables や AssetBundle に差し替えられます。

```csharp
UrsaCore.Initialize(new UrsaSceneManager(new MyAddressablesSceneLoader()));
```
