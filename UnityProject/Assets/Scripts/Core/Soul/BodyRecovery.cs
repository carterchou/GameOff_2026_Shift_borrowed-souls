namespace BorrowedSouls.Core.Soul
{
    /// <summary>
    /// The small heals that keep a new player alive between borrows. They only reach the body the player is in:
    /// a body left on the floor stays as it was, so walking out and back never becomes free healing.
    /// </summary>
    public static class BodyRecovery
    {
        /// <summary>A wave ended. Returns the HP actually restored.</summary>
        public static float OnWaveCleared(VesselState body, float shareOfMaxHp)
        {
            return body == null ? 0f : HealIfHeld(body, body.MaxHp * shareOfMaxHp);
        }

        /// <summary>An ember reached the player while in a body. Returns the HP actually restored.</summary>
        public static float OnEmberCollected(VesselState body, float hp)
        {
            return body == null ? 0f : HealIfHeld(body, hp);
        }

        /// <summary>Whether to remind the player that borrowing a body is how to recover.</summary>
        public static bool ShouldHintBorrow(float hpShare, float threshold, bool legalBodyInReach, float cooldownLeft)
        {
            return hpShare > 0f && hpShare <= threshold && legalBodyInReach && cooldownLeft <= 0f;
        }

        private static float HealIfHeld(VesselState body, float amount)
        {
            if (body.Owner != VesselOwner.Player) return 0f;
            float before = body.Hp;
            body.Heal(amount);
            return body.Hp - before;
        }
    }
}
