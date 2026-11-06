using BorrowedSouls.Combat;
using BorrowedSouls.Data;
using BorrowedSouls.Vessels;
using UnityEngine;

namespace BorrowedSouls.Feedback
{
    /// <summary>
    /// Plays animator states from what the body is doing. Presentation only: it reads combat and motor state, and attack
    /// clips are timed to their AttackDefinition, so hits still come from <see cref="VesselCombat"/>.
    /// Layer 0 is the body: a four-direction locomotion blend, dodge, hit, death. Attacks play on two more layers: a full-body
    /// one for standing still and an upper-body one over the running legs, cross-weighted by how fast the body is moving.
    /// The upper body doesn't follow the running pelvis during an attack, so strikes and shots stay on the aim.
    /// Controllers have no transitions; this is the single place that picks a state.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class VesselAnimationDriver : MonoBehaviour
    {
        private const int FullBodyAttackLayer = 1;
        private const int UpperBodyAttackLayer = 2;
        private const string HipsBone = "Hips";
        private const string SpineBone = "Spine";
        private static readonly int Locomotion = Animator.StringToHash("Locomotion");
        private static readonly int Dodge = Animator.StringToHash("Dodge");
        private static readonly int Hit = Animator.StringToHash("Hit");
        private static readonly int Death = Animator.StringToHash("Death");
        private static readonly int GetUp = Animator.StringToHash("GetUp");
        private static readonly int Summon = Animator.StringToHash("Summon");
        private static readonly int MoveX = Animator.StringToHash("MoveX");
        private static readonly int MoveZ = Animator.StringToHash("MoveZ");
        private static readonly int MoveRate = Animator.StringToHash("MoveRate");

        [SerializeField] private VesselRuntime _vessel;
        [Tooltip("Seconds the movement blend takes to catch up, so direction changes turn rather than snap.")]
        [SerializeField, Min(0f)] private float _moveDamping = 0.1f;
        [Tooltip("Ground speed in metres per second at which the run clips' feet don't slide, for a 1.8 m body.")]
        [SerializeField, Min(0.1f)] private float _nominalRunSpeed = 4.5f;
        [SerializeField, Min(0.01f)] private float _minMoveRate = 0.35f;
        [SerializeField, Min(0.01f)] private float _maxMoveRate = 1.4f;
        [Tooltip("Locomotion playback rate at rest. Idle is as long as a run cycle, so this is what stretches the breath out.")]
        [SerializeField, Min(0.01f)] private float _idleRate = 0.16f;
        [Tooltip("Fraction of full speed at which the steps reach their full rate. Stride shrinks with speed, so the rate shouldn't.")]
        [SerializeField, Range(0.01f, 1f)] private float _fullRateAtSpeed = 0.25f;
        [SerializeField, Min(0f)] private float _fadeSeconds = 0.08f;
        [Tooltip("Slower blend into lying down, so leaving a body doesn't snap.")]
        [SerializeField, Min(0f)] private float _downFadeSeconds = 0.15f;
        [Tooltip("How fast attack layers fade in, in seconds.")]
        [SerializeField, Min(0.01f)] private float _attackFadeSeconds = 0.07f;
        [Tooltip("How fast attack layers fade out, in seconds: slower, so the arms ease back into the run.")]
        [SerializeField, Min(0.01f)] private float _attackFadeOutSeconds = 0.14f;
        [Tooltip("Blend when one attack cuts straight into the next, in seconds.")]
        [SerializeField, Min(0f)] private float _attackChainSeconds = 0.06f;
        [Tooltip("How fast attacks hand the legs between the attack and the run, in seconds.")]
        [SerializeField, Min(0.01f)] private float _legsHandoverSeconds = 0.12f;
        [Tooltip("Degrees the upper body carries on per m/s of speed just lost, so a stop has weight. Starting needs none: the run clip already leans in.")]
        [SerializeField, Min(0f)] private float _leanPerSpeedLost = 2.2f;
        [SerializeField, Min(0f)] private float _maxLeanDegrees = 10f;
        [Tooltip("How long the body takes to catch up with a change of speed, in seconds.")]
        [SerializeField, Min(0.01f)] private float _momentumSeconds = 0.16f;
        [Tooltip("How quickly the lean itself moves, so it never snaps.")]
        [SerializeField, Min(1f)] private float _leanSharpness = 18f;
        [Tooltip("Speeds above this are a teleport or a respawn, not running.")]
        [SerializeField, Min(1f)] private float _maxTrackedSpeed = 30f;
        [SerializeField, Min(0f)] private float _getUpSeconds = 0.72f;
        [SerializeField, Min(0f)] private float _summonSeconds = 1.5f;

        private Animator _animator;
        private int _state;
        private int _attackState;
        private AttackDefinition _attack;
        private float _attackElapsed;
        private float _attackWeight;
        private float _legsRun;
        private float _pelvisCancel;
        private Vector3 _lastPosition;
        private Vector3 _momentum;
        private Vector3 _lean;
        private bool _hasLastPosition;
        private Transform _hips;
        private Transform _spine;
        private Quaternion _hipsRestInverse;
        private float _getUpLeft;
        private float _summonLeft;

        /// <summary>How far the upper body is leaning from momentum right now, in degrees.</summary>
        public float MomentumLean => _lean.magnitude;

        /// <summary>Hash of the body layer's state.</summary>
        public int CurrentState => _state;

        /// <summary>Hash of the attack playing on the attack layers, or 0.</summary>
        public int CurrentAttackState => _attackState;

        public void Bind(Animator animator)
        {
            _animator = animator;
            _state = Locomotion;
            FindPelvisBones();
            _vessel.Root.Events.Subscribe<BossSummonRequested>(OnBossSummon);
        }

        /// <summary>Called before the Animator first evaluates, so the hips are still in their rest pose.</summary>
        private void FindPelvisBones()
        {
            foreach (Transform bone in _animator.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == HipsBone) _hips = bone;
                else if (bone.name == SpineBone) _spine = bone;
            }
            if (_hips != null) _hipsRestInverse = Quaternion.Inverse(Quaternion.Inverse(_animator.transform.rotation) * _hips.rotation);
        }

