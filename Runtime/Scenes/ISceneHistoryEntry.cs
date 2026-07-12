using System;

namespace Ursa
{
    /// <summary>
    /// 履歴スタック上の1エントリを表すインターフェース
    /// </summary>
    public interface ISceneHistoryEntry
    {
        /// <summary>履歴内のインデックス（0が最も古い）</summary>
        int Index { get; }

        /// <summary>シーン名</summary>
        string SceneName { get; }

        /// <summary>シーンの型</summary>
        Type SceneType { get; }

        /// <summary>表示方法</summary>
        UrsaScenePresentation Presentation { get; }
    }
}
