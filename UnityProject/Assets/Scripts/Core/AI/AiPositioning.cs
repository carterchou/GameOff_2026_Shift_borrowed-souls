namespace BorrowedSouls.Core.AI
{
    /// <summary>Where an enemy body wants to move relative to the player's body this decision tick.</summary>
    public enum AiMove
    {
        Hold,
        Approach,
        Retreat,
        Strafe
    }

    /// <summary>
    /// Movement choice for the shared class AI.
    /// Pure so the arena stalemate (enemies idling beyond sense range while the player sits at the wall,
    /// HANDOFF §4) is covered by EditMode tests.
    /// </summary>
    public static class AiPositioning
    {
        public static AiMove Choose(float distance, float senseRange, float preferredMax, float retreatDistance, float secondsOutOfSense, float outOfSenseHold)
        {
            if (distance > senseRange) return OutOfSense(secondsOutOfSense, outOfSenseHold);
            if (distance > preferredMax) return AiMove.Approach;
            if (retreatDistance > 0f && distance < retreatDistance) return AiMove.Retreat;
            return AiMove.Strafe;
        }

        /// <summary>
        /// The player's body is beyond this enemy's sense range. In the closed wave arena an enemy that holds here
        /// forever stalls the wave (the wave only ends when every enemy dies), so the answer must turn into
        /// Approach at some point. Decision 2026-11-03: hold for <paramref name="outOfSenseHold"/> seconds (the
        /// tunable grace time from AiProfile, 2 s default, 0 = never wait) so a player who just swapped bodies at
        /// the edge gets a breath, then close in.
        /// </summary>
        private static AiMove OutOfSense(float secondsOutOfSense, float outOfSenseHold)
        {
            return secondsOutOfSense >= outOfSenseHold ? AiMove.Approach : AiMove.Hold;
        }
    }
}
