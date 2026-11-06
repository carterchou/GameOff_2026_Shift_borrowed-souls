using System;
using BorrowedSouls.Core.Soul;
using UnityEngine;

namespace BorrowedSouls.Data
{
    /// <summary>Player-facing upgrade text.</summary>
    [CreateAssetMenu(menuName = "Borrowed Souls/Upgrade Catalog", fileName = "UpgradeCatalog")]
    public sealed class UpgradeCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public UpgradeId Id;
            public string DisplayName;
            [Tooltip("Index n = text for reaching level n+1.")]
            [TextArea] public string[] NextLevelText;
        }

        [SerializeField] private Entry[] _entries = new Entry[0];

        public bool TryGet(UpgradeId id, out Entry entry)
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].Id != id) continue;
                entry = _entries[i];
                return true;
            }
            entry = default;
            return false;
        }

        public string Describe(UpgradeId id, int currentLevel)
        {
            if (!TryGet(id, out Entry e) || e.NextLevelText == null || e.NextLevelText.Length == 0) return id.ToString();
            int i = Mathf.Clamp(currentLevel, 0, e.NextLevelText.Length - 1);
            return e.NextLevelText[i];
        }

        public string NameOf(UpgradeId id) => TryGet(id, out Entry e) && !string.IsNullOrEmpty(e.DisplayName) ? e.DisplayName : id.ToString();
    }
}
