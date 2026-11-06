using UnityEngine;

namespace BorrowedSouls.Vessels
{
    /// <summary>
    /// Planar locomotion on a CharacterController: accelerated walking, facing toward aim, knockback with
    /// exponential decay, and forced velocity for dodges / lunges (collides with walls — no transform teleport,
    ///).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class VesselMotor : MonoBehaviour
    {
        private CharacterController _controller;
        private VesselRuntime _vessel;
        private Vector3 _velocity;
        private Vector3 _knockback;
        private Vector3 _forcedVelocity;
        private float _forcedLeft;
        private float _groundY;

        public Vector3 Velocity => _velocity;
        public Vector3 Facing => transform.forward;
        public bool HasForcedMotion => _forcedLeft > 0f;

        public void Init(VesselRuntime vessel)
        {
            _vessel = vessel;
            _controller = GetComponent<CharacterController>();
            // Move() silently drops steps shorter than minMoveDistance (default 1 mm). Per-frame steps fall below
            // that at high frame rates, under hit stop / slow-mo, or at low speeds, freezing bodies — so never filter.
            _controller.minMoveDistance = 0f;
            _groundY = transform.position.y;
        }

        public void Tick(in VesselIntent intent, float dt, float speedMultiplier, bool canTurn)
        {
            var def = _vessel.Definition;
            Vector3 desired = new Vector3(intent.Move.x, 0f, intent.Move.y);
            if (desired.sqrMagnitude > 1f) desired.Normalize();
            desired *= _vessel.MoveSpeed.Value * speedMultiplier;
            _velocity = Vector3.MoveTowards(_velocity, desired, def.Acceleration * dt);

            Vector3 motion;
            if (_forcedLeft > 0f)
            {
                motion = _forcedVelocity;
                _forcedLeft -= dt;
            }
            else
            {
                motion = _velocity;
            }

            motion += _knockback;
            float damping = _vessel.Root.CombatSettings.KnockbackDamping;
            _knockback *= Mathf.Exp(-damping * dt);
            if (_knockback.sqrMagnitude < 0.0001f) _knockback = Vector3.zero;

            _controller.Move(motion * dt);

            Vector3 p = transform.position;
            if (!Mathf.Approximately(p.y, _groundY))
            {
                p.y = _groundY;
                transform.position = p;
            }

            if (canTurn && intent.Aim.sqrMagnitude > 1e-4f)
            {
                Quaternion target = Quaternion.LookRotation(intent.Aim, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, def.TurnSpeedDegrees * dt);
            }
        }

        /// <summary>Dead bodies stop blocking movement and attacks; revived (possessed) bodies collide again.</summary>
        public void SetCollisionEnabled(bool enabled)
        {
            if (_controller.enabled != enabled) _controller.enabled = enabled;
        }

        public void FaceImmediately(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        public void SetForcedVelocity(Vector3 velocity, float duration)
        {
            _forcedVelocity = velocity;
            _forcedLeft = duration;
            _velocity = Vector3.zero;
        }

        public void ClearForcedVelocity()
        {
            _forcedLeft = 0f;
        }

        public void AddKnockback(Vector3 velocity)
        {
            _knockback += velocity;
        }
    }
}
