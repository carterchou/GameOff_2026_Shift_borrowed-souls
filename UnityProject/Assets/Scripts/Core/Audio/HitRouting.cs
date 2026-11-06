namespace BorrowedSouls.Core.Audio
{
    /// <summary>Which mix a confirmed hit belongs to.</summary>
    public enum HitRoute
    {
        /// <summary>The player's body hit someone: bright and crisp.</summary>
        PlayerDealt,
        /// <summary>The player's body got hit: the loudest, highest-priority hit sound.</summary>
        PlayerHurt,
        /// <summary>Enemies hitting each other or vacated bodies: quiet and distant.</summary>
        Bystander
    }

    public static class HitRouting
    {
        /// <summary>Getting hurt wins over dealing damage, so the player always hears the danger.</summary>
        public static HitRoute Classify(bool attackerIsPlayer, bool targetIsPlayer)
        {
            if (targetIsPlayer) return HitRoute.PlayerHurt;
            return attackerIsPlayer ? HitRoute.PlayerDealt : HitRoute.Bystander;
        }
    }
}
