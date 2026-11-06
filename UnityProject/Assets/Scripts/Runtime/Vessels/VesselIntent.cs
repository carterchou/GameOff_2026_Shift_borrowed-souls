using UnityEngine;

namespace BorrowedSouls.Vessels
{
    /// <summary>
    /// What a driver (player input or AI) wants the body to do this frame. The vessel never knows who
    /// produced it — possession only swaps the driver.
    /// </summary>
    public struct VesselIntent
    {
        /// <summary>World-space planar move direction (x, z), magnitude 0..1.</summary>
        public Vector2 Move;
        /// <summary>World-space planar aim direction, normalized or zero.</summary>
        public Vector3 Aim;
        public bool WeakPressed;
        public bool WeakHeld;
        public bool StrongPressed;
        public bool SpecialPressed;
        public bool DodgePressed;
        public bool SoulShiftHeld;
        /// <summary>AI wind-up before an attack (readable telegraph); presentation only.</summary>
        public bool Telegraphing;
        /// <summary>The attack being wound up (drives ground telegraph shapes); presentation only.</summary>
        public Data.AttackDefinition TelegraphAttack;
    }

    public interface IVesselDriver
    {
        void BuildIntent(VesselRuntime vessel, ref VesselIntent intent);
    }
}
