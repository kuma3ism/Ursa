using System.Threading.Tasks;
using UnityEngine;

namespace Ursa
{
    /// <summary>
    /// ダイアログ Prefab のロード方法を抽象化するインターフェース。
    /// ISceneLoader と同じ役割。
    /// デフォルト実装は ResourcesDialogLoader です。
    /// </summary>
    public interface IDialogLoader
    {
        /// <summary>指定した名前のダイアログ Prefab をロードして返します。</summary>
        Task<GameObject> LoadAsync(string dialogName);

        /// <summary>ロード済みリソースを解放します（Addressables 等の後始末用）。</summary>
        void Unload(string dialogName);
    }

    /// <summary>
    /// Resources.Load を使ったデフォルトの IDialogLoader 実装。
    /// Prefab は Resources/{basePath}/{DialogName} に配置してください。
    /// </summary>
    public class ResourcesDialogLoader : IDialogLoader
    {
        private readonly string _basePath;

        /// <param name="basePath">Resources 以下のサブパス（デフォルト: "Dialogs"）。空文字なら Resources 直下。</param>
        public ResourcesDialogLoader(string basePath = "Dialogs")
        {
            _basePath = basePath;
        }

        public Task<GameObject> LoadAsync(string dialogName)
        {
            var path = string.IsNullOrEmpty(_basePath)
                ? dialogName
                : $"{_basePath}/{dialogName}";

            var prefab = Resources.Load<GameObject>(path);
            if (prefab == null)
                throw new System.InvalidOperationException(
                    $"[Ursa] Dialog prefab not found at Resources/{path}");

            return Task.FromResult(prefab);
        }

        /// <summary>Resources はエンジン管理のため明示的なアンロードは行いません。</summary>
        public void Unload(string dialogName) { }
    }
}
