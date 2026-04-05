using System;
using System.Threading.Tasks;

namespace Ursa.UI.Button
{
    public interface IUIButton
    {
        void SetOnClickAsync(Func<Task> handler);
    }
}
