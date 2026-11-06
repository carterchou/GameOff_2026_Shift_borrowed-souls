using BorrowedSouls.Bootstrap;
using BorrowedSouls.Core.Combat;
using BorrowedSouls.Core.Soul;
using BorrowedSouls.Core.Stats;
using BorrowedSouls.Data;
using UnityEngine;

namespace BorrowedSouls.Vessels
{
    public struct VesselHitResult
    {
        public bool Killed;
        public bool Interrupted;
    }

    /// <summary>
    /// A physical body. HP and ownership live in the pure <see cref="VesselState"/> (everything belongs
    /// to the vessel, never to the soul); only PossessionService changes ownership. Team follows the owner, so a
    /// possessed enemy fights for the player. The driver (player input or AI) only produces intent.
    /// </summary>
    [RequireComponent(typeof(VesselMotor), typeof(VesselCombat))]
    public sealed class VesselRuntime : MonoBehaviour
    {
        [SerializeField] private GameRoot _root;
        [SerializeField] private VesselDefinition _definition;
        [Tooltip("Name shown instead of the definition's, for bodies like a training dummy that reuse one.")]
        [SerializeField] private string _displayNameOverride = "";
        [SerializeField] private Team _team = Team.Enemy;
        [SerializeField] private VesselKind _kind = VesselKind.Normal;
        [Tooltip("Component implementing IVesselDriver: the AI that owns this body when no player soul is inside.")]
        [SerializeField] private MonoBehaviour _initialDriver;

        private VesselMotor _motor;
        private VesselCombat _combat;
        private VesselIntent _intent;
        private float _staggerLeft;
        private float _shieldCooldownLeft;
        private float _eliteHpMultiplier = 1f;
        private float _eliteAiDamageMultiplier = 1f;
        private static readonly object EliteBodySource = new object();
        private static readonly object EliteAiSource = new object();

        public GameRoot Root => _root;
        public VesselDefinition Definition => _definition;
        public string DisplayName => string.IsNullOrEmpty(_displayNameOverride) ? _definition.DisplayName : _displayNameOverride;
        public VesselClass Class => _definition.Class;
        public VesselMotor Motor => _motor;
        public VesselCombat Combat => _combat;
        public VesselState State { get; private set; }
        public IVesselDriver Driver { get; private set; }
        /// <summary>The body's own AI (resumes control when a suppressed enemy soul takes back its body).</summary>
        public IVesselDriver AiDriver { get; private set; }

        public Stat MaxHp { get; private set; }
        public Stat MoveSpeed { get; private set; }
        public Stat DamageMultiplier { get; private set; }

        public bool ProjectileShieldReady => _definition.ProjectileShieldCooldown > 0f && _shieldCooldownLeft <= 0f;
        public float Hp => State.Hp;
        public bool IsDead => !State.IsAlive;
        /// <summary>No soul in it, dead or left behind by the player: the body lies on the ground.</summary>
        public bool IsDown => IsDead || State.IsVacated;
        public bool IsStaggered => _staggerLeft > 0f;
        public bool IsInvulnerable => _combat.IsInvulnerable;
        public VesselIntent LastIntent => _intent;
        /// <summary>Set up as the player's body, readable before Awake has built <see cref="State"/>.</summary>
        public bool StartsAsPlayer => _team == Team.Player;

        public Team Team
        {
            get
            {
                switch (State.Owner)
                {
                    case VesselOwner.Player: return Team.Player;
                    case VesselOwner.EnemyAI: return Team.Enemy;
                    default: return Team.Neutral;
                }
            }
        }

        /// <summary>
        /// Runtime spawn setup (WaveDirector). Must be called before Awake: spawn into an inactive parent,
        /// configure, then activate.
        /// </summary>
        public void Configure(GameRoot root, Team team, VesselKind kind, MonoBehaviour aiDriver, float eliteHpMultiplier, float eliteAiDamageMultiplier)
        {
            _root = root;
            _team = team;
            _kind = kind;
            _initialDriver = aiDriver;
            _eliteHpMultiplier = eliteHpMultiplier;
            _eliteAiDamageMultiplier = eliteAiDamageMultiplier;
        }

        private void Awake()
        {
            _motor = GetComponent<VesselMotor>();
            _combat = GetComponent<VesselCombat>();

            // Copy definition data into runtime stats; the ScriptableObject is never modified.
            MaxHp = new Stat(_definition.MaxHp);
            MoveSpeed = new Stat(_definition.MoveSpeed);
            DamageMultiplier = new Stat(1f);
            //: MaxHP x1.5 belongs to the body (kept when possessed); the attack bonus is AI-only.
            if (_eliteHpMultiplier != 1f) MaxHp.AddModifier(new StatModifier(_eliteHpMultiplier, StatModType.Multiply, EliteBodySource));

            VesselOwner owner = _team == Team.Player ? VesselOwner.Player : VesselOwner.EnemyAI;
            State = new VesselState(GetInstanceID(), _kind, MaxHp.Value, owner);

            _motor.Init(this);
            _combat.Init(this);
            AiDriver = _team == Team.Player ? null : _initialDriver as IVesselDriver;
            Driver = AiDriver;
            SetAiBonus(Driver != null);
        }

