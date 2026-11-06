using System.Collections.Generic;

namespace BorrowedSouls.Core.Meta
{
    public enum TalentId
    {
        ThickShell,
        Respite,
        SoulLeeway,
        FirstBlessing,
        GatherEmbers
    }

    /// <summary>One talent's rules: what each level costs, and how much each level gives.</summary>
    public readonly struct TalentRule
    {
        public TalentRule(TalentId id, int[] costs, float perLevel)
        {
            Id = id;
            Costs = costs;
            PerLevel = perLevel;
        }

        public TalentId Id { get; }
        public int[] Costs { get; }
        public float PerLevel { get; }
        public int MaxLevel => Costs.Length;
    }

    /// <summary>What the talents add to a run.</summary>
    public struct RunBonuses
    {
        public float MaxHpShare;
        public float WaveHealShare;
        public float SoulSeconds;
        public int BankedUpgrades;
        public float EmberMagnetShare;

        public static RunBonuses From(ProfileState profile, IReadOnlyList<TalentRule> rules)
        {
            var bonuses = new RunBonuses();
            foreach (TalentRule rule in rules)
            {
                float amount = profile.Level(rule.Id) * rule.PerLevel;
                switch (rule.Id)
                {
                    case TalentId.ThickShell: bonuses.MaxHpShare = amount; break;
                    case TalentId.Respite: bonuses.WaveHealShare = amount; break;
                    case TalentId.SoulLeeway: bonuses.SoulSeconds = amount; break;
                    case TalentId.FirstBlessing: bonuses.BankedUpgrades = (int)amount; break;
                    case TalentId.GatherEmbers: bonuses.EmberMagnetShare = amount; break;
                }
            }
            return bonuses;
        }
    }

    /// <summary>The bodies waiting on the bench, any of which a run can start in.</summary>
    public enum StartingBody
    {
        Swordsman,
        Archer,
        Mage
    }

    /// <summary>
    /// What survives between runs: soul crystals, bought talents and the body last chosen, nothing else. Every run pays something, so even a short one moves
    /// the player forward.
    /// </summary>
    public sealed class ProfileState
    {
        private readonly Dictionary<TalentId, int> _levels = new Dictionary<TalentId, int>();

        public int Crystals { get; private set; }

        public StartingBody Body { get; private set; }

        public void ChooseBody(StartingBody body) => Body = body;

        public int Level(TalentId id) => _levels.TryGetValue(id, out int level) ? level : 0;

        public void AddCrystals(int amount)
        {
            if (amount > 0) Crystals += amount;
        }

        /// <summary>What the next level costs, or -1 once the talent is maxed.</summary>
        public int NextCost(in TalentRule rule)
        {
            int level = Level(rule.Id);
            return level < rule.MaxLevel ? rule.Costs[level] : -1;
        }

        /// <summary>Spends crystals on the next level. Returns false, changing nothing, if it is maxed or too dear.</summary>
        public bool TryBuy(in TalentRule rule)
        {
            int cost = NextCost(rule);
            if (cost < 0 || Crystals < cost) return false;
            Crystals -= cost;
            _levels[rule.Id] = Level(rule.Id) + 1;
            return true;
        }

        /// <summary>Puts back saved values, ignoring impossible ones, so an edited or damaged save can't break a run.</summary>
        public void Restore(int crystals, TalentId id, int level, IReadOnlyList<TalentRule> rules)
        {
            Crystals = crystals < 0 ? 0 : crystals;
            foreach (TalentRule rule in rules)
            {
                if (rule.Id != id) continue;
                _levels[id] = level < 0 ? 0 : (level > rule.MaxLevel ? rule.MaxLevel : level);
            }
        }
    }

    public static class CrystalReward
    {
        public static int For(int wavesCleared, int kills, bool victory, int perWave, int perKill, int victoryBonus)
        {
            int reward = (wavesCleared < 0 ? 0 : wavesCleared) * perWave + (kills < 0 ? 0 : kills) * perKill;
            return victory ? reward + victoryBonus : reward;
        }
    }
}
