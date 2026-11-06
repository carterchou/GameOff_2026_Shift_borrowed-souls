namespace BorrowedSouls.Core.Combat
{
    public enum VesselClass
    {
        Melee,
        Ranged,
        Mage
    }

    public enum Team
    {
        Player,
        Enemy,
        Neutral
    }

    public enum HitTier
    {
        Weak,
        Strong,
        Special
    }

    /// <summary>
    /// Class triangle and damage formula.
    /// Melee beats Mage, Mage beats Ranged, Ranged beats Melee.
    /// </summary>
    public static class CombatRules
    {
        public static bool Counters(VesselClass attacker, VesselClass defender)
        {
            switch (attacker)
            {
                case VesselClass.Melee: return defender == VesselClass.Mage;
                case VesselClass.Mage: return defender == VesselClass.Ranged;
                case VesselClass.Ranged: return defender == VesselClass.Melee;
                default: return false;
            }
        }

        public static float CounterMultiplier(VesselClass attacker, VesselClass defender, float counterBonus)
        {
            return Counters(attacker, defender) ? counterBonus : 1f;
        }

        /// <summary>Damage = Base × AttackMultiplier × CounterMultiplier × BuffMultiplier, never negative.</summary>
        public static float ComputeDamage(float baseDamage, float attackMultiplier, float counterMultiplier, float buffMultiplier)
        {
            float d = baseDamage * attackMultiplier * counterMultiplier * buffMultiplier;
            return d < 0f ? 0f : d;
        }

        public static bool IsHostile(Team a, Team b)
        {
            if (a == Team.Neutral || b == Team.Neutral) return false;
            return a != b;
        }
    }
}
