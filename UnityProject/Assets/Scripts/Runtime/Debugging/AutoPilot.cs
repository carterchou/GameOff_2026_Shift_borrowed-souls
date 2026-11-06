using BorrowedSouls.Bootstrap;
using BorrowedSouls.Core.Combat;
using BorrowedSouls.Core.Soul;
using BorrowedSouls.Data;
using BorrowedSouls.Run;
using BorrowedSouls.Soul;
using BorrowedSouls.UI;
using BorrowedSouls.Vessels;
using UnityEngine;

namespace BorrowedSouls.Debugging
{
    /// <summary>
    /// Development bot for automated runs and balance
    /// data. Drives the body like a player (approach to weapon range, attack, dodge enemy wind-ups) and the soul
    /// (leave a weak or countered body, fly to a legal vessel — preferring the class that counters the nearest
    /// enemy — and possess it). Auto-picks upgrades. Toggle with F2 or <see cref="SetActive"/>. Never in shipping flow.
    /// </summary>
    public sealed class AutoPilot : MonoBehaviour, IVesselDriver, ISoulInput
    {
        [SerializeField] private GameRoot _root;
        [SerializeField] private PlayerSoulController _soul;
        [SerializeField] private UpgradeSelectionPanel _upgrades;
        [SerializeField, Range(0f, 1f)] private float _leaveBodyHpRatio = 0.35f;
        [SerializeField, Min(0f)] private float _dangerRadius = 3.2f;

        private bool _active;
        private bool _holdShift;
        private bool _pressShift;
        private float _holdTimer;
        private VesselRuntime _soulGoal;
        private float _strafeSign = 1f;
        private float _nextStrafeFlip;
        private VesselRuntime _justLeft;
        private float _justLeftUntil;
        private float _settleUntil;
        private VesselRuntime _lastBody;
        private bool _dodgeRoll;
        private bool _ignoreCorpses;
        private float _dodgeRollUntil;

        public bool Active => _active;

        public void SetActive(bool on)
        {
            _active = on;
            _soul.SetControlSource(on ? this : null, on ? this : null);
            if (_upgrades != null) _upgrades.AutoPick = on;
            Debug.Log($"[AutoPilot] {(on ? "ON" : "OFF")}");
        }

        /// <summary>Editor-only switch so automated sessions start with the bot already driving.</summary>
        public const string SessionKey = "BorrowedSouls.AutoPilotOnPlay";

        /// <summary>Command-line flag for development builds: skip the title, bot plays one run, then quit.</summary>
        public const string LaunchArg = "-autopilot";

        /// <summary>
        /// True when this launch should start with the bot driving: the editor <see cref="SessionKey"/>, or
        /// <see cref="LaunchArg"/> on a development build (headless balance runs outside the editor). Always
        /// false in release builds.
        /// </summary>
        public static bool RequestedAtLaunch
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.SessionState.GetBool(SessionKey, false);
#elif DEVELOPMENT_BUILD
                return System.Array.IndexOf(System.Environment.GetCommandLineArgs(), LaunchArg) >= 0;
#else
                return false;
#endif
            }
        }

        private void OnEnable() => _root.Events.Subscribe<RunEnded>(OnRunEnded);
        private void OnDisable() => _root.Events.Unsubscribe<RunEnded>(OnRunEnded);

        private void Start()
        {
            if (RequestedAtLaunch) SetActive(true);
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
            if (RequestedAtLaunch) DevWatchdog.Begin(gameObject);
            FrameCapture.StartIfRequested(gameObject, _root); // "-capture DIR": trailer frames
            // "-botnocorpse": the bot never borrows corpses, so they stay on the floor (tests elites smashing them).
            _ignoreCorpses = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-botnocorpse") >= 0;
            // Headless runs are uncapped; "-fps N" pins the frame rate to compare against real play.
            string[] args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-fps");
            if (i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out int fps)) Application.targetFrameRate = fps;
#endif
        }

        private static void OnRunEnded(RunEnded e)
        {
            // Player-build batch runs exit once the result is logged (Quit takes effect at end of frame, after
            // RunMetrics has written its [RUN] line); the editor keeps the results screen.
            if (!Application.isEditor && RequestedAtLaunch) Application.Quit();
        }

        private void Update()
        {
            DevWatchdog.Mark("autopilot");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Development tool only: release builds must never let players hand control to the bot.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.f2Key.wasPressedThisFrame) SetActive(!_active);
