using BorrowedSouls.Vessels;

namespace BorrowedSouls.Combat
{
    /// <summary>Boss asks the encounter director for possessable adds.</summary>
    public struct BossSummonRequested
    {
        public VesselRuntime Boss;
        public int Count;
    }

    /// <summary>Boss has begun charging a named attack, so the player can be told what is coming.</summary>
    public struct BossAttackAnnounced
    {
        public VesselRuntime Boss;
        public Data.AttackDefinition Attack;
    }

    /// <summary>Boss crossed an HP threshold.</summary>
    public struct BossPhaseChanged
    {
        public VesselRuntime Boss;
        public int Phase;
    }
}
