using BorrowedSouls.Core.Combat;
using BorrowedSouls.Data;
using BorrowedSouls.Vessels;
using UnityEngine;

namespace BorrowedSouls.Combat
{
    /// <summary>Published only when damage was actually applied.</summary>
    public struct HitConfirmed
    {
        public VesselRuntime Attacker;
        public Team AttackerTeam;
        public VesselRuntime Target;
        public AttackDefinition Attack;
        public float Damage;
        public Vector3 Point;
        public Vector3 Direction;
        public HitTier Tier;
        public bool IsKill;
        public bool Interrupted;
    }

    /// <summary>A projectile stopped flying: it struck a body or a wall, or ran out of range.</summary>
    public struct ProjectileEnded
    {
        public AttackDefinition Attack;
        public Vector3 Point;
        public Vector3 Direction;
        public bool Struck;
        public bool HitVessel;
    }

    /// <summary>A wave's end restored HP to the player's body. Embers heal too, but too little and too often to announce.</summary>
    public struct BodyHealed
    {
        public VesselRuntime Vessel;
        public float Amount;
    }

    /// <summary>An enemy broke a lying corpse so the player cannot borrow it.</summary>
    public struct CorpseSmashed
    {
        public VesselRuntime Corpse;
        public VesselRuntime By;
    }

    /// <summary>A hit landed during the target's invulnerability window.</summary>
    public struct HitEvaded
    {
        public VesselRuntime Attacker;
        public VesselRuntime Target;
        public Vector3 Point;
    }

    public struct AttackStarted
    {
        public VesselRuntime Attacker;
        public AttackDefinition Attack;
    }

    /// <summary>An attack's start-up ended: this is the moment it lands or leaves the attacker.</summary>
    public struct AttackReleased
    {
        public VesselRuntime Attacker;
        public AttackDefinition Attack;
    }

    public struct DodgeStarted
    {
        public VesselRuntime Vessel;
        public Vector3 Direction;
    }

    public struct VesselDied
    {
        public VesselRuntime Vessel;
        public VesselRuntime Killer;
        public Vector3 LastHitDirection;
    }
}
