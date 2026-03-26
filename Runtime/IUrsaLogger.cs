using UnityEngine;

namespace Ursa
{
    /// <summary>
    /// Ursa フレームワーク内部のログ出力を抽象化するインターフェース。
    /// UrsaSceneManager のコンストラクタに渡すことで差し替えられます。
    /// </summary>
    public interface IUrsaLogger
    {
        void Log(string message);
        void LogWarning(string message);
    }

    /// <summary>
    /// Unity の Debug.Log / Debug.LogWarning に委譲するデフォルト実装。
    /// </summary>
    public class UnityDebugLogger : IUrsaLogger
    {
        public void Log(string message) => Debug.Log(message);
        public void LogWarning(string message) => Debug.LogWarning(message);
    }

    /// <summary>
    /// ログを一切出力しない実装。リリースビルドで Ursa のログを抑制したい場合に使います。
    /// </summary>
    public class NullUrsaLogger : IUrsaLogger
    {
        public void Log(string message) { }
        public void LogWarning(string message) { }
    }
}
