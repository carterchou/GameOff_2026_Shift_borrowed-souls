using System;
using BorrowedSouls.Core.Meta;
using UnityEngine;

namespace BorrowedSouls.Data
{
    /// <summary>The talents on sale in the rest room and what a run pays.</summary>
    [CreateAssetMenu(menuName = "Borrowed Souls/Talent Catalog", fileName = "TalentCatalog")]
    public sealed class TalentCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public TalentId Id;
            public string DisplayName;
            [Tooltip("What one level gives, shown to the player.")]
            public string Effect;
            public int[] Costs;
            [Tooltip("The value one level adds: a share of 1 for percentages, seconds for soul time, a count for upgrades.")]
            public float PerLevel;
        }

        [Tooltip("When off, the talents, the crystals and what they pay stay out of the game, and bought levels do nothing.")]
        [SerializeField] private bool _released;
        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();
        [SerializeField, Min(0)] private int _crystalsPerWave = 5;
        [SerializeField, Min(0)] private int _crystalsPerKill = 1;
        [SerializeField, Min(0)] private int _victoryCrystals = 30;

        private TalentRule[] _rules;

        /// <summary>Lets a test see the game with talents on or off, without writing to the asset. Null follows the asset.</summary>
        public static bool? ReleasedForTests { get; set; }

        public bool Released => ReleasedForTests ?? _released;

        public Entry[] Entries => _entries;

        public TalentRule[] Rules
        {
            get
            {
                if (_rules == null || _rules.Length != _entries.Length)
                {
                    _rules = new TalentRule[_entries.Length];
                    for (int i = 0; i < _entries.Length; i++) _rules[i] = new TalentRule(_entries[i].Id, _entries[i].Costs, _entries[i].PerLevel);
                }
                return _rules;
            }
        }

        public int RewardFor(int wavesCleared, int kills, bool victory) =>
            CrystalReward.For(wavesCleared, kills, victory, _crystalsPerWave, _crystalsPerKill, _victoryCrystals);
    }
}
