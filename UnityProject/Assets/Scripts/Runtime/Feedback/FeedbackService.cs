using BorrowedSouls.Bootstrap;
using BorrowedSouls.Combat;
using BorrowedSouls.Data;
using BorrowedSouls.Soul;
using Unity.Cinemachine;
using UnityEngine;

namespace BorrowedSouls.Feedback
{
    /// <summary>
    /// Turns confirmed combat events into feel: hit stop (via TimeService), directional camera impulse,
    /// target flash + punch, spark aimed back at the attacker. Gameplay code never calls
    /// any of this directly.
    /// </summary>
    public sealed class FeedbackService : MonoBehaviour
    {
        [SerializeField] private GameRoot _root;
        [SerializeField] private CinemachineImpulseSource _impulse;
        [SerializeField] private ParticlePool _hitSparks;
        [SerializeField] private ParticlePool _evadeSparks;
        [SerializeField] private ParticlePool _fireBursts;
        [Tooltip("Size of the fire burst when a fire projectile strikes something.")]
        [SerializeField, Min(0f)] private float _fireBurstScale = 1f;
        [Tooltip("Size of the fire burst when a fire projectile runs out of range.")]
        [SerializeField, Min(0f)] private float _fireFizzleScale = 0.45f;
        [Tooltip("Size of the sparks when an arrow strikes a wall. Bodies have their own hit sparks.")]
        [SerializeField, Min(0f)] private float _arrowWallSparkScale = 0.6f;
        [Tooltip("Size of the burst at the feet when a ring attack goes out.")]
        [SerializeField, Min(0f)] private float _ringBurstScale = 3f;

        private void Start()
        {
            // Shake keeps animating during hit stop instead of freezing at a random offset.
            CinemachineImpulseManager.Instance.IgnoreTimeScale = true;
        }

        private void OnEnable()
        {
            _root.Events.Subscribe<HitConfirmed>(OnHitConfirmed);
            _root.Events.Subscribe<HitEvaded>(OnHitEvaded);
            _root.Events.Subscribe<ProjectileEnded>(OnProjectileEnded);
            _root.Events.Subscribe<EmberCollected>(OnEmberCollected);
            _root.Events.Subscribe<AttackReleased>(OnAttackReleased);
        }

        private void OnDisable()
        {
            _root.Events.Unsubscribe<HitConfirmed>(OnHitConfirmed);
            _root.Events.Unsubscribe<HitEvaded>(OnHitEvaded);
            _root.Events.Unsubscribe<ProjectileEnded>(OnProjectileEnded);
            _root.Events.Unsubscribe<EmberCollected>(OnEmberCollected);
            _root.Events.Unsubscribe<AttackReleased>(OnAttackReleased);
        }

        /// <summary>The ring is the one attack felt the moment it goes out, whether it reaches the player or not.</summary>
        private void OnAttackReleased(AttackReleased e)
        {
            if (e.Attack.Delivery != AttackDelivery.Ring) return;
            FeedbackProfile p = e.Attack.Feedback;
            Vector3 feet = e.Attacker.transform.position;
            if (_hitSparks != null) _hitSparks.Play(feet + Vector3.up * 0.2f, Vector3.up, _ringBurstScale);
            if (p == null) return;
            float shake = p.ImpulseForce * _root.CombatSettings.CameraShakeScale * _root.Options.ShakeScale;
            if (_impulse != null && shake > 0f) _impulse.GenerateImpulseAtPositionWithVelocity(feet, Vector3.down * shake);
        }

        private void OnHitConfirmed(HitConfirmed e)
        {
            FeedbackProfile p = e.Attack.Feedback;
            if (p == null) return;
            CombatSettings settings = _root.CombatSettings;

            float stop = p.HitStop + (e.IsKill ? settings.KillHitStopBonus : 0f);
            _root.Time.Stack.RequestHitStop(stop);

            var options = _root.Options;
            float shake = p.ImpulseForce * settings.CameraShakeScale * options.ShakeScale * (e.IsKill ? 1.4f : 1f);
            if (_impulse != null && shake > 0f) _impulse.GenerateImpulseAtPositionWithVelocity(e.Point, e.Direction * shake);

            if (e.Target.TryGetComponent(out VesselView view))
            {
                view.Flash(settings.ReduceFlashes || options.ReduceFlashes ? p.FlashDuration * 0.5f : p.FlashDuration);
                view.Punch(p.PunchScale);
            }

            if (_hitSparks != null) _hitSparks.Play(e.Point, -e.Direction, p.SparkScale);
        }

        private void OnProjectileEnded(ProjectileEnded e)
        {
            ProjectileLook look = e.Attack.ProjectileLook;
            if (look == ProjectileLook.FireBolt || look == ProjectileLook.FlameLance)
            {
                if (_fireBursts != null) _fireBursts.Play(e.Point, -e.Direction, e.Struck ? _fireBurstScale : _fireFizzleScale);
            }
            else if (e.Struck && !e.HitVessel && _hitSparks != null)
            {
                _hitSparks.Play(e.Point, -e.Direction, _arrowWallSparkScale);
            }
        }

        private void OnEmberCollected(EmberCollected e)
        {
            if (_evadeSparks != null) _evadeSparks.Play(e.Position, Vector3.up, 0.45f);
        }

        private void OnHitEvaded(HitEvaded e)
        {
            if (_evadeSparks != null) _evadeSparks.Play(e.Point, Vector3.up, 0.8f);
        }
    }
}
