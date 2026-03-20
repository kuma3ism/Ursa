using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ursa
{
    public interface ISceneParameter 
    {
        // デフォルトは Single。必要に応じて Additive を返すように override する
        LoadSceneMode Mode => LoadSceneMode.Single;
    }

    public static class UrsaCore
    {
        public static ISceneManager Scene { get; set; }
        public static bool IsReady => Scene != null;
        public static void Dispose() => Scene = null;
    }

    public interface ISceneManager
    {
        Task TransitionAsync<TScene>(ISceneParameter parameter) where TScene : MonoBehaviour;
        Task PushAsync<TScene>(ISceneParameter parameter) where TScene : MonoBehaviour;
        Task PopAsync();
        
        // 追加：現在の最前面シーンを破棄して、新しいシーンをロードする
        Task ReplaceAsync<TScene>(ISceneParameter parameter) where TScene : MonoBehaviour;
        // 指定したキーが最前面なら true を返す
        bool IsActive(UnityEngine.SceneManagement.Scene scene);

        // --- 新規追加: インスタンスベース機能 ---
        Task<TScene> CreateSceneAsync<TScene>() where TScene : MonoBehaviour;
        Task PushInstanceAsync(UnityEngine.SceneManagement.Scene scene);
        Task ReplaceInstanceAsync(UnityEngine.SceneManagement.Scene scene);
        
        Task<TResult> OpenResultAsync<TScene, TParam, TResult>(TParam parameter) 
            where TScene : Ursa.Scenes.SceneBase<TParam, TResult> 
            where TParam : ISceneParameter;
    }

    public interface ISceneReceiver<T> where T : ISceneParameter 
    { 
        System.Threading.Tasks.Task OnEnterScene(T parameter); 
    }
}