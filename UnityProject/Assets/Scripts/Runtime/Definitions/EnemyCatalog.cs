using System;
using BorrowedSouls.Core.Combat;
using UnityEngine;

namespace BorrowedSouls.Data
{
    /// <summary>Maps a class to its vessel prefab and AI profile, plus the elite template.</summary>
    [CreateAssetMenu(menuName = "Borrowed Souls/Enemy Catalog", fileName = "EnemyCatalog")]
    public sealed class EnemyCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public VesselClass Class;
            public GameObject Prefab;
            public AiProfile Ai;
        }

        [SerializeField] private Entry[] _entries = new Entry[0];

        [Header("Boss")]
        [SerializeField] private GameObject _bossPrefab;

        [Header("Elite template")]
        [SerializeField, Min(1f)] private float _eliteHpMultiplier = 1.5f;
        [SerializeField, Min(1f)] private float _eliteAttackMultiplier = 1.15f;
        [SerializeField, Min(1f)] private float _eliteScale = 1.2f;

        public GameObject BossPrefab => _bossPrefab;
        public float EliteHpMultiplier => _eliteHpMultiplier;
        public float EliteAttackMultiplier => _eliteAttackMultiplier;
        public float EliteScale => _eliteScale;

        public bool TryGet(VesselClass cls, out Entry entry)
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].Class != cls) continue;
                entry = _entries[i];
                return true;
            }
            entry = default;
            return false;
        }
    }
}
