using BorrowedSouls.Data;
using BorrowedSouls.Feedback;
using UnityEngine;

namespace BorrowedSouls.Soul
{
    /// <summary>
    /// One soul ember. Pop arc (horizontal ease-out + sin height + decaying spin) → rests → homes in with a
    /// linear speed ramp once the collector is in magnet range; the pop is interrupted as soon as the player
    /// is close. Motion runs on world time, so hit stop and pause freeze it.
    /// </summary>
    public sealed class SoulEmber : MonoBehaviour
    {
        private enum Phase
        {
            Pop,
            Rest,
            Homing
        }

        private EmberSystem _system;
        private EmberSettings _settings;
        private Phase _phase;
        private Vector3 _start;
        private Vector3 _landing;
        private float _height;
        private float _t;
        private float _speed;
        private float _spin;
        private Vector3 _spinAxis;
        private bool _forcedPull;
        private float _yaw;
        private TrailRenderer _trail;

        private const float UprightDegreesPerSecond = 300f;
        private const float RestSpinDegreesPerSecond = 70f;

        private void Awake()
        {
            SoulColorLayer.Apply(gameObject); // embers stay readable during soul vision, since they are often collected while the soul flies
            _trail = GetComponentInChildren<TrailRenderer>();
        }

        public void Launch(EmberSystem system, EmberSettings settings, Vector3 origin)
        {
            _system = system;
            _settings = settings;
            _phase = Phase.Pop;
            _t = 0f;
            _forcedPull = false;
            _start = origin;
            Vector2 r = Random.insideUnitCircle * settings.ScatterRadius;
            _landing = new Vector3(origin.x + r.x * 0.7f, 0.35f, origin.z + r.y * 0.7f);
            _height = Random.Range(settings.PopHeight.x, settings.PopHeight.y);
            _spin = Random.Range(settings.SpinDegrees.x, settings.SpinDegrees.y);
            _spinAxis = Random.onUnitSphere;
            transform.position = origin;
            if (_trail != null) _trail.Clear(); // from the pool, it would streak from where it was last collected
        }

        /// <summary>Wave end: fly to the collector regardless of range.</summary>
        public void ForcePull()
        {
            _forcedPull = true;
            _phase = Phase.Homing;
            _speed = Mathf.Max(_speed, _settings.WaveEndPullSpeed);
        }

        public void Tick(float dt, Vector3 collector, float magnetRange, bool hasCollector)
        {
            if (dt <= 0f) return;
            float distSq = hasCollector ? (collector - transform.position).sqrMagnitude : float.MaxValue;

            if (_phase != Phase.Homing && hasCollector && distSq <= magnetRange * magnetRange)
            {
                _phase = Phase.Homing;
                _speed = _settings.HomingStartSpeed;
            }

            switch (_phase)
            {
                case Phase.Pop:
                {
                    _t += dt / _settings.PopDuration;
                    float t = Mathf.Clamp01(_t);
                    float ease = 1f - (1f - t) * (1f - t);
                    Vector3 p = Vector3.Lerp(_start, _landing, ease);
                    p.y = Mathf.Lerp(_start.y, _landing.y, ease) + Mathf.Sin(t * Mathf.PI) * _height;
                    transform.position = p;
                    transform.Rotate(_spinAxis, _spin * (1f - t) * dt, Space.World);
                    if (t >= 1f) _phase = Phase.Rest;
                    break;
                }
                case Phase.Rest:
                    // It tumbled through the air; on landing it stands up and turns slowly, so the cut catches the eye.
                    _yaw += RestSpinDegreesPerSecond * dt;
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0f, _yaw, 0f), UprightDegreesPerSecond * dt);
                    transform.position = new Vector3(transform.position.x, _landing.y + Mathf.Sin(Time.time * 3f + _spin) * 0.08f, transform.position.z);
                    break;
                case Phase.Homing:
                {
                    if (!hasCollector) return;
                    _speed = Mathf.Min(_speed + _settings.HomingAcceleration * dt, _forcedPull ? _settings.HomingMaxSpeed * 1.5f : _settings.HomingMaxSpeed);
                    transform.position = Vector3.MoveTowards(transform.position, collector, _speed * dt);
                    if ((collector - transform.position).sqrMagnitude <= _settings.CollectDistance * _settings.CollectDistance) _system.Collect(this);
                    break;
                }
            }
        }
    }
}
