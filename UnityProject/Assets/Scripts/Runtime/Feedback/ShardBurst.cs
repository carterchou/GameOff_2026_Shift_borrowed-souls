using System.Collections.Generic;
using BorrowedSouls.Bootstrap;
using BorrowedSouls.Combat;
using BorrowedSouls.Soul;
using BorrowedSouls.Vessels;
using UnityEngine;
using UnityEngine.Pool;

namespace BorrowedSouls.Feedback
{
    /// <summary>
    /// Directional debris: pooled rigidbody cubes thrown along the last hit
    /// direction with an upward spread, held for 1.5 s then shrunk over 0.4 s. A kill throws a small burst; a
    /// vessel that becomes Broken (host destroyed, corpse dissolved) shatters with a large burst (
    /// 容器明確碎裂). Angular velocity is set in rad/s. Real-time lifetimes.
    /// </summary>
    public sealed class ShardBurst : MonoBehaviour
    {
        private struct Live
        {
            public Rigidbody Body;
            public float Born;
            public Vector3 Scale;
        }

        [SerializeField] private GameRoot _root;
        [SerializeField] private Rigidbody _prefab;
        [SerializeField, Min(1)] private int _prewarm = 48;
        [SerializeField, Min(0)] private int _killShards = 6;
        [SerializeField, Min(0)] private int _breakShards = 14;
        [SerializeField] private Vector2 _speed = new Vector2(4f, 9f);
        [SerializeField, Min(0.1f)] private float _hold = 1.5f;
        [SerializeField, Min(0.05f)] private float _shrink = 0.4f;

        private readonly List<Live> _live = new List<Live>(64);
        private readonly Dictionary<VesselRuntime, bool> _wasBroken = new Dictionary<VesselRuntime, bool>();
        private ObjectPool<Rigidbody> _pool;
        private MaterialPropertyBlock _block;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _pool = new ObjectPool<Rigidbody>(
                () => Instantiate(_prefab, transform),
                b => b.gameObject.SetActive(true),
                b => { b.linearVelocity = Vector3.zero; b.angularVelocity = Vector3.zero; b.gameObject.SetActive(false); },
                b => Destroy(b.gameObject),
                collectionCheck: true, defaultCapacity: _prewarm, maxSize: 160);
            StartCoroutine(PoolWarmup.Fill(this, _pool, _prewarm));
        }

        private void OnEnable() => _root.Events.Subscribe<VesselDied>(OnVesselDied);
        private void OnDisable() => _root.Events.Unsubscribe<VesselDied>(OnVesselDied);

        private void OnVesselDied(VesselDied e)
        {
            if (e.Vessel == null) return;
            Burst(e.Vessel.transform.position + Vector3.up, e.LastHitDirection, _killShards, e.Vessel.Definition.BodyColor);
        }

        public void Burst(Vector3 origin, Vector3 direction, int count, Color color)
        {
            Vector3 dir = new Vector3(direction.x, 0f, direction.z);
            for (int i = 0; i < count; i++)
            {
                Rigidbody b = _pool.Get();
                Transform t = b.transform;
                t.SetPositionAndRotation(origin + Random.insideUnitSphere * 0.3f, Random.rotation);
                Vector3 v = (dir + Random.insideUnitSphere * 0.6f);
                v.y = Mathf.Abs(v.y) * 0.8f + 0.5f;
                b.linearVelocity = v.normalized * Random.Range(_speed.x, _speed.y);
                b.angularVelocity = Random.insideUnitSphere * Random.Range(4f, 10f); // rad/s
                if (b.TryGetComponent(out Renderer r))
                {
                    _block.SetColor(BaseColorId, color);
                    r.SetPropertyBlock(_block);
                }
                _live.Add(new Live { Body = b, Born = Time.unscaledTime, Scale = t.localScale });
            }
        }

        private void Update()
        {
            BorrowedSouls.Debugging.DevWatchdog.Mark("shards");
            // Large shatter when a vessel becomes Broken.
            var vessels = _root.Vessels;
            for (int i = 0; i < vessels.Count; i++)
            {
                VesselRuntime v = vessels[i];
                bool broken = v.State.IsBroken;
                _wasBroken.TryGetValue(v, out bool was);
                if (broken && !was) Burst(v.transform.position + Vector3.up * 0.6f, Vector3.zero, _breakShards, v.Definition.BodyColor * 0.6f);
                _wasBroken[v] = broken;
            }

            float now = Time.unscaledTime;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Live s = _live[i];
                float age = now - s.Born;
                if (age < _hold) continue;
                float k = 1f - Mathf.Clamp01((age - _hold) / _shrink);
                s.Body.transform.localScale = s.Scale * k;
                if (k > 0f) continue;
                s.Body.transform.localScale = s.Scale;
                _pool.Release(s.Body);
                _live.RemoveAt(i);
            }
        }
    }
}
