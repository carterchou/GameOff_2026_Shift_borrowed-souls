namespace BorrowedSouls.Core.Audio
{
    /// <summary>
    /// The player's chained hits for the rising hit pitch:
    /// each hit within <c>window</c> seconds of the previous one climbs one step, capped at <c>maxStep</c>; a gap resets to 0.
    /// </summary>
    public sealed class HitCombo
    {
        private readonly float _window;
        private readonly int _maxStep;
        private float _lastTime = float.NegativeInfinity;
        private int _step = -1;

        public HitCombo(float window, int maxStep)
        {
            _window = window;
            _maxStep = maxStep;
        }

        /// <summary>Records a hit at <paramref name="now"/> (seconds) and returns its step, 0..maxStep.</summary>
        public int Register(float now)
        {
            _step = now - _lastTime <= _window ? System.Math.Min(_step + 1, _maxStep) : 0;
            _lastTime = now;
            return _step;
        }
    }
}