        private void OnDestroy()
        {
            if (_animator != null) _vessel.Root.Events.Unsubscribe<BossSummonRequested>(OnBossSummon);
        }

        private void OnBossSummon(BossSummonRequested e)
        {
            if (e.Boss == _vessel && _animator.HasState(FullBodyAttackLayer, Summon)) _summonLeft = _summonSeconds;
        }

        private void Update()
        {
            if (_animator == null) return;
            float dt = Time.deltaTime;
            float speed01 = UpdateLocomotion(dt);
            UpdateBodyState(dt);
            UpdateAttackLayers(dt, speed01);
        }

        /// <summary>Feeds the blend tree with velocity in the body's own frame. Returns speed as a 0..1 fraction.</summary>
        private float UpdateLocomotion(float dt)
        {
            Vector3 local = transform.InverseTransformDirection(_vessel.Motor.Velocity);
            float moveSpeed = Mathf.Max(0.1f, _vessel.MoveSpeed.Value);
            float x = Mathf.Clamp(local.x / moveSpeed, -1f, 1f);
            float z = Mathf.Clamp(local.z / moveSpeed, -1f, 1f);
            _animator.SetFloat(MoveX, x, _moveDamping, dt);
            _animator.SetFloat(MoveZ, z, _moveDamping, dt);

            float speed01 = Mathf.Clamp01(new Vector2(_animator.GetFloat(MoveX), _animator.GetFloat(MoveZ)).magnitude);
            // Bigger bodies take longer strides at the same ground speed.
            float rate = moveSpeed / (_nominalRunSpeed * Mathf.Max(0.1f, transform.lossyScale.y));
            float runRate = Mathf.Clamp(rate, _minMoveRate, _maxMoveRate);
            _animator.SetFloat(MoveRate, Mathf.Lerp(_idleRate, runRate, speed01 / _fullRateAtSpeed));
            return speed01;
        }

        private void UpdateBodyState(float dt)
        {
            if (_getUpLeft > 0f) _getUpLeft -= dt;
            int next = PickBodyState();
            if (next == _state) return;

            if (_state == Death && next != Death)
            {
                next = GetUp;
                _getUpLeft = _getUpSeconds;
            }
            float fade = next == Death || next == GetUp ? _downFadeSeconds : _fadeSeconds;
            _state = next;
            _animator.CrossFadeInFixedTime(next, fade, 0, 0f);
        }

        private int PickBodyState()
        {
            if (_vessel.IsDown) return Death;
            if (_getUpLeft > 0f) return GetUp;
            if (_vessel.Combat.IsDodging) return Dodge;
            if (_vessel.IsStaggered) return Hit;
            return Locomotion;
        }

