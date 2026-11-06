using System.Collections.Generic;
using BorrowedSouls.Bootstrap;
using BorrowedSouls.Data;
using BorrowedSouls.Vessels;
using UnityEngine;

namespace BorrowedSouls.Combat
{
    /// <summary>
    /// A real flying projectile. Each frame it sphere-casts along its
    /// path — no transform.Translate tunnelling — and stops on walls/pillars (COMBAT-06).
    /// The attack source is a snapshot taken at launch (team/class/damage multiplier).
    /// </summary>
    public sealed class Projectile : MonoBehaviour
    {
        [Tooltip("Same order as ProjectileLook.")]
        [SerializeField] private ProjectileLookView[] _looks = new ProjectileLookView[0];

        private readonly RaycastHit[] _hits = new RaycastHit[8];
        private readonly HashSet<VesselRuntime> _alreadyHit = new HashSet<VesselRuntime>();
        private ProjectileLookView _look;
        private float _lingerLeft;
        private GameRoot _root;
        private ProjectilePool _pool;
        private AttackSource _source;
        private AttackDefinition _attack;
        private Vector3 _direction;
        private float _traveled;
        private int _pierceLeft;
        private LayerMask _mask;
        private bool _live;

        /// <summary>The look on show, for tests and tools.</summary>
        public ProjectileLookView Look => _look;

        public void Launch(GameRoot root, ProjectilePool pool, in AttackSource source, AttackDefinition attack, Vector3 origin, Vector3 direction, LayerMask mask)
        {
            _root = root;
            _pool = pool;
            _source = source;
            _attack = attack;
            _direction = direction.normalized;
            _traveled = 0f;
            _pierceLeft = attack.Pierce;
            _mask = mask;
            _alreadyHit.Clear();
            transform.SetPositionAndRotation(origin, Quaternion.LookRotation(_direction));
            _look = _looks[(int)attack.ProjectileLook];
            foreach (ProjectileLookView look in _looks)
            {
                if (look != _look) look.gameObject.SetActive(false);
            }
            _look.Show();
            _lingerLeft = 0f;
            _live = true;
        }

        private void Update()
        {
            if (_root == null) return; // pooled and never launched
            float dt = _root.Time.WorldDelta;
            if (dt <= 0f) return;
            if (!_live)
            {
                _lingerLeft -= dt;
                if (_lingerLeft <= 0f) _pool.Release(this);
                return;
            }
            BorrowedSouls.Debugging.DevWatchdog.Mark("projectile", GetInstanceID());

            float step = _attack.ProjectileSpeed * dt;
            Vector3 origin = transform.position;
            int count = Physics.SphereCastNonAlloc(origin, _attack.ProjectileRadius, _direction, _hits, step, _mask, QueryTriggerInteraction.Ignore);
            SortByDistance(count);

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.collider.TryGetComponent(out VesselRuntime vessel))
                {
                    if (vessel == _source.Vessel || _alreadyHit.Contains(vessel)) continue;
                    Vector3 point = hit.distance > 0f ? hit.point : origin;
                    HitOutcome outcome = _root.Damage.Resolve(_source, vessel, _attack, point, _direction, isProjectile: true);
                    if (outcome == HitOutcome.Ignored) continue; // friendly or empty shell: fly through
                    _alreadyHit.Add(vessel);
                    if (outcome == HitOutcome.Blocked || --_pierceLeft < 0)
                    {
                        End(point, struck: true, hitVessel: true);
                        return;
                    }
                }
                else
                {
                    End(hit.distance > 0f ? hit.point : origin, struck: true, hitVessel: false); // walls and pillars stop projectiles
                    return;
                }
            }

            transform.position = origin + _direction * step;
            _traveled += step;
            if (_traveled >= _attack.ProjectileMaxDistance) End(transform.position, struck: false, hitVessel: false);
        }

        private void End(Vector3 at, bool struck, bool hitVessel)
        {
            _live = false;
            transform.position = at;
            _root.Events.Publish(new ProjectileEnded { Attack = _attack, Point = at, Direction = _direction, Struck = struck, HitVessel = hitVessel });
            _look.Linger();
            _lingerLeft = _look.LingerSeconds;
        }

        private void SortByDistance(int count)
        {
            for (int i = 1; i < count; i++)
            {
                RaycastHit key = _hits[i];
                int j = i - 1;
                while (j >= 0 && _hits[j].distance > key.distance)
                {
                    _hits[j + 1] = _hits[j];
                    j--;
                }
                _hits[j + 1] = key;
            }
        }
    }
}
