namespace BorrowedSouls.Core.Combat
{
    /// <summary>
    /// Weak-attack chain (e.g. longsword 1→2→3). Resets to the first step when the player idles longer than
    /// the reset window after an attack finishes, when the chain completes, or on dodge.
    ///
    /// </summary>
    public sealed class ComboCounter
    {
        private readonly int _length;
        private readonly float _resetWindow;
        private int _next;
        private float _idle;
        private bool _counting;

        public ComboCounter(int length, float resetWindow)
        {
            _length = length < 1 ? 1 : length;
            _resetWindow = resetWindow;
        }

        public int NextIndex => _next;

        /// <summary>Returns the step index to perform now and advances the chain.</summary>
        public int Consume()
        {
            if (_counting && _idle > _resetWindow) _next = 0;
            int index = _next;
            _next = (_next + 1) % _length;
            _counting = false;
            _idle = 0f;
            return index;
        }

        /// <summary>Call when the attack's recovery ends; starts the idle timer.</summary>
        public void MarkAttackFinished()
        {
            _counting = true;
            _idle = 0f;
        }

        public void Tick(float dt)
        {
            if (!_counting) return;
            _idle += dt;
            if (_idle > _resetWindow) _next = 0;
        }

        public void Reset()
        {
            _next = 0;
            _idle = 0f;
            _counting = false;
        }
    }
}
