namespace BorrowedSouls.Core.Soul
{
    public enum VesselOwner
    {
        EnemyAI,
        Player,
        Empty,
        Broken
    }

    public enum VesselKind
    {
        Normal,
        Elite,
        Boss
    }

    /// <summary>
    /// Pure ownership/health state of one body. HP, ownership and the "first claim used" flag live here —
    /// never on the soul. Only <see cref="PossessionService"/> mutates <see cref="Owner"/>.
    ///
    /// </summary>
    public sealed class VesselState
    {
        public VesselState(int id, VesselKind kind, float maxHp, VesselOwner initialOwner)
        {
            Id = id;
            Kind = kind;
            MaxHp = maxHp;
            Hp = maxHp;
            Owner = initialOwner;
            HasEnemySoul = initialOwner == VesselOwner.EnemyAI;
            CorpseTimeLeft = -1f;
        }

        public int Id { get; }
        public VesselKind Kind { get; }
        public float MaxHp { get; private set; }
        public float Hp { get; private set; }
        public VesselOwner Owner { get; internal set; }

        /// <summary>A living enemy's original soul still belongs to this body (active or suppressed).</summary>
        public bool HasEnemySoul { get; internal set; }
        public bool EnemySoulSuppressed { get; internal set; }

        /// <summary>The player already received the first-possession heal from this body.</summary>
        public bool ClaimedByPlayer { get; internal set; }

        /// <summary>Seconds left to claim a fallen, never-possessed enemy body; negative when not a corpse.</summary>
        public float CorpseTimeLeft { get; internal set; }

        public bool IsAlive => Hp > 0f && Owner != VesselOwner.Broken;
        public bool IsBroken => Owner == VesselOwner.Broken;
        public bool IsFallenCorpse => Owner == VesselOwner.Empty && CorpseTimeLeft > 0f;
        public bool IsVacated => Owner == VesselOwner.Empty && CorpseTimeLeft < 0f && Hp > 0f;

        /// <summary>Applies damage and resolves death transitions. Returns true if this hit killed the body.</summary>
        public bool ApplyDamage(float amount, float corpseLifetime)
        {
            if (amount <= 0f || Owner == VesselOwner.Broken || Hp <= 0f) return false;
            Hp -= amount;
            if (Hp > 0f) return false;

            Hp = 0f;
            BurnTimeLeft = 0f;
            switch (Owner)
            {
                case VesselOwner.EnemyAI:
                    // An enemy killed while its own soul controls it leaves a claimable corpse (10s by default).
                    Owner = Kind == VesselKind.Boss ? VesselOwner.Broken : VesselOwner.Empty;
                    HasEnemySoul = false;
                    EnemySoulSuppressed = false;
                    CorpseTimeLeft = Kind == VesselKind.Boss ? -1f : corpseLifetime;
                    break;
                default:
                    // Player-held body, or an empty shell killed by DOT/AoE: destroyed for good (SOUL-03, SOUL-16).
                    Owner = VesselOwner.Broken;
                    HasEnemySoul = false;
                    EnemySoulSuppressed = false;
                    CorpseTimeLeft = -1f;
                    break;
            }
            return true;
        }

        /// <summary>Remaining burn time; burn lives on the body and survives possession changes (COMBAT-07).</summary>
        /// <summary>Revived from a corpse by the player: such a body breaks when the soul leaves (one borrow per corpse).</summary>
        public bool RevivedFromCorpse { get; internal set; }

        /// <summary>An enemy smashes a lying corpse so the player cannot borrow it. False if not a corpse.</summary>
        public bool BreakCorpse()
        {
            if (!IsFallenCorpse) return false;
            CorpseTimeLeft = -1f;
            Owner = VesselOwner.Broken;
            return true;
        }


        public float BurnTimeLeft { get; private set; }
        public bool IsBurning => BurnTimeLeft > 0f;
        private float _burnDps;

        /// <summary>Apply / refresh burn.</summary>
        public void ApplyBurn(float duration, float totalDamage)
        {
            if (duration <= 0f || !IsAlive) return;
            BurnTimeLeft = duration;
            _burnDps = totalDamage / duration;
        }

        /// <summary>Ticks status effects on world time, also while the body is empty. Returns true if it died.</summary>
        public bool TickStatus(float dt, float corpseLifetime)
        {
            if (BurnTimeLeft <= 0f || dt <= 0f) return false;
            float step = dt < BurnTimeLeft ? dt : BurnTimeLeft;
            BurnTimeLeft -= step;
            if (BurnTimeLeft < 1e-4f) BurnTimeLeft = 0f; // float residue must not leave a body "burning" forever
            return ApplyDamage(_burnDps * step, corpseLifetime);
        }

        /// <summary>Full reset for pooled respawns / training dummies. Never used for gameplay healing.</summary>
        public void Restore(VesselOwner owner)
        {
            Hp = MaxHp;
            Owner = owner;
            HasEnemySoul = owner == VesselOwner.EnemyAI;
            EnemySoulSuppressed = false;
            ClaimedByPlayer = false;
            RevivedFromCorpse = false;
            CorpseTimeLeft = -1f;
            BurnTimeLeft = 0f;
        }

        public void Heal(float amount)
        {
            if (amount <= 0f || !IsAlive) return;
            Hp = Hp + amount > MaxHp ? MaxHp : Hp + amount;
        }

        /// <summary>MaxHP change keeps the HP ratio.</summary>
        public void SetMaxHpKeepRatio(float newMax)
        {
            if (newMax <= 0f) return;
            float ratio = MaxHp > 0f ? Hp / MaxHp : 1f;
            MaxHp = newMax;
            if (Hp > 0f) Hp = ratio * newMax;
        }

        internal void SetHp(float hp)
        {
            Hp = hp < 0f ? 0f : (hp > MaxHp ? MaxHp : hp);
        }

        internal void TickCorpse(float dt)
        {
            if (!IsFallenCorpse) return;
            CorpseTimeLeft -= dt;
            if (CorpseTimeLeft <= 0f)
            {
                CorpseTimeLeft = -1f;
                Owner = VesselOwner.Broken; // dissolved, no longer claimable (SOUL-07)
            }
        }
    }
}
