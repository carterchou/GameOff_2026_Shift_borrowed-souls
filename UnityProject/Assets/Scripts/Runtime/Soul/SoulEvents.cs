using BorrowedSouls.Vessels;
using UnityEngine;

namespace BorrowedSouls.Soul
{
    /// <summary>
    public struct SoulExited
    {
        public VesselRuntime PreviousVessel;
        public bool Forced;
        public bool EnemySoulResumed;
        public Vector3 Position;
    }

    public struct PossessionSucceeded
    {
        public VesselRuntime From;
        public VesselRuntime To;
        public bool FirstClaim;
        public bool SuppressedEnemySoul;
        public float Healed;
    }

    public struct SoulExpired
    {
        public Vector3 Position;
    }

    /// <summary>One ember collected. Combo drives the rising pickup pitch; Banked &gt; 0 when an upgrade was stored.</summary>
    public struct EmberCollected
    {
        public int Combo;
        public int Banked;
        public int TotalBanked;
        public float Fill01;
        public float SoulTimeBonus;
        public Vector3 Position;
    }
}
