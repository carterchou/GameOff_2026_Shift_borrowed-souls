using BorrowedSouls.Core.AI;
using BorrowedSouls.Core.Soul;
using BorrowedSouls.Data;
using BorrowedSouls.Vessels;
using UnityEngine;

namespace BorrowedSouls.AI
{
    /// <summary>
    /// Lightweight FSM shared by melee / ranged / mage enemies:
    /// Acquire → Approach/Reposition → Telegraph → Attack → Recovery → Re-evaluate.
    /// Produces intent only; attacks need one of the shared attack tokens (max 3) before the wind-up starts.
    /// Ranged classes need line of sight and strafe to find a new firing angle when a pillar blocks it.
    /// Elites also deny corpses: when a corpse lies closer to them than the player, they walk over and smash it with a
    /// telegraphed strong attack, so the player has to race them for the body.
    /// </summary>
    public sealed class ClassBrain : MonoBehaviour, IVesselDriver
    {
        private enum State
        {
            Idle,
            Position,
            Telegraph,
            Attacking,
            SeekCorpse
        }

        private enum Move
        {
            Weak,
            Strong,
            Special
        }

        [SerializeField] private AiProfile _profile;
        [Tooltip("Level geometry that blocks line of sight (pillars/walls).")]
        [SerializeField] private LayerMask _sightBlockers = 1;

        private State _state;
        private Move _pending;
        private float _decisionTimer;
        private float _cooldown;
        private float _telegraphLeft;
        private float _attackTime;
        private float _strafeSign = 1f;
        private Vector2 _move;
        private Vector3 _aim = Vector3.forward;
        private bool _hasSight = true;
        private VesselRuntime _corpse;
        private float _smashCooldown;
        private float _outOfSenseTime;

        public string DebugState => _state.ToString();

        /// <summary>Runtime spawn setup (WaveDirector) — call before the vessel activates.</summary>
        public void Configure(AiProfile profile)
        {
            _profile = profile;
        }

        public void BuildIntent(VesselRuntime vessel, ref VesselIntent intent)
        {
            var root = vessel.Root;
            float dt = root.Time.WorldDelta;
            float now = Time.time;
            _cooldown -= dt;
            _smashCooldown -= dt;
            _decisionTimer -= dt;
            int id = vessel.GetInstanceID();

            VesselRuntime target = root.PlayerVessel;
            bool hasTarget = target != null && !target.IsDead && target != vessel;
            float distance = float.MaxValue;
            if (hasTarget)
            {
                Vector3 to = target.transform.position - vessel.transform.position;
                to.y = 0f;
                distance = to.magnitude;
                if (distance > 0.01f) _aim = to / distance;
                intent.Aim = _aim;
            }
            else
            {
                root.AttackTokens.Release(id);
                // No body to fight (the player is flying as a soul): exactly when a corpse matters, so elites race for it.
                if (_corpse == null && _state != State.Telegraph && _state != State.Attacking
                    && vessel.State.Kind == VesselKind.Elite && _smashCooldown <= 0f)
                {
                    _corpse = NearestCorpse(vessel, _profile.CorpseSmashRadius);
                    if (_corpse != null) _state = State.SeekCorpse;
                }
                if (_corpse == null && _state != State.Attacking)
                {
                    _state = State.Idle;
                    return;
                }
            }

            if (_corpse != null && !_corpse.State.IsFallenCorpse) _corpse = null; // dissolved, borrowed or smashed
            if (_state == State.SeekCorpse && hasTarget && distance <= _profile.CorpseSmashAbortDistance) _corpse = null; // fight back first
            if (_state == State.SeekCorpse && _corpse == null) _state = hasTarget ? State.Position : State.Idle;
            if (_corpse != null)
            {
                Vector3 toCorpse = _corpse.transform.position - vessel.transform.position;
                toCorpse.y = 0f;
                if (toCorpse.sqrMagnitude > 1e-4f) intent.Aim = toCorpse.normalized;
                if (_state == State.SeekCorpse)
                {
                    float reach = Mathf.Min(_profile.AttackRange, 2f);
                    if (toCorpse.magnitude > reach)
                    {
                        Vector3 dir = toCorpse.normalized;
                        intent.Move = new Vector2(dir.x, dir.z);
                        return;
                    }
                    _pending = Move.Strong;
                    _telegraphLeft = _profile.CorpseSmashTelegraph;
                    _state = State.Telegraph;
                }
            }

            switch (_state)
            {
                case State.Telegraph:
                    // Stand, face the target and glow: the readable wind-up before the real attack startup.
                    intent.Telegraphing = true;
                    intent.TelegraphAttack = PendingAttack(vessel);
                    _telegraphLeft -= dt;
                    if (_telegraphLeft > 0f) return;
                    PressPending(ref intent);
                    _attackTime = now;
                    _state = State.Attacking;
                    if (_corpse != null)
                    {
                        // The swing lands on the body: shattered, not borrowable.
                        if (_corpse.State.BreakCorpse()) root.Events.Publish(new Combat.CorpseSmashed { Corpse = _corpse, By = vessel });
                        _corpse = null;
                        _smashCooldown = _profile.CorpseSmashCooldown;
                    }
                    return;

                case State.Attacking:
                    if (vessel.Combat.IsAttacking || now - _attackTime < 0.1f)
                    {
                        root.AttackTokens.TryAcquire(id, now, root.CombatSettings.AttackTokenLease); // keep lease while swinging
                        return;
                    }
                    root.AttackTokens.Release(id);
                    _cooldown = _profile.AttackCooldown * Random.Range(0.8f, 1.25f);
                    _state = State.Position;
                    break;
            }

            if (!hasTarget) return; // a smash in progress keeps running without a player body; nothing else to decide

            if (_decisionTimer <= 0f)
            {
                _decisionTimer = _profile.DecisionInterval;
                Decide(vessel, target, distance, now, id);
            }
            if (_state == State.Telegraph) return;
            intent.Move = _move;
        }

