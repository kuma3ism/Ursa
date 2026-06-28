using System.Threading.Tasks;
using UnityEngine;

namespace Ursa
{
    /// <summary>
    /// Task の fire-and-forget 拡張。
    /// UniTask を導入していない環境でも UniTask と同じ書き心地で使えます。
    /// UniTask を導入している場合も Task 型には UniTask の Forget() は生えないため競合しません。
    ///
    /// 例外は握りつぶさず Debug.LogException でコンソールに出力します。
    /// </summary>
    public static class TaskExtensions
    {
        /// <summary>Task を明示的に捨てます。例外は Debug.LogException で出力されます。</summary>
        public static void Forget(this Task task)
        {
            task.ContinueWith(
                t => Debug.LogException(t.Exception?.InnerException),
                TaskContinuationOptions.OnlyOnFaulted);
        }

        /// <summary>Task&lt;T&gt; を明示的に捨てます。例外は Debug.LogException で出力されます。</summary>
        public static void Forget<T>(this Task<T> task)
        {
            task.ContinueWith(
                t => Debug.LogException(t.Exception?.InnerException),
                TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
