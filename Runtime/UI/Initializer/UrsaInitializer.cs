using Ursa.UI.Blocking;
using Ursa.UI.Core;
using Ursa.UI.Execution;

namespace Ursa.UI.Initializer
{
    public static class UrsaInitializer
    {
        private static bool _initialized;

        public static void Initialize(bool force = false)
        {
            if (_initialized && !force) return;

            UrsaCore.Register<IUIExecutionLock>(new UIExecutionLock());
            UrsaCore.Register<IUIBlocker>(new SimpleUIBlocker());
            _initialized = true;
        }
    }
}
