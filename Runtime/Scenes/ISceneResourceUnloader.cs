namespace Ursa
{
    /// <summary>
    /// パラメーターに実装することで、シーン破棄時にアセットの解放を自動実行するインターフェース。
    /// SceneBaseの OnDestroy() 内で自動的に呼び出されます。
    /// </summary>
    public interface ISceneResourceUnloader
    {
        void UnloadResources();
    }
}
