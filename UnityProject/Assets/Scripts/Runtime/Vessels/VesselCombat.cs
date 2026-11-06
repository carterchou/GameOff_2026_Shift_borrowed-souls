using System.Collections.Generic;
using BorrowedSouls.Combat;
using BorrowedSouls.Core.Combat;
using BorrowedSouls.Data;
using UnityEngine;

namespace BorrowedSouls.Vessels
{
    /// <summary>
    /// Weak/strong/special attacks and dodge for any vessel (player- or AI-driven).
    /// Attack phases come from <see cref="AttackTimeline"/>; hits are only resolved while Active, once per
    /// target per swing. Dodge cancels attacks and resets the weak chain.
    /// </summary>
    public sealed class VesselCombat : MonoBehaviour
    {
        private enum BufferedAction
        {
            None,
            Weak,
            Strong,
            Special,
            Dodge
        }

        [Tooltip("Layers that contain vessel colliders (hurtboxes).")]
        [SerializeField] private LayerMask _hitMask = ~0;

        private readonly AttackTimeline _timeline = new AttackTimeline();
        private readonly DodgeState _dodge = new DodgeState();
        private readonly HashSet<VesselRuntime> _hitThisSwing = new HashSet<VesselRuntime>();
        private readonly Dictionary<VesselRuntime, int> _multiHits = new Dictionary<VesselRuntime, int>();
        private readonly Collider[] _overlap = new Collider[24];

        private VesselRuntime _vessel;
        private ComboCounter _combo;
        private AttackDefinition _current;
        private BufferedAction _buffered;
        private float _bufferLeft;
        private float _specialCooldownLeft;
        private int _shotsFired;
        private float _nextMultiHitTime;
        private AttackSource _source;

        public bool IsInvulnerable => _dodge.IsInvulnerable;
        public bool IsDodging => _dodge.IsDodging;
        public bool IsAttacking => _timeline.IsBusy;
        public AttackPhase Phase => _timeline.Phase;
        public float PhaseProgress => _timeline.PhaseProgress;
        public float Elapsed => _timeline.Elapsed;
        public AttackDefinition CurrentAttack => _current;
        public int NextComboIndex => _combo.NextIndex;
        public float DodgeCooldownLeft => _dodge.CooldownLeft;
        public float SpecialCooldownLeft => _specialCooldownLeft;

        public float MoveSpeedMultiplier => _current != null ? _current.MoveSpeedMultiplier : 1f;

        /// <summary>Aim may rotate the body while idle or winding up; the swing direction locks once Active.</summary>
        public bool CanTurn => !_dodge.IsDodging && (_current == null || _timeline.Phase == AttackPhase.Startup);

        public void Init(VesselRuntime vessel)
        {
            _vessel = vessel;
            VesselDefinition def = vessel.Definition;
            int comboLength = def.Weapon != null ? Mathf.Max(1, def.Weapon.WeakComboLength) : 1;
            _combo = new ComboCounter(comboLength, vessel.Root.CombatSettings.WeakComboResetWindow);
            _dodge.Configure(def.DodgeDuration, def.DodgeInvulnStart, def.DodgeInvulnEnd, def.DodgeCooldown);
        }

        public void Tick(in VesselIntent intent, float dt, bool staggered)
        {
            _dodge.Tick(dt);
            if (_specialCooldownLeft > 0f) _specialCooldownLeft -= dt;
            if (!_timeline.IsBusy) _combo.Tick(dt);

            CaptureBuffer(intent, dt);
            QueueHeldRepeat(intent);

            if (!staggered)
            {
                if (_buffered == BufferedAction.Dodge && _dodge.IsReady) StartDodge(intent);
                else if (!_dodge.IsDodging && !_timeline.IsBusy && _buffered != BufferedAction.None && _buffered != BufferedAction.Dodge) TryStartBufferedAttack(intent);
            }

            if (_current == null) return;

            AttackTimelineEvents events = _timeline.Tick(dt);
            if ((events & AttackTimelineEvents.EnteredActive) != 0) OnEnterActive();
            if (_timeline.Phase == AttackPhase.Active || (events & AttackTimelineEvents.EnteredActive) != 0 || (events & AttackTimelineEvents.ExitedActive) != 0) DeliverActive();
            if ((events & AttackTimelineEvents.Finished) != 0) OnFinished();
        }