        private void OnEnable()
        {
            _root.Register(this);
        }

        private void OnDisable()
        {
            _root.Unregister(this);
        }

        /// <summary>Called by the possession layer when control changes hands.</summary>
        public void SetDriver(IVesselDriver driver)
        {
            Driver = driver;
            _combat.Interrupt();
            SetAiBonus(driver != null && driver == AiDriver);
        }

        private void SetAiBonus(bool active)
        {
            DamageMultiplier.RemoveAllFromSource(EliteAiSource);
            if (active && _eliteAiDamageMultiplier != 1f) DamageMultiplier.AddModifier(new StatModifier(_eliteAiDamageMultiplier, StatModType.Multiply, EliteAiSource));
        }

        private void Update()
        {
            float dt = _root.Time.WorldDelta;
            if (dt <= 0f) return;

            BorrowedSouls.Debugging.DevWatchdog.Mark("vessel status", GetInstanceID());
            // Status effects tick on the body even while it is empty.
            if (State.TickStatus(dt, _root.SoulSettings.CorpseClaimLifetime))
            {
                OnKilled();
                _root.Events.Publish(new BorrowedSouls.Combat.VesselDied { Vessel = this, Killer = null, LastHitDirection = Vector3.zero });
            }
            _motor.SetCollisionEnabled(!IsDead);
            if (IsDead) return;
            if (_shieldCooldownLeft > 0f) _shieldCooldownLeft -= dt;

            if (_staggerLeft > 0f) _staggerLeft -= dt;

            _intent = default;
            bool controlled = State.Owner == VesselOwner.Player || State.Owner == VesselOwner.EnemyAI;
            BorrowedSouls.Debugging.DevWatchdog.Mark("vessel intent", GetInstanceID());
            if (controlled && !IsStaggered && Driver != null) Driver.BuildIntent(this, ref _intent);

            BorrowedSouls.Debugging.DevWatchdog.Mark("vessel combat", GetInstanceID());
            _combat.Tick(_intent, dt, IsStaggered);
            BorrowedSouls.Debugging.DevWatchdog.Mark("vessel motor", GetInstanceID());
            _motor.Tick(_intent, dt, _combat.MoveSpeedMultiplier, _combat.CanTurn && !IsStaggered);
        }

        /// <summary>Called by DamageResolver only.</summary>
        public VesselHitResult ReceiveHit(AttackDefinition attack, float damage, Vector3 direction)
        {
            var result = new VesselHitResult();
            result.Killed = State.ApplyDamage(damage, _root.SoulSettings.CorpseClaimLifetime);

            if (!result.Killed && !_definition.SuperArmor && attack.HitStun > 0f && _combat.CanBeInterruptedBy(attack))
            {
                result.Interrupted = _combat.Interrupt();
                _staggerLeft = Mathf.Max(_staggerLeft, attack.HitStun);
            }

            Vector3 planar = new Vector3(direction.x, 0f, direction.z);
            if (attack.Knockback > 0f && !_definition.SuperArmor && planar.sqrMagnitude > 1e-4f)
            {
                _motor.AddKnockback(planar.normalized * attack.Knockback * _root.CombatSettings.KnockbackDamping * 0.5f);
            }

            if (result.Killed) OnKilled();
            return result;
        }

        /// <summary>Falls where it stands, with no hit to show for it: the boss is gone, and what it held up goes with it.</summary>
        public void FallWithoutAHit()
        {
            if (!State.ApplyDamage(State.Hp, _root.SoulSettings.CorpseClaimLifetime)) return;
            OnKilled();
            _root.Events.Publish(new BorrowedSouls.Combat.VesselDied { Vessel = this, Killer = null, LastHitDirection = Vector3.zero });
        }

        /// <summary>Mage trait: consume the periodic projectile shield if it is ready.</summary>
        public bool TryConsumeProjectileShield()
        {
            if (!ProjectileShieldReady) return false;
            _shieldCooldownLeft = _definition.ProjectileShieldCooldown;
            return true;
        }

        private void OnKilled()
        {
            _combat.Interrupt();
            _staggerLeft = 0f;
            Driver = null;
        }

        /// <summary>Pooled respawn / training dummy reset. Gives control back to the body's own AI.</summary>
        public void Restore()
        {
            _staggerLeft = 0f;
            State.Restore(AiDriver != null ? VesselOwner.EnemyAI : VesselOwner.Empty);
            Driver = AiDriver;
            SetAiBonus(Driver != null);
        }
    }
}
