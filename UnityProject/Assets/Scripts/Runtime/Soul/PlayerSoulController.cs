using System.Collections.Generic;
using BorrowedSouls.Bootstrap;
using BorrowedSouls.Core.Soul;
using BorrowedSouls.Core.Time;
using BorrowedSouls.Feedback;
using BorrowedSouls.InputHandling;
using BorrowedSouls.Vessels;
using UnityEngine;

namespace BorrowedSouls.Soul
{
    /// <summary>
    /// Runtime glue for the player soul: hold SoulShift to leave the body, fly through
    /// interior walls, release (or press again) to possess the selected legal vessel. All ownership rules live
    /// in <see cref="PossessionService"/>; this class only reads input, moves the soul avatar, swaps drivers and
    /// publishes events. Runs after vessels so the frame order is: damage → possession input → timers.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class PlayerSoulController : MonoBehaviour
    {
        [SerializeField] private GameRoot _root;
        [SerializeField] private PlayerInputDriver _input;
        [SerializeField] private VesselRuntime _startingVessel;
        [SerializeField] private Transform _soulAvatar;
        [SerializeField] private Transform _arenaCenter;

        private readonly List<VesselState> _states = new List<VesselState>(32);
        private PossessionService _service;
        private PlayerSoul _soul;
        private VesselRuntime _current;
        private VesselRuntime _target;
        private float _holdTime;
        private bool _awaitRelease;
        private TimeRequestHandle _slowMo;
        private ISoulInput _inputOverride;
        private IVesselDriver _driverOverride;

        private ISoulInput SoulInput => _inputOverride ?? _input;
        private IVesselDriver BodyDriver => _driverOverride ?? _input;

        private bool _clockStopped;

        public PlayerSoul Soul => _soul;
        public PossessionService Service => _service;
        public VesselRuntime Target => _target;
        public bool IsFree => _soul != null && _soul.Mode == SoulMode.Free;
        public bool IsExpired => _soul != null && _soul.Mode == SoulMode.Expired;
        public Vector3 SoulPosition => _soulAvatar.position;

        /// <summary>Whether the soul could possess this body, range aside.</summary>
        public bool IsLegalTarget(VesselRuntime vessel) => _service.CheckTarget(_soul, vessel.State) == PossessionCheck.Ok;

        /// <summary>Route soul + body control to an automation source (autopilot); null restores player input.</summary>
        public void SetControlSource(ISoulInput soulInput, IVesselDriver bodyDriver)
        {
            _inputOverride = soulInput;
            _driverOverride = bodyDriver;
            if (_current != null) _current.SetDriver(BodyDriver);
        }

        private void Awake()
        {
            SoulConfig config = _root.SoulSettings.ToConfig();
            config.SoulFreeLifetime += _root.Bonuses.SoulSeconds;
            _service = new PossessionService(config);
            _soul = new PlayerSoul();
            SoulColorLayer.Apply(_soulAvatar.gameObject); // the soul stays blue in the greyed world
            _soulAvatar.gameObject.SetActive(false);
        }

        /// <summary>The run is won: nothing can run out now, not the soul's time and not the bodies lying about.</summary>
        public void StopTheClock() => _clockStopped = true;

        /// <summary>Which body the run starts in, when it isn't the one set in the scene. Only before Start.</summary>
        public void UseStartingVessel(VesselRuntime vessel) => _startingVessel = vessel;

        private void Start()
        {
            _service.AssignInitialVessel(_soul, _startingVessel.State);
            Attach(_startingVessel);
        }

        private void Update()
        {
            BorrowedSouls.Debugging.DevWatchdog.Mark("soul controller");
            if (_soul.Mode == SoulMode.Expired) return;

            float realDt = _root.Time.RealDelta;
            float soulDt = _root.Time.GameplayUnscaledDelta;
            if (_root.Time.IsPaused) return;

            if (_soul.Mode == SoulMode.InVessel) UpdateHoused(realDt);
            else UpdateFree(soulDt);

            CollectStates();
            _service.Tick(_soul, _states, _clockStopped ? 0f : soulDt);

            if (_soul.Mode == SoulMode.Expired)
            {
                _soulAvatar.gameObject.SetActive(false);
                ClearHighlights();
                _root.Events.Publish(new SoulExpired { Position = _soulAvatar.position });
            }
        }

        private void UpdateHoused(float realDt)
        {
            // Forced ejection: the body we live in was destroyed this frame.
            if (_current != null && _current.State.IsBroken)
            {
                VesselRuntime broken = _current;
                _service.OnCurrentVesselDestroyed(_soul);
                Detach(broken, forced: true, enemyResumed: false);
                return;
            }

            if (SoulInput.SoulShiftHeld) _holdTime += realDt;
            else _holdTime = 0f;

            if (_holdTime < _root.SoulSettings.SoulExitHold) return;
            _holdTime = 0f;

            VesselRuntime leaving = _current;
            bool conscious = true; // Stun status arrives with the status-effect system; stagger is not unconsciousness.
            if (!_service.ExitVoluntary(_soul, conscious)) return;

            bool resumed = leaving.State.Owner == VesselOwner.EnemyAI;
            Detach(leaving, forced: false, enemyResumed: resumed);
            _awaitRelease = true;
        }

        private void UpdateFree(float soulDt)
        {
            // Movement ignores slow motion (soul clock) and passes through interior walls; the arena edge still holds.
            Vector2 move = SoulInput.ReadMoveWorld();
            Vector3 p = _soulAvatar.position + new Vector3(move.x, 0f, move.y) * (_root.SoulSettings.SoulMoveSpeed * soulDt);
            Vector3 center = _arenaCenter != null ? _arenaCenter.position : Vector3.zero;
            Vector3 offset = new Vector3(p.x - center.x, 0f, p.z - center.z);
            float r = _root.SoulSettings.ArenaRadius;
            if (offset.sqrMagnitude > r * r) offset = offset.normalized * r;
            _soulAvatar.position = new Vector3(center.x + offset.x, _soulAvatar.position.y, center.z + offset.z);

            SelectTarget(SoulInput.ReadAimWorld(_soulAvatar.position));

            bool release = _awaitRelease && !SoulInput.SoulShiftHeld;
            if (release) _awaitRelease = false;
            bool confirm = release || (!_awaitRelease && SoulInput.SoulShiftPressed);
            if (confirm && _target != null) TryPossess(_target);
        }

        private void SelectTarget(Vector3 aim)
        {
            VesselRuntime best = null;
            float bestScore = float.MaxValue;
            float range = _service.InteractRange(_soul);
            var vessels = _root.Vessels;
            for (int i = 0; i < vessels.Count; i++)
            {
                VesselRuntime v = vessels[i];
                bool legal = _service.CheckTarget(_soul, v.State) == PossessionCheck.Ok;
                SetHighlight(v, legal ? SoulHighlight.Legal : (v.State.Kind == VesselKind.Boss ? SoulHighlight.Immune : SoulHighlight.Dimmed));
                if (!legal) continue;

                Vector3 to = v.transform.position - _soulAvatar.position;
                to.y = 0f;
                float d = to.magnitude;
                if (d > range) continue;
                // Prefer bodies in the aim direction, then the closest.
                float align = d > 0.01f ? Vector3.Dot(aim, to / d) : 1f;
                float score = d - align * 1.2f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = v;
                }
            }

            _target = best;
            if (_target != null) SetHighlight(_target, SoulHighlight.Targeted);
        }

