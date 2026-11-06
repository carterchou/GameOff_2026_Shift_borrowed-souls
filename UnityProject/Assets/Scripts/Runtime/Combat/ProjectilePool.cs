using BorrowedSouls.Bootstrap;
using BorrowedSouls.Data;
using UnityEngine;
using UnityEngine.Pool;

namespace BorrowedSouls.Combat
{
    /// <summary>Typed projectile pool (UnityEngine.Pool, collection check guards double release).</summary>
    public sealed class ProjectilePool : MonoBehaviour
    {
        [SerializeField] private GameRoot _root;
        [SerializeField] private Projectile _prefab;
        [Tooltip("Layers projectiles collide with: vessels and level geometry.")]
        [SerializeField] private LayerMask _hitMask = ~0;
        [SerializeField, Min(1)] private int _prewarm = 24;

        private ObjectPool<Projectile> _pool;

        private void Awake()
        {
            _pool = new ObjectPool<Projectile>(
                () => Instantiate(_prefab, transform),
                p => p.gameObject.SetActive(true),
                p => p.gameObject.SetActive(false),
                p => Destroy(p.gameObject),
                collectionCheck: true, defaultCapacity: _prewarm, maxSize: 128);

            StartCoroutine(PoolWarmup.Fill(this, _pool, _prewarm));
        }

        public void Fire(in AttackSource source, AttackDefinition attack, Vector3 origin, Vector3 direction)
        {
            Projectile p = _pool.Get();
            p.Launch(_root, this, source, attack, origin, direction, _hitMask);
        }

        public void Release(Projectile p)
        {
            _pool.Release(p);
        }
    }
}
