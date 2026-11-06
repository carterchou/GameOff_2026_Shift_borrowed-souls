namespace BorrowedSouls.Core.Audio
{
    /// <summary>Which music plays.</summary>
    public enum MusicState
    {
        /// <summary>Title screen, before the first wave: the menu track.</summary>
        Menu,
        /// <summary>Arena combat track; how many of its stacked layers sound comes from <see cref="MusicIntensity.Layers"/>.</summary>
        Arena,
        /// <summary>The boss wave, from its first moment: the boss track replaces the arena layers.</summary>
        Boss,
        /// <summary>The run is won: the boss track dies away under the victory sting.</summary>
        Victory
    }

    /// <summary>Snapshot of the run the music reacts to; filled by the AudioDirector each frame.</summary>
    public struct MusicContext
    {
        public bool RunStarted;
        /// <summary>Upgrade choice, pause menu or results: rhythm drops out, only the ambience stays.</summary>
        public bool Paused;
        public bool BossFight;
        public bool Won;
        /// <summary>Enemies alive or still queued for this wave (0 between waves).</summary>
        public int RemainingEnemies;
        /// <summary>The player's current body HP fraction; 1 while flying as a soul (no body).</summary>
        public float PlayerHp01;
    }

    /// <summary>
    /// Adaptive layering for the arena track: layer 0 is ambience, 1 adds bass and kick, 2 adds the top percussion.
    /// Pure so the mapping is covered by EditMode tests; thresholds come from AudioMixSettings.
    /// </summary>
    public static class MusicIntensity
    {
        public static MusicState State(in MusicContext c)
        {
            if (!c.RunStarted) return MusicState.Menu;
            if (c.Won) return MusicState.Victory;
            return c.BossFight ? MusicState.Boss : MusicState.Arena;
        }

        /// <summary>How many arena layers sound (1..<paramref name="maxLayers"/>).</summary>
        public static int Layers(in MusicContext c, int maxLayers, int busyEnemies, float lowHp01)
        {
            if (maxLayers <= 1) return 1;
            if (c.Paused || c.RemainingEnemies == 0) return 1; // breathing room between waves / while choosing
            bool pressure = c.RemainingEnemies >= busyEnemies || c.PlayerHp01 <= lowHp01;
            return pressure ? maxLayers : maxLayers - 1;
        }
    }
}
