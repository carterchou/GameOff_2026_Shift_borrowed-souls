namespace BorrowedSouls.Core.Audio
{
    /// <summary>
    /// Picks which of a cue's clip variants to play next, never the same one twice in a row
    ///.
    /// </summary>
    public sealed class VariantCycler
    {
        private int _last = -1;

        public int Next(int count, System.Random rng)
        {
            if (count <= 1) return _last = 0;
            if (_last >= count) _last = -1;
            int i = rng.Next(_last < 0 ? count : count - 1);
            if (_last >= 0 && i >= _last) i++;
            return _last = i;
        }
    }
}