        /// <summary>Whether an incoming attack staggers / cancels this vessel's current action.</summary>
        public bool CanBeInterruptedBy(AttackDefinition incoming)
        {
            if (_current == null) return true;
            return _current.Interruptible || incoming.Interrupts;
        }

        /// <summary>Cancels the current attack. Returns true if something was cancelled.</summary>
        public bool Interrupt()
        {
            bool hadAttack = _current != null;
            _timeline.Cancel();
            _current = null;
            _combo.Reset();
            _buffered = BufferedAction.None;
            return hadAttack;
        }

        private void CaptureBuffer(in VesselIntent intent, float dt)
        {
            BufferedAction pressed = BufferedAction.None;
            if (intent.DodgePressed) pressed = BufferedAction.Dodge;
            else if (intent.SpecialPressed) pressed = BufferedAction.Special;
            else if (intent.StrongPressed) pressed = BufferedAction.Strong;
            else if (intent.WeakPressed) pressed = BufferedAction.Weak;

            if (pressed != BufferedAction.None)
            {
                _buffered = pressed;
                _bufferLeft = _vessel.Root.CombatSettings.InputBufferTime;
                return;
            }

            if (_buffered == BufferedAction.None) return;
            _bufferLeft -= dt;
            if (_bufferLeft <= 0f) _buffered = BufferedAction.None;
        }

        /// <summary>Longbow weak fires again while the button stays held.</summary>
        private void QueueHeldRepeat(in VesselIntent intent)
        {
            if (!intent.WeakHeld || _timeline.IsBusy || _dodge.IsDodging || _buffered != BufferedAction.None) return;
            WeaponDefinition weapon = _vessel.Definition.Weapon;
            AttackDefinition weak = weapon != null ? weapon.GetWeak(_combo.NextIndex) : null;
            if (weak != null && weak.RepeatWhileHeld) _buffered = BufferedAction.Weak;
        }

        private void StartDodge(in VesselIntent intent)
        {
            if (_current != null) Interrupt();
            _combo.Reset();
            _buffered = BufferedAction.None;
            if (!_dodge.TryStart()) return;

            Vector3 dir = new Vector3(intent.Move.x, 0f, intent.Move.y);
            if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
            dir.Normalize();

            VesselDefinition def = _vessel.Definition;
            _vessel.Motor.FaceImmediately(dir);
            _vessel.Motor.SetForcedVelocity(dir * (def.DodgeDistance / def.DodgeDuration), def.DodgeDuration);
            _vessel.Root.Events.Publish(new DodgeStarted { Vessel = _vessel, Direction = dir });
        }

        private void TryStartBufferedAttack(in VesselIntent intent)
        {
            WeaponDefinition weapon = _vessel.Definition.Weapon;
            if (weapon == null) return;

            AttackDefinition attack = null;
            bool isWeak = false;
            switch (_buffered)
            {
                case BufferedAction.Weak:
                    attack = weapon.GetWeak(_combo.NextIndex);
                    isWeak = true;
                    break;
                case BufferedAction.Strong:
                    attack = weapon.Strong;
                    break;
                case BufferedAction.Special:
                    if (_specialCooldownLeft > 0f) return; // keep it buffered until ready or expired
                    attack = weapon.Special;
                    break;
            }
            if (attack == null) return;

            if (isWeak) _combo.Consume();
            else _combo.Reset();
            if (_buffered == BufferedAction.Special) _specialCooldownLeft = attack.Cooldown;

            _buffered = BufferedAction.None;
            _current = attack;
            _source = AttackSource.From(_vessel); // snapshot: projectiles keep the team they were fired with
            _vessel.Motor.FaceImmediately(intent.Aim);
            _timeline.Begin(attack.Startup, attack.Active, attack.Recovery);
            _vessel.Root.Events.Publish(new AttackStarted { Attacker = _vessel, Attack = attack });
        }

        private void OnEnterActive()
        {
            _hitThisSwing.Clear();
            _multiHits.Clear();
            _shotsFired = 0;
            _nextMultiHitTime = 0f;
            _vessel.Root.Events.Publish(new AttackReleased { Attacker = _vessel, Attack = _current });
            if (_current.LungeDistance > 0f && _current.Active > 0f)
            {
                _vessel.Motor.SetForcedVelocity(transform.forward * (_current.LungeDistance / _current.Active), _current.Active);
            }
        }