#endif
            if (!_active) return;

            _pressShift = false;
            if (_soul.IsFree) UpdateSoulPlan();
            else if (_holdShift)
            {
                _holdTimer -= Time.unscaledDeltaTime;
                if (_holdTimer <= 0f && !_soul.IsFree) _holdShift = false; // exit was refused (e.g. stunned)
            }
        }

        // ---- body ---------------------------------------------------------------------------------------
        public void BuildIntent(VesselRuntime vessel, ref VesselIntent intent)
        {
            VesselRuntime target = NearestHostile(vessel.transform.position, out float distance);
            if (target == null) return;

            Vector3 to = target.transform.position - vessel.transform.position;
            to.y = 0f;
            Vector3 aim = distance > 0.01f ? to / distance : vessel.transform.forward;
            intent.Aim = aim;

            WeaponDefinition weapon = vessel.Definition.Weapon;
            AttackDefinition weak = weapon.GetWeak(0);
            float range = weak.Delivery == AttackDelivery.Projectile ? Mathf.Min(10f, weak.ProjectileMaxDistance * 0.7f) : weak.Range * 0.85f;
            bool ranged = weak.Delivery == AttackDelivery.Projectile;

            if (Time.time > _nextStrafeFlip)
            {
                _strafeSign = -_strafeSign;
                _nextStrafeFlip = Time.time + Random.Range(1.2f, 2.5f);
            }
            Vector3 side = new Vector3(-aim.z, 0f, aim.x) * _strafeSign;
            Vector3 move;
            if (distance > range) move = aim;
            else if (ranged && distance < 4f) move = (-aim + side * 0.5f).normalized;
            else move = side * 0.4f;
            intent.Move = new Vector2(move.x, move.z);

            if (InDanger(vessel) && vessel.Combat.DodgeCooldownLeft <= 0f)
            {
                intent.DodgePressed = true;
                intent.Move = new Vector2(side.x, side.z);
                return;
            }

            if (distance <= range + 0.3f && !vessel.Combat.IsAttacking)
            {
                float r = Random.value;
                if (vessel.Combat.SpecialCooldownLeft <= 0f && r < 0.15f) intent.SpecialPressed = true;
                else if (r < 0.3f) intent.StrongPressed = true;
                else intent.WeakPressed = true;
            }
            intent.WeakHeld = ranged && distance <= range;

            ConsiderLeaving(vessel, target);
        }

        private void ConsiderLeaving(VesselRuntime vessel, VesselRuntime nearestEnemy)
        {
            if (vessel != _lastBody)
            {
                _lastBody = vessel;
                _settleUntil = Time.time + 4f; // fight in a new body for a while before considering another swap
            }
            if (_holdShift || Time.time < _settleUntil) return;
            bool weak = vessel.Hp / vessel.MaxHp.Value < _leaveBodyHpRatio;
            bool countered = CombatRules.Counters(nearestEnemy.Class, vessel.Class);
            if (!weak && !countered) return;
            VesselRuntime goal = FindSoulGoal(vessel.transform.position, nearestEnemy, 7f, vessel);
            if (goal == null) return;
            // Being countered is only worth a swap if the new body is a different class that counters back.
            if (!weak && (goal.Class == vessel.Class || !CombatRules.Counters(goal.Class, nearestEnemy.Class))) return;
            _soulGoal = goal; // otherwise the previous trip's goal (= this body) is kept and re-possessed at once
            _justLeft = vessel;
            _justLeftUntil = Time.time + 3f;
            _holdShift = true;
            _holdTimer = _root.SoulSettings.SoulExitHold + 0.3f;
        }

        /// <summary>Only a real attack start-up that can reach us and faces us is worth a dodge (dodging cancels our own swing).</summary>
        private bool InDanger(VesselRuntime self)
        {
            if (self.Combat.Phase == AttackPhase.Active) return false;
            var vessels = _root.Vessels;
            for (int i = 0; i < vessels.Count; i++)
            {
                VesselRuntime v = vessels[i];
                if (v == self || v.Team != Team.Enemy || v.Combat.Phase != AttackPhase.Startup || v.Combat.CurrentAttack == null) continue;
                Vector3 to = self.transform.position - v.transform.position;
                to.y = 0f;
                float reach = v.Combat.CurrentAttack.Delivery == AttackDelivery.Projectile ? 12f : v.Combat.CurrentAttack.Range + 0.8f;
                if (to.sqrMagnitude > reach * reach || to.sqrMagnitude > _dangerRadius * _dangerRadius * 4f) continue;
                if (Vector3.Dot(v.transform.forward, to.normalized) < 0.6f) continue;
                // One roll per wind-up window (not per frame, which made the 60% effectively 100% and had the
                // bot cancel its own swings on every enemy start-up).
                if (Time.time >= _dodgeRollUntil)
                {
                    _dodgeRoll = Random.value < 0.6f;
                    _dodgeRollUntil = Time.time + 0.5f;
                }
                return _dodgeRoll;
            }
            return false;
        }

        // ---- soul ---------------------------------------------------------------------------------------
        private void UpdateSoulPlan()
        {
            VesselRuntime nearestEnemy = NearestHostile(_soul.SoulPosition, out _);
            if (_soulGoal == null || _soul.Service.CheckTarget(_soul.Soul, _soulGoal.State) != PossessionCheck.Ok)
            {
                _soulGoal = FindSoulGoal(_soul.SoulPosition, nearestEnemy, 99f, Time.time < _justLeftUntil ? _justLeft : null);
                if (_soulGoal == null) _soulGoal = FindSoulGoal(_soul.SoulPosition, nearestEnemy, 99f, null); // last resort: go back
            }
            if (_soulGoal == null)
            {
                _holdShift = false; // nothing legal yet: drift and wait
                return;
            }

            float d = Planar(_soulGoal.transform.position - _soul.SoulPosition).magnitude;
            bool inRange = d <= _soul.Service.InteractRange(_soul.Soul) * 0.8f;
            if (inRange)
            {
                if (_holdShift) _holdShift = false; // releasing possesses the selected target
                else _pressShift = true;
            }
            else
            {
                _holdShift = true; // keep holding while travelling so the release confirms the target
            }
        }

        private VesselRuntime FindSoulGoal(Vector3 from, VesselRuntime nearestEnemy, float maxDistance, VesselRuntime exclude)
        {
            VesselRuntime best = null;
            float bestScore = float.MaxValue;
            var vessels = _root.Vessels;
            for (int i = 0; i < vessels.Count; i++)
            {
                VesselRuntime v = vessels[i];
                if (v == exclude || _soul.Service.CheckTarget(_soul.Soul, v.State) != PossessionCheck.Ok) continue;
                if (_ignoreCorpses && v.State.IsFallenCorpse) continue;
                float d = Planar(v.transform.position - from).magnitude;
                if (d > maxDistance) continue;
                float score = d;
                if (nearestEnemy != null && CombatRules.Counters(v.Class, nearestEnemy.Class)) score -= 4f;
                if (v.State.IsFallenCorpse && v.State.CorpseTimeLeft < 2f) score += 3f; // about to dissolve
                if (score < bestScore)
                {
                    bestScore = score;
                    best = v;
                }
            }
            return best;
        }

        public Vector2 ReadMoveWorld()
        {
            if (_soulGoal == null) return Vector2.zero;
            Vector3 d = Planar(_soulGoal.transform.position - _soul.SoulPosition);
            return d.sqrMagnitude > 0.01f ? new Vector2(d.x, d.z).normalized : Vector2.zero;
        }

        public Vector3 ReadAimWorld(Vector3 origin)
        {
            if (_soulGoal == null) return Vector3.forward;
            Vector3 d = Planar(_soulGoal.transform.position - origin);
            return d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward;
        }

        public bool SoulShiftHeld => _holdShift;
        public bool SoulShiftPressed => _pressShift;

        private VesselRuntime NearestHostile(Vector3 from, out float distance)
        {
            VesselRuntime best = null;
            distance = float.MaxValue;
            var vessels = _root.Vessels;
            for (int i = 0; i < vessels.Count; i++)
            {
                VesselRuntime v = vessels[i];
                if (v.Team != Team.Enemy || v.IsDead) continue;
                float d = Planar(v.transform.position - from).magnitude;
                if (d < distance)
                {
                    distance = d;
                    best = v;
                }
            }
            return best;
        }

        private static Vector3 Planar(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
