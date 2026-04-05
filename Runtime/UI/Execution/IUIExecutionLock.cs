using System;

namespace Ursa.UI.Execution
{
    public interface IUIExecutionLock
    {
        bool TryEnter(out IDisposable scope);
    }
}
