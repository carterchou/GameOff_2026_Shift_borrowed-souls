using BorrowedSouls.Bootstrap;
using UnityEngine;
using UnityEngine.Pool;

namespace BorrowedSouls.Feedback
{
    /// <summary>
    /// Typed pool for one-shot particle effects (UnityEngine.Pool). Release is tracked by real time so effects
    /// spawned during hit stop are returned correctly; double release is guarded by ObjectPool's collection check.
    /// </summary>
    public sealed class ParticlePool : MonoBehaviour
    {
        [SerializeField] private ParticleSystem _prefab;
        [SerializeField, Min(1)] private int _prewarm = 12;
        [SerializeField, Min(0.05f)] private float _lifetime = 0.6f;

        private ObjectPool<ParticleSystem> _pool;
        private readonly System.Collections.Generic.List<(ParticleSystem fx, float releaseAt)> _live =
            new System.Collections.Generic.List<(ParticleSystem, float)>(32);

        private void Awake()
        {
            _pool = new ObjectPool<ParticleSystem>(
                () => Instantiate(_prefab, transform),
                fx => fx.gameObject.SetActive(true),
                fx => { fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); fx.gameObject.SetActive(false); },
                fx => Destroy(fx.gameObject),
                collectionCheck: true, defaultCapacity: _prewarm, maxSize: 64);

            StartCoroutine(PoolWarmup.Fill(this, _pool, _prewarm));
        }

        public void Play(Vector3 position, Vector3 forward, float scale)
        {
            ParticleSystem fx = _pool.Get();
            Transform t = fx.transform;
            t.SetPositionAndRotation(position, forward.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(forward) : Quaternion.identity);
            t.localScale = Vector3.one * scale;
            fx.Play(true);
            _live.Add((fx, Time.unscaledTime + _lifetime));
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                if (_live[i].releaseAt > now) continue;
                _pool.Release(_live[i].fx);
                _live.RemoveAt(i);
            }
        }
    }
}