        private void TryPossess(VesselRuntime target)
        {
            Vector3 to = target.transform.position - _soulAvatar.position;
            to.y = 0f;
            PossessionResult r = _service.TryPossess(_soul, target.State, to.magnitude);
            if (!r.Success) return;

            ClearHighlights();
            Attach(target);
            _root.Events.Publish(new PossessionSucceeded
            {
                From = null,
                To = target,
                FirstClaim = r.FirstClaim,
                SuppressedEnemySoul = r.SuppressedEnemySoul,
                Healed = r.HealedAmount
            });
        }

        private void Attach(VesselRuntime vessel)
        {
            _current = vessel;
            _target = null;
            _holdTime = 0f;
            _awaitRelease = false;
            vessel.SetDriver(BodyDriver);
            _root.PlayerVessel = vessel;
            _soulAvatar.gameObject.SetActive(false);
            if (_slowMo.IsValid)
            {
                _root.Time.Stack.Pop(_slowMo);
                _slowMo = default;
            }
        }

        private void Detach(VesselRuntime vessel, bool forced, bool enemyResumed)
        {
            // A living host goes back to its own AI with the HP/status it has right now; empty shells stay idle.
            vessel.SetDriver(enemyResumed ? vessel.AiDriver : null);
            _current = null;
            _root.PlayerVessel = null;

            _soulAvatar.position = new Vector3(vessel.transform.position.x, _soulAvatar.position.y, vessel.transform.position.z);
            _soulAvatar.gameObject.SetActive(true);

            var s = _root.SoulSettings;
            if (s.ExitSlowMoDuration > 0f) _slowMo = _root.Time.Stack.PushTimed(TimeRequestKind.SlowMotion, s.ExitSlowMoScale, s.ExitSlowMoDuration);

            _root.Events.Publish(new SoulExited
            {
                PreviousVessel = vessel,
                Forced = forced,
                EnemySoulResumed = enemyResumed,
                Position = _soulAvatar.position
            });
        }

        private void CollectStates()
        {
            _states.Clear();
            var vessels = _root.Vessels;
            for (int i = 0; i < vessels.Count; i++) _states.Add(vessels[i].State);
        }

        private void ClearHighlights()
        {
            var vessels = _root.Vessels;
            for (int i = 0; i < vessels.Count; i++) SetHighlight(vessels[i], SoulHighlight.None);
        }

        private static void SetHighlight(VesselRuntime v, SoulHighlight mode)
        {
            if (v.TryGetComponent(out VesselView view)) view.SetSoulHighlight(mode);
        }
    }
}