        private void Decide(VesselRuntime vessel, VesselRuntime target, float distance, float now, int id)
        {
            _outOfSenseTime = distance > _profile.SenseRange ? _outOfSenseTime + _profile.DecisionInterval : 0f;
            AiMove moveChoice = AiPositioning.Choose(distance, _profile.SenseRange, _profile.PreferredMax, _profile.RetreatDistance, _outOfSenseTime, _profile.OutOfSenseHold);
            _state = moveChoice == AiMove.Hold ? State.Idle : State.Position;
            _move = Vector2.zero;
            if (_state == State.Idle) return;

            // Elites deny the player a body when a corpse lies closer to them than the player does. (The first rule,
            // "player out of attack range", never fired for ranged/mage elites, whose range covers most of the arena.)
            if (vessel.State.Kind == VesselKind.Elite && _smashCooldown <= 0f && distance > _profile.CorpseSmashAbortDistance)
            {
                VesselRuntime corpse = NearestCorpse(vessel, _profile.CorpseSmashRadius);
                if (corpse != null && Vector3.Distance(corpse.transform.position, vessel.transform.position) < distance)
                {
                    _corpse = corpse;
                    _state = State.SeekCorpse;
                    return;
                }
            }

            _hasSight = !_profile.RequiresLineOfSight || HasLineOfSight(vessel, target);
            Vector3 away = -_aim;
            Vector3 side = new Vector3(-_aim.z, 0f, _aim.x) * _strafeSign;

            Vector3 desired;
            if (moveChoice == AiMove.Approach) desired = _aim;
            else if (moveChoice == AiMove.Retreat) desired = (away + side * 0.4f).normalized;
            else desired = side * _profile.StrafeSpeed;

            if (!_hasSight) desired = (side + _aim * 0.3f).normalized; // find a new firing angle around the pillar
            if (Random.value < 0.08f) _strafeSign = -_strafeSign;
            _move = new Vector2(desired.x, desired.z);

            bool inRange = distance <= _profile.AttackRange && distance >= _profile.PreferredMin * 0.5f;
            if (!inRange || !_hasSight || _cooldown > 0f || vessel.Combat.IsAttacking) return;
            if (!vessel.Root.AttackTokens.TryAcquire(id, now, vessel.Root.CombatSettings.AttackTokenLease)) return;

            _pending = PickMove(vessel, distance);
            _telegraphLeft = _profile.ExtraTelegraph * (_pending == Move.Weak ? 1f : 1.6f);
            _state = State.Telegraph;
            _move = Vector2.zero;
        }

        private Move PickMove(VesselRuntime vessel, float distance)
        {
            float r = Random.value;
            if (r < _profile.SpecialChance && distance <= _profile.SpecialMaxDistance && vessel.Combat.SpecialCooldownLeft <= 0f) return Move.Special;
            if (r < _profile.SpecialChance + _profile.StrongChance) return Move.Strong;
            return Move.Weak;
        }

        private AttackDefinition PendingAttack(VesselRuntime vessel)
        {
            WeaponDefinition w = vessel.Definition.Weapon;
            switch (_pending)
            {
                case Move.Strong: return w.Strong;
                case Move.Special: return w.Special;
                default: return w.GetWeak(vessel.Combat.NextComboIndex);
            }
        }

        private void PressPending(ref VesselIntent intent)
        {
            switch (_pending)
            {
                case Move.Strong: intent.StrongPressed = true; break;
                case Move.Special: intent.SpecialPressed = true; break;
                default: intent.WeakPressed = true; break;
            }
        }

        /// <summary>The closest body lying within reach that the player could still borrow. Shared with the boss.</summary>
        public static VesselRuntime NearestCorpse(VesselRuntime self, float radius)
        {
            VesselRuntime best = null;
            float bestSqr = radius * radius;
            var vessels = self.Root.Vessels;
            for (int i = 0; i < vessels.Count; i++)
            {
                VesselRuntime v = vessels[i];
                if (v == self || !v.State.IsFallenCorpse) continue;
                float sqr = (v.transform.position - self.transform.position).sqrMagnitude;
                if (sqr > bestSqr) continue;
                bestSqr = sqr;
                best = v;
            }
            return best;
        }

        private bool HasLineOfSight(VesselRuntime vessel, VesselRuntime target)
        {
            Vector3 from = vessel.transform.position + Vector3.up * 1.1f;
            Vector3 to = target.transform.position + Vector3.up * 1.1f;
            return !Physics.Linecast(from, to, _sightBlockers, QueryTriggerInteraction.Ignore);
        }
    }
}
