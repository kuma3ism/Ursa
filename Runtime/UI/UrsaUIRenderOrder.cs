namespace Ursa.UI
{
    /// <summary>
    /// Shared sorting order ranges used by Ursa-managed UI.
    /// </summary>
    public static class UrsaUIRenderOrder
    {
        public const int SceneBase = 0;
        public const int SceneStep = 100;

        public const int Dialog = 8000;
        public const int Transition = 9000;
        public const int TapEffect = 9500;
    }
}
