namespace Ursa.UI.Blocking
{
    public interface IUIBlocker
    {
        bool IsBlocked { get; }
        void Enter();
        void Exit();
    }
}
