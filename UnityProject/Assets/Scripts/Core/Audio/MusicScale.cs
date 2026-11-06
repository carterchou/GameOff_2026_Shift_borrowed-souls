namespace BorrowedSouls.Core.Audio
{
    /// <summary>
    /// Keeps melodic cues in the BGM key:
    /// ember chains climb the minor pentatonic instead of raw semitones, so they never clash with the music.
    /// </summary>
    public static class MusicScale
    {
        private static readonly int[] MinorPentatonic = { 0, 3, 5, 7, 10 };

        /// <summary>Semitones above the root of the n-th minor-pentatonic note (0 = root), wrapping up by octaves.</summary>
        public static int PentatonicSemitones(int step)
        {
            if (step < 0) step = 0;
            int length = MinorPentatonic.Length;
            return step / length * 12 + MinorPentatonic[step % length];
        }

        /// <summary>AudioSource pitch multiplier for a shift of <paramref name="semitones"/>.</summary>
        public static float SemitonesToPitch(float semitones) => (float)System.Math.Pow(2.0, semitones / 12.0);
    }
}
