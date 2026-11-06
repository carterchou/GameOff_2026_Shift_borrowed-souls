namespace BorrowedSouls.Core.Combat
{
    public enum AttackPhase
    {
        None,
        Startup,
        Active,
        Recovery
    }

    /// <summary>Phase transitions produced by one <see cref="AttackTimeline.Tick"/>.</summary>
    [System.Flags]
    public enum AttackTimelineEvents
    {
        None = 0,
        EnteredActive = 1,
        ExitedActive = 2,
        Finished = 4
    }

    /// <summary>
    /// Startup → Active → Recovery → None. Hits may only be resolved while Active.
    /// Cancelling during Startup guarantees the attack never reaches Active (no ghost hits).
    ///
    /// </summary>
    public sealed class AttackTimeline
    {
        private float _startup;
        private float _active;
        private float _recovery;

        public AttackPhase Phase { get; private set; } = AttackPhase.None;
        public float Elapsed { get; private set; }
        public bool IsBusy => Phase != AttackPhase.None;
        public bool HasReachedActive { get; private set; }
        public float Duration => _startup + _active + _recovery;

        /// <summary>0..1 progress through the current phase (used by telegraph presentation).</summary>
        public float PhaseProgress
        {
            get
            {
                switch (Phase)
                {
                    case AttackPhase.Startup: return _startup > 0f ? Elapsed / _startup : 1f;
                    case AttackPhase.Active: return _active > 0f ? (Elapsed - _startup) / _active : 1f;
                    case AttackPhase.Recovery: return _recovery > 0f ? (Elapsed - _startup - _active) / _recovery : 1f;
                    default: return 0f;
                }
            }
        }

        public void Begin(float startup, float active, float recovery)
        {
            _startup = startup < 0f ? 0f : startup;
            _active = active < 0f ? 0f : active;
            _recovery = recovery < 0f ? 0f : recovery;
            Elapsed = 0f;
            HasReachedActive = false;
            Phase = AttackPhase.Startup;
        }

        public AttackTimelineEvents Tick(float dt)
        {
            if (Phase == AttackPhase.None || dt <= 0f) return AttackTimelineEvents.None;

            AttackTimelineEvents events = AttackTimelineEvents.None;
            Elapsed += dt;

            if (Phase == AttackPhase.Startup && Elapsed >= _startup)
            {
                Phase = AttackPhase.Active;
                HasReachedActive = true;
                events |= AttackTimelineEvents.EnteredActive;
            }

            if (Phase == AttackPhase.Active && Elapsed >= _startup + _active)
            {
                Phase = AttackPhase.Recovery;
                events |= AttackTimelineEvents.ExitedActive;
            }

            if (Phase == AttackPhase.Recovery && Elapsed >= Duration)
            {
                Phase = AttackPhase.None;
                events |= AttackTimelineEvents.Finished;
            }

            return events;
        }

        /// <summary>Stops the attack immediately. Returns true if it had already dealt its active window.</summary>
        public bool Cancel()
        {
            bool reached = HasReachedActive;
            Phase = AttackPhase.None;
            Elapsed = 0f;
            return reached;
        }
    }
}
