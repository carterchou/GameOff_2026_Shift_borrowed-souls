using System;
using System.Collections.Generic;

namespace BorrowedSouls.Core.Soul
{
    /// <summary>Soul upgrade pool. Upgrades belong to the soul, never to a vessel.</summary>
    public enum UpgradeId
    {
        Invasion,
        Restoration,
        SoulDuration,
        SoulRange,
        VesselVitality,
        SoulMight,
        SoulHaste,
        SoulBurst
    }

    /// <summary>
    /// Rolls the wave-end 3-choice and applies picks: maxed upgrades are excluded,
    /// fewer than three valid options are shown as-is (never padded), and rolls use the run's seeded RNG so a
    /// balance problem can be reproduced (RUN-06).
    /// </summary>
    public sealed class UpgradeService
    {
        private readonly Dictionary<UpgradeId, int> _maxLevels;
        private readonly List<UpgradeId> _candidates = new List<UpgradeId>(8);

        public UpgradeService(IDictionary<UpgradeId, int> maxLevels)
        {
            _maxLevels = new Dictionary<UpgradeId, int>(maxLevels);
        }

        /// <summary> (all eight P0 upgrades).</summary>
        public static Dictionary<UpgradeId, int> DefaultMaxLevels() => new Dictionary<UpgradeId, int>
        {
            { UpgradeId.Invasion, 4 },
            { UpgradeId.Restoration, 3 },
            { UpgradeId.SoulDuration, 2 },
            { UpgradeId.SoulRange, 3 },
            { UpgradeId.VesselVitality, 3 },
            { UpgradeId.SoulMight, 3 },
            { UpgradeId.SoulHaste, 2 },
            { UpgradeId.SoulBurst, 1 },
        };

        public int MaxLevel(UpgradeId id) => _maxLevels.TryGetValue(id, out int m) ? m : 0;

        public static int GetLevel(PlayerSoul soul, UpgradeId id)
        {
            switch (id)
            {
                case UpgradeId.Invasion: return soul.InvasionLevel;
                case UpgradeId.Restoration: return soul.RestorationLevel;
                case UpgradeId.SoulDuration: return soul.SoulDurationLevel;
                case UpgradeId.SoulRange: return soul.SoulRangeLevel;
                case UpgradeId.VesselVitality: return soul.VitalityLevel;
                case UpgradeId.SoulMight: return soul.MightLevel;
                case UpgradeId.SoulHaste: return soul.HasteLevel;
                case UpgradeId.SoulBurst: return soul.BurstLevel;
                default: return 0;
            }
        }

        public bool IsMaxed(PlayerSoul soul, UpgradeId id) => GetLevel(soul, id) >= MaxLevel(id);

        /// <summary>Fills <paramref name="result"/> with up to <paramref name="count"/> distinct, non-maxed upgrades.</summary>
        public void RollChoices(PlayerSoul soul, int count, Random rng, List<UpgradeId> result)
        {
            result.Clear();
            _candidates.Clear();
            foreach (var pair in _maxLevels)
            {
                if (pair.Value > 0 && GetLevel(soul, pair.Key) < pair.Value) _candidates.Add(pair.Key);
            }
            _candidates.Sort(); // dictionary order is not guaranteed; sort so the same seed gives the same roll

            for (int i = 0; i < count && _candidates.Count > 0; i++)
            {
                int pick = rng.Next(_candidates.Count);
                result.Add(_candidates[pick]);
                _candidates.RemoveAt(pick);
            }
        }

        /// <summary>Raises the upgrade one level. Returns false if it was already maxed.</summary>
        public bool Apply(PlayerSoul soul, UpgradeId id)
        {
            if (IsMaxed(soul, id)) return false;
            switch (id)
            {
                case UpgradeId.Invasion: soul.InvasionLevel++; break;
                case UpgradeId.Restoration: soul.RestorationLevel++; break;
                case UpgradeId.SoulDuration: soul.SoulDurationLevel++; break;
                case UpgradeId.SoulRange: soul.SoulRangeLevel++; break;
                case UpgradeId.VesselVitality: soul.VitalityLevel++; break;
                case UpgradeId.SoulMight: soul.MightLevel++; break;
                case UpgradeId.SoulHaste: soul.HasteLevel++; break;
                case UpgradeId.SoulBurst: soul.BurstLevel++; break;
            }
            return true;
        }
    }
}