        private void OnFinished()
        {
            if (_current != null && _current.Tier == HitTier.Weak) _combo.MarkAttackFinished();
            else _combo.Reset();
            _current = null;
        }

        private void DeliverActive()
        {
            switch (_current.Delivery)
            {
                case AttackDelivery.Projectile: FireDueShots(); break;
                case AttackDelivery.Cone: ResolveHits(multiHit: true); break;
                case AttackDelivery.Ring: ResolveRing(); break;
                default: ResolveHits(multiHit: false); break;
            }
        }

        private void FireDueShots()
        {
            AttackDefinition attack = _current;
            float activeElapsed = _timeline.Elapsed - attack.Startup;
            float spacing = attack.Shots > 1 ? attack.Active / attack.Shots : 0f;
            while (_shotsFired < attack.Shots && activeElapsed >= _shotsFired * spacing)
            {
                Vector3 dir = transform.forward;
                if (attack.SpreadDegrees > 0f && attack.Shots > 1)
                {
                    float t = (float)_shotsFired / (attack.Shots - 1) - 0.5f;
                    dir = Quaternion.Euler(0f, t * attack.SpreadDegrees, 0f) * dir;
                }
                Vector3 origin = transform.position + Vector3.up * 1.1f + transform.forward * 0.6f;
                _vessel.Root.Projectiles.Fire(_source, attack, origin, dir);
                _shotsFired++;
            }
        }

        /// <summary>Expanding ring: a target is hit once when the damage band sweeps over it.</summary>
        private void ResolveRing()
        {
            AttackDefinition attack = _current;
            float activeElapsed = Mathf.Clamp(_timeline.Elapsed - attack.Startup, 0f, attack.Active);
            float radius = attack.Active > 0f ? attack.Range * (activeElapsed / attack.Active) : attack.Range;
            int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up, radius + attack.RingBand, _overlap, _hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!_overlap[i].TryGetComponent(out VesselRuntime target) || target == _vessel || _hitThisSwing.Contains(target)) continue;
                Vector3 to = target.transform.position - transform.position;
                to.y = 0f;
                float d = to.magnitude;
                if (Mathf.Abs(d - radius) > attack.RingBand) continue;
                Vector3 dir = d > 0.01f ? to / d : transform.forward;
                HitOutcome outcome = _vessel.Root.Damage.Resolve(_source, target, attack, target.transform.position + Vector3.up, dir, isProjectile: false);
                if (outcome != HitOutcome.Ignored) _hitThisSwing.Add(target);
            }
        }

        private void ResolveHits(bool multiHit)
        {
            if (multiHit)
            {
                float activeElapsed = _timeline.Elapsed - _current.Startup;
                if (activeElapsed < _nextMultiHitTime) return;
                _nextMultiHitTime = activeElapsed + _current.MultiHitInterval;
            }

            AttackDefinition attack = _current;
            Vector3 origin = transform.position + Vector3.up;
            Vector3 forward = transform.forward;
            float halfArcCos = Mathf.Cos(attack.ArcDegrees * 0.5f * Mathf.Deg2Rad);

            int count = Physics.OverlapSphereNonAlloc(origin, attack.Range + 0.5f, _overlap, _hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!_overlap[i].TryGetComponent(out VesselRuntime target)) continue;
                if (target == _vessel) continue;
                if (multiHit)
                {
                    _multiHits.TryGetValue(target, out int n);
                    if (n >= attack.MaxHitsPerTarget) continue;
                }
                else if (_hitThisSwing.Contains(target)) continue;

                Vector3 to = target.transform.position - transform.position;
                to.y = 0f;
                float distance = to.magnitude;
                if (distance > attack.Range + 0.4f) continue;
                if (distance > 0.3f && Vector3.Dot(forward, to / distance) < halfArcCos) continue;

                Vector3 dir = distance > 0.01f ? to / distance : forward;
                Vector3 point = target.transform.position + Vector3.up - dir * 0.4f;
                HitOutcome outcome = _vessel.Root.Damage.Resolve(_source, target, attack, point, dir, isProjectile: false);
                if (outcome == HitOutcome.Ignored) continue;
                if (multiHit) { _multiHits.TryGetValue(target, out int n); _multiHits[target] = n + 1; }
                else _hitThisSwing.Add(target);
            }
        }
    }
}
