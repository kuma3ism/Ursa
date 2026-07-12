using System.Threading.Tasks;

namespace Ursa
{
    /// <summary>
    /// シーン遷移時にパラメーターを受け取り、初期化処理を行うための基底インターフェース。
    /// リフレクション不要で GetComponentsInChildren から直接取得できます。
    /// </summary>
    public interface ISceneReceiver
    {
        Task OnEnterScene(ISceneParameter parameter);
    }

    /// <summary>
    /// シーン遷移時にパラメーターを受け取り、初期化処理を行うためのインターフェース
    /// </summary>
    public interface ISceneReceiver<T> : ISceneReceiver where T : ISceneParameter
    {
        Task OnEnterScene(T parameter);
    }
}
