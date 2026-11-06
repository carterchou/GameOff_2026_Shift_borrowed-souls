namespace BorrowedSouls.Core.Combat
{
    /// <summary>
    /// Dodge timing: total duration, invulnerability window inside it, and a cooldown measured from dodge start.
    ///
    /// </summary>
    public sealed class DodgeState
    {
        private float _duration;
        private float _invulnStart;
        private float _invulnEnd;
        private float _cooldown;
        private float _elapsed = -1f;
        private float _cooldownLeft;

        public void Configure(float duration, float invulnStart, float invulnEnd, float cooldown)
        {
            _duration = duration;
            _invulnStart = invulnStart;
            _invulnEnd = invulnEnd;
            _cooldown = cooldown;
        }

        public bool IsDodging => _elapsed >= 0f;
        public bool IsInvulnerable => IsDodging && _elapsed >= _invulnStart && _elapsed <= _invulnEnd;
        public bool IsReady => !IsDodging && _cooldownLeft <= 0f;
        public float Progress => IsDodging && _duration > 0f ? _elapsed / _duration : 0f;
        public float CooldownLeft => _cooldownLeft;

        public bool TryStart()
        {
            if (!IsReady) return false;
            _elapsed = 0f;
            _cooldownLeft = _cooldown;
            return true;
        }

        /// <summary>Returns true on the tick the dodge ends.</summary>
        public bool Tick(float dt)
        {
            if (dt <= 0f) return false;
            if (_cooldownLeft > 0f) _cooldownLeft -= dt;
            if (!IsDodging) return false;

            _elapsed += dt;
            if (_elapsed >= _duration)
            {
                _elapsed = -1f;
                return true;
            }
            return false;
        }

        public void Interrupt()
        {
            _elapsed = -1f;
        }
    }
}
