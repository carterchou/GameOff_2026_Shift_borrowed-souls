using System.Collections.Generic;
using BorrowedSouls.Combat;
using BorrowedSouls.Core.Combat;
using BorrowedSouls.Core.Events;
using BorrowedSouls.Core.Meta;
using BorrowedSouls.Data;
using BorrowedSouls.Meta;
using BorrowedSouls.Vessels;
using UnityEngine;

namespace BorrowedSouls.Bootstrap
{
    /// <summary>
    /// Scene composition root. Owns the per-run services (event bus, damage resolver, vessel registry) and
    /// hands them to scene components through serialized references — no singletons, no Find.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class GameRoot : MonoBehaviour
    {
        [SerializeField] private TimeService _time;
        [SerializeField] private CombatSettings _combatSettings;
        [SerializeField] private SoulSettings _soulSettings;
        [SerializeField] private TalentCatalog _talents;
        [SerializeField] private ProjectilePool _projectiles;

        private readonly EventBus _events = new EventBus();
        private readonly List<VesselRuntime> _vessels = new List<VesselRuntime>(32);
        private DamageResolver _damage;
        private AttackTokenPool _attackTokens;
        private RunBonuses? _bonuses;

        public TimeService Time => _time;
        public CombatSettings CombatSettings => _combatSettings;
        public SoulSettings SoulSettings => _soulSettings;
        public ProjectilePool Projectiles => _projectiles;
        public EventBus Events => _events;
        public DamageResolver Damage => _damage;
        public AttackTokenPool AttackTokens => _attackTokens;
        public GameOptions Options { get; private set; }

        /// <summary>What the talents bought in the rest room add to this run. Fixed when first read.</summary>
        public RunBonuses Bonuses
        {
            get
            {
                if (_bonuses == null) _bonuses = _talents != null && _talents.Released ? RunBonuses.From(ProfileStore.Current(_talents), _talents.Rules) : default;
                return _bonuses.Value;
            }
        }
        public IReadOnlyList<VesselRuntime> Vessels => _vessels;

        /// <summary>The vessel the player soul currently controls; null while the soul flies free.</summary>
        public VesselRuntime PlayerVessel { get; set; }

        private void Awake()
        {
            _time.Configure(_combatSettings.MaxHitStopChain, _combatSettings.MinHitStopGap);
            _damage = new DamageResolver(_events, _combatSettings);
            _attackTokens = new AttackTokenPool(_combatSettings.MaxConcurrentAttackers);
            Options = GameOptions.Load();
        }

        private void OnDestroy()
        {
            _events.Clear();
        }

        public void Register(VesselRuntime vessel)
        {
            if (!_vessels.Contains(vessel)) _vessels.Add(vessel);
        }

        public void Unregister(VesselRuntime vessel)
        {
            _vessels.Remove(vessel);
        }
    }
}
