using System.Threading.Tasks;

namespace Ursa.UI.Button
{
    public abstract class UrsaButtonHandler
    {
        public Task ExecuteAsync() => ExecuteCore();

        protected abstract Task ExecuteCore();
    }
}