        private void UpdateAttackLayers(float dt, float speed01)
        {
            VesselCombat combat = _vessel.Combat;
            AttackDefinition attack = _vessel.IsDown ? null : combat.CurrentAttack;
            // A held bow re-fires the same attack, which the attack alone can't show.
            bool restarted = attack != null && attack == _attack && combat.Elapsed < _attackElapsed;
            _attack = attack;
            _attackElapsed = combat.Elapsed;

            if (_summonLeft > 0f) _summonLeft -= dt;
            int wanted = 0;
            if (attack != null) wanted = Animator.StringToHash(attack.AnimationState);
            else if (_summonLeft > 0f) wanted = Summon;
            if (wanted != 0 && !_animator.HasState(FullBodyAttackLayer, wanted)) wanted = 0;

            if (wanted != 0 && (wanted != _attackState || restarted))
            {
                // Cutting straight into another attack would otherwise pop to its first pose.
                bool chaining = _attackWeight > 0f && wanted != _attackState;
                StartAttack(wanted, FullBodyAttackLayer, chaining);
                StartAttack(wanted, UpperBodyAttackLayer, chaining);
            }
            _attackState = wanted;

            float fade = wanted != 0 ? _attackFadeSeconds : _attackFadeOutSeconds;
            _attackWeight = Mathf.MoveTowards(_attackWeight, wanted != 0 ? 1f : 0f, dt / fade);
            // Lunges keep their full-body pose: their speed comes from the attack, not from running.
            float legsRun = attack != null && attack.LungeDistance > 0f ? 0f : Mathf.Clamp01(speed01 * 2f);
            _legsRun = Mathf.MoveTowards(_legsRun, legsRun, dt / _legsHandoverSeconds);

            float weight = Mathf.SmoothStep(0f, 1f, _attackWeight);
            _animator.SetLayerWeight(FullBodyAttackLayer, weight * (1f - _legsRun));
            _animator.SetLayerWeight(UpperBodyAttackLayer, weight);
            _pelvisCancel = _state == Locomotion ? weight * _legsRun : 0f;
        }

        private void StartAttack(int state, int layer, bool chaining)
        {
            if (chaining) _animator.CrossFadeInFixedTime(state, _attackChainSeconds, layer, 0f);
            else _animator.Play(state, layer, 0f);
        }

        /// <summary>
        /// The masked attack layer poses the spine relative to the hips, which turn up to 26 degrees on a sideways run. Undoing
        /// that turn after the Animator has run keeps the attack pointing where the body aims.
        /// </summary>
        private void LateUpdate()
        {
            CancelPelvisTurn();
            LeanWithMomentum();
        }

        /// <summary>Works out how much speed the body has just lost and lets the upper body carry on that way, then settle back.</summary>
        private void LeanWithMomentum()
        {
            if (_spine == null) return;
            float dt = _vessel.Root.Time.WorldDelta;
            if (dt <= 0f) return; // hit stop: the pose holds

            Vector3 position = _vessel.transform.position;
            Vector3 velocity = _hasLastPosition ? (position - _lastPosition) / dt : Vector3.zero;
            velocity.y = 0f;
            _lastPosition = position;
            _hasLastPosition = true;
            if (velocity.sqrMagnitude > _maxTrackedSpeed * _maxTrackedSpeed) _momentum = velocity = Vector3.zero;

            _momentum = Vector3.Lerp(_momentum, velocity, 1f - Mathf.Exp(-dt / _momentumSeconds));
            Vector3 lost = _momentum - velocity;
            bool slowing = Vector3.Dot(lost, velocity) >= 0f;
            Vector3 wanted = slowing && !_vessel.IsDown ? Vector3.ClampMagnitude(lost * _leanPerSpeedLost, _maxLeanDegrees) : Vector3.zero;
            _lean = Vector3.Lerp(_lean, wanted, 1f - Mathf.Exp(-_leanSharpness * dt));

            float degrees = _lean.magnitude;
            if (degrees < 0.01f) return;
            _spine.rotation = Quaternion.AngleAxis(degrees, Vector3.Cross(Vector3.up, _lean / degrees)) * _spine.rotation;
        }

        private void CancelPelvisTurn()
        {
            if (_hips == null || _spine == null || _pelvisCancel <= 0f) return;
            Quaternion body = _animator.transform.rotation;
            Quaternion pelvisTurn = Quaternion.Inverse(body) * _hips.rotation * _hipsRestInverse;
            Quaternion cancel = Quaternion.Slerp(Quaternion.identity, Quaternion.Inverse(pelvisTurn), _pelvisCancel);
            _spine.rotation = body * cancel * Quaternion.Inverse(body) * _spine.rotation;
        }
    }
}
