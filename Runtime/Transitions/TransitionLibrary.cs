using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ursa.Transitions
{
    /// <summary>
    /// 遷移演出のプレハブを名前で管理するライブラリ。
    /// Inspector から登録し、エディタ拡張によって TransitionType Enum を自動生成します。
    /// </summary>
    [CreateAssetMenu(fileName = "UrsaTransitionLibrary", menuName = "Ursa/Transition Library")]
    public class TransitionLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string Name;
            public TransitionController Prefab;
        }

        [SerializeField]
        private List<Entry> _entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => _entries;

        /// <summary>
        /// 指定した TransitionType に対応するプレハブを返します。
        /// </summary>
        public TransitionController GetPrefab(TransitionType type)
        {
            if (type == TransitionType.Default) return null;

            string typeName = type.ToString();
            foreach (var entry in _entries)
            {
                if (entry.Name == typeName) return entry.Prefab;
            }
            return null;
        }
    }
}
