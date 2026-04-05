using System;
using System.Threading.Tasks;

namespace Ursa.UI.Button
{
    public sealed class DelegateButtonHandler : UrsaButtonHandler
    {
        private readonly Func<Task> _action;

        public DelegateButtonHandler(Func<Task> action)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        protected override Task ExecuteCore() => _action();
    }
}
