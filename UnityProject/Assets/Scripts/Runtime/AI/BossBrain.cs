using BorrowedSouls.Combat;
using BorrowedSouls.Data;
using BorrowedSouls.Vessels;
using UnityEngine;

namespace BorrowedSouls.AI
{
    /// <summary>
    /// 監魂者 (Soul Warden) grey-box brain: heavy slash with a ground fan (weapon weak),
    /// soul-ring shockwave (weapon strong), and periodic summons of possessable adds through
    /// <see cref="BossSummonRequested"/>. HP thresholds 70% / 35% raise the attack tempo and emit
    /// <see cref="BossPhaseChanged"/>. The body has super armor; clear punish windows come from long recoveries.
    /// Like an elite, and unlike a normal enemy, it smashes a body the player could borrow when that body is closer than the player
    ///, with a longer wait between smashes, since the fight is built on borrowing the adds it summons.
    /// </summary>
    public sealed class BossBrain : MonoBehaviour, IVesselDriver
    {
        private enum State
        {
            Chase,
            Telegraph,
            Attacking
        }

        [SerializeField, Min(0f)] private float _baseCooldown = 1.6f;
        [SerializeField, Min(0f)] private float _slashTelegraph = 0.45f;
        [SerializeField, Min(0f)] private float _ringTelegraph = 0.6f;
        [SerializeField, Min(1f)] private float _summonInterval = 22f;
        [SerializeField, Min(1)] private int _summonCount = 2;
        [SerializeField] private float[] _phaseThresholds = { 0.7f, 0.35f };
        [SerializeField] private float[] _phaseCooldownScale = { 1f, 0.8f, 0.65f };

        [Header("Smashing corpses")]
        [SerializeField, Min(0f)] private float _smashRadius = 9f;
        [SerializeField, Min(0f)] private float _smashTelegraph = 0.8f;
        [SerializeField, Min(0f)] private float _smashCooldown = 12f;
        [Tooltip("With the player this close, the boss fights the player instead.")]
        [SerializeField, Min(0f)] private float _smashAbortDistance = 4f;
        [SerializeField, Min(0.1f)] private float _smashReach = 2.5f;

        private State _state;
        private bool _ringPending;
        private float _cooldown = 1.5f;
        private float _telegraphLeft;
        private float _attackTime;
        private float _summonTimer = 8f;
        private int _phase;
        private float _smashWait;
        private VesselRuntime _corpse;

        public int Phase => _phase;

        public void BuildIntent(VesselRuntime vessel, ref VesselIntent intent)
        {
            var root = vessel.Root;
            float dt = root.Time.WorldDelta;
            _cooldown -= dt;
            _summonTimer -= dt;
            _smashWait -= dt;
            UpdatePhase(vessel);

            if (_summonTimer <= 0f && _state == State.Chase)
            {
                _summonTimer = _summonInterval * (_phase >= 2 ? 0.8f : 1f);
                root.Events.Publish(new BossSummonRequested { Boss = vessel, Count = _summonCount });
            }

            VesselRuntime target = root.PlayerVessel;
            bool hasTarget = target != null && !target.IsDead;
            Vector3 to = hasTarget ? target.transform.position - vessel.transform.position : Vector3.zero;
            to.y = 0f;
            float distance = hasTarget ? to.magnitude : float.PositiveInfinity;

            ChooseCorpse(vessel, hasTarget, distance);
            if (!hasTarget && _corpse == null)
            {
                _state = State.Chase;
                return; // soul in flight with no body to deny: the warden waits, it does not hunt the soul
            }

            Vector3 aim = hasTarget && distance > 0.01f ? to / distance : vessel.transform.forward;
            intent.Aim = aim;

            WeaponDefinition w = vessel.Definition.Weapon;
            AttackDefinition slash = w.GetWeak(0);
            AttackDefinition ring = w.Strong;

            if (_corpse != null)
            {
                Vector3 toCorpse = _corpse.transform.position - vessel.transform.position;
                toCorpse.y = 0f;
                if (toCorpse.sqrMagnitude > 1e-4f) intent.Aim = toCorpse.normalized;
                if (_state == State.Chase)
                {
                    if (toCorpse.magnitude > _smashReach)
                    {
                        Vector3 way = toCorpse.normalized;
                        intent.Move = new Vector2(way.x, way.z);
                        return;
                    }
                    _ringPending = false;
                    _telegraphLeft = _smashTelegraph;
                    _state = State.Telegraph;
                }
            }

            switch (_state)
            {
                case State.Telegraph:
                    intent.Telegraphing = true;
                    intent.TelegraphAttack = _ringPending ? ring : slash;
                    _telegraphLeft -= dt;
                    if (_telegraphLeft > 0f) return;
                    if (_ringPending) intent.StrongPressed = true;
                    else intent.WeakPressed = true;
                    _attackTime = Time.time;
                    _state = State.Attacking;
                    if (_corpse != null)
                    {
                        // The swing lands on the body: shattered, not borrowable.
                        if (_corpse.State.BreakCorpse()) root.Events.Publish(new CorpseSmashed { Corpse = _corpse, By = vessel });
                        _corpse = null;
                        _smashWait = _smashCooldown;
                    }
                    return;

                case State.Attacking:
                    if (vessel.Combat.IsAttacking || Time.time - _attackTime < 0.1f) return;
                    _cooldown = _baseCooldown * _phaseCooldownScale[Mathf.Min(_phase, _phaseCooldownScale.Length - 1)] * Random.Range(0.85f, 1.15f);
                    _state = State.Chase;
                    break;
            }

            if (_cooldown <= 0f)
            {
                bool slashRange = distance <= slash.Range * 0.9f;
                bool ringRange = ring != null && distance <= ring.Range * 0.75f;
                if (slashRange || ringRange)
                {
                    _ringPending = ring != null && (!slashRange || Random.value < 0.3f);
                    _telegraphLeft = _ringPending ? _ringTelegraph : _slashTelegraph;
                    _state = State.Telegraph;
                    if (_ringPending) root.Events.Publish(new BossAttackAnnounced { Boss = vessel, Attack = ring });
                    return;
                }
            }

            if (distance > slash.Range * 0.7f) intent.Move = new Vector2(aim.x, aim.z);
        }

        /// <summary>Picks a body to smash, or lets one go if it has gone or the player has come close.</summary>
        private void ChooseCorpse(VesselRuntime vessel, bool hasTarget, float distance)
        {
            if (_corpse != null && !_corpse.State.IsFallenCorpse) _corpse = null; // dissolved, borrowed or smashed
            if (_corpse != null && _state == State.Chase && distance <= _smashAbortDistance) _corpse = null;
            if (_corpse != null || _state != State.Chase || _smashWait > 0f || distance <= _smashAbortDistance) return;
            VesselRuntime corpse = ClassBrain.NearestCorpse(vessel, _smashRadius);
            if (corpse == null) return;
            if (!hasTarget || Vector3.Distance(corpse.transform.position, vessel.transform.position) < distance) _corpse = corpse;
        }

        private void UpdatePhase(VesselRuntime vessel)
        {
            float ratio = vessel.MaxHp.Value > 0f ? vessel.Hp / vessel.MaxHp.Value : 0f;
            while (_phase < _phaseThresholds.Length && ratio <= _phaseThresholds[_phase])
            {
                _phase++;
                vessel.Root.Events.Publish(new BossPhaseChanged { Boss = vessel, Phase = _phase });
                _summonTimer = Mathf.Min(_summonTimer, 2f); // a phase change brings fresh bodies soon
            }
        }
    }
}
