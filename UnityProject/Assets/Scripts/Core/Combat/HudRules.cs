using System;
using BorrowedSouls.Core.Soul;

namespace BorrowedSouls.Core.Combat
{
    /// <summary>What the combat HUD shows.</summary>
    public static class HudRules
    {
        private const float TenthsRoundingSlack = 0.001f;

        /// <summary>
        /// In-world bar over a body: only once it is hurt, or while locked on. Never on the player's own body (the HUD
        /// shows that), a dead one, or the boss (which has the large bar at the top of the screen).
        /// </summary>
        public static bool ShowWorldHealthBar(bool isPlayerBody, bool isAlive, VesselKind kind, float hp01, bool isLockTarget)
        {
            if (isPlayerBody || !isAlive || kind == VesselKind.Boss) return false;
            return isLockTarget || hp01 < 1f;
        }

        /// <summary>Fraction of the cooldown still to run, 0 when ready.</summary>
        public static float CooldownRemaining01(float left, float total)
        {
            if (total <= 0f || left <= 0f) return 0f;
            return left >= total ? 1f : left / total;
        }

        /// <summary>Remaining time in tenths of a second, rounded up so 0 means ready and a shown time never reads 0.0.</summary>
        public static int CooldownTenths(float left)
        {
            if (left <= 0f) return 0;
            // The slack keeps float error from rounding a whole tenth up: under Unity's Mono, 0.8f * 10f comes out just
            // above 8, which would show a fresh 0.8 s cooldown as 0.9 s.
            int tenths = (int)Math.Ceiling(left * 10f - TenthsRoundingSlack);
            return tenths < 1 ? 1 : tenths;
        }
    }
}
