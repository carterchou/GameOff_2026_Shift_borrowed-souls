using System.Collections.Generic;
using BorrowedSouls.Bootstrap;
using BorrowedSouls.Combat;
using BorrowedSouls.Core.Soul;
using BorrowedSouls.Data;
using UnityEngine;
using UnityEngine.Pool;

namespace BorrowedSouls.Soul
{
    /// <summary>
    /// Spawns soul embers when enemies are killed, drives them in one Update (no per-ember Update/FindTag
    /// lookups), banks upgrades in the soul's <see cref="EmberWallet"/>, extends the soul
    /// timer while flying free, and can pull every ember in at wave end.
    /// </summary>
    public sealed class EmberSystem : MonoBehaviour
    {
        [SerializeField] private GameRoot _root;
        [SerializeField] private PlayerSoulController _soul;
        [SerializeField] private EmberSettings _settings;
        [SerializeField] private SoulEmber _prefab;
        [SerializeField, Min(1)] private int _prewarm = 32;

        private readonly List<SoulEmber> _live = new List<SoulEmber>(64);
        private ObjectPool<SoulEmber> _pool;
        private EmberWallet _wallet;
        private float _lastPickupTime = -99f;
        private int _combo;

        public EmberWallet Wallet => _wallet;
        public int LiveCount => _live.Count;

        private void Awake()
        {
            _wallet = new EmberWallet(_settings.UpgradeBaseCost, _settings.UpgradeCostGrowth);
            _wallet.Grant(_root.Bonuses.BankedUpgrades);
            _pool = new ObjectPool<SoulEmber>(
                () => Instantiate(_prefab, transform),
                e => e.gameObject.SetActive(true),
                e => e.gameObject.SetActive(false),
                e => Destroy(e.gameObject),
                collectionCheck: true, defaultCapacity: _prewarm, maxSize: 256);
            StartCoroutine(PoolWarmup.Fill(this, _pool, _prewarm));
        }

        private void OnEnable()
        {
            _root.Events.Subscribe<VesselDied>(OnVesselDied);
            _root.Events.Subscribe<BossPhaseChanged>(OnBossPhase);
        }

        private void OnDisable()
        {
            _root.Events.Unsubscribe<VesselDied>(OnVesselDied);
            _root.Events.Unsubscribe<BossPhaseChanged>(OnBossPhase);
        }

        public void Spawn(Vector3 position, int count)
        {
            for (int i = 0; i < count; i++)
            {
                SoulEmber e = _pool.Get();
                e.Launch(this, _settings, position + Vector3.up);
                _live.Add(e);
            }
        }

        /// <summary>Wave end: every ember on the field flies to the player before upgrades open.</summary>
        public void PullAll()
        {
            for (int i = 0; i < _live.Count; i++) _live[i].ForcePull();
        }

        public void Collect(SoulEmber ember)
        {
            int index = _live.IndexOf(ember);
            if (index < 0) return; // already collected this frame
            _live.RemoveAt(index);
            Vector3 position = ember.transform.position;
            _pool.Release(ember);

            float now = Time.unscaledTime;
            _combo = now - _lastPickupTime <= _settings.ComboWindow ? Mathf.Min(_combo + 1, _settings.ComboMax) : 0;
            _lastPickupTime = now;

            int banked = _wallet.Add(1);
            float timeBonus = _soul.IsFree ? _soul.Service.AddEmberTime(_soul.Soul, 1) : 0f;
            if (_root.PlayerVessel != null) BodyRecovery.OnEmberCollected(_root.PlayerVessel.State, _root.SoulSettings.EmberHealHp);
            _root.Events.Publish(new EmberCollected
            {
                Combo = _combo,
                Banked = banked,
                TotalBanked = _wallet.BankedUpgrades,
                Fill01 = _wallet.Fill01,
                SoulTimeBonus = timeBonus,
                Position = position
            });
        }

        private void OnBossPhase(BossPhaseChanged e)
        {
            if (e.Boss != null) Spawn(e.Boss.transform.position, _settings.DropBossPhase); // boss phase 10 embers
        }

        private void OnVesselDied(VesselDied e)
        {
            // Only enemies dying under their own soul drop embers; a possessed host or empty shell does not.
            if (e.Vessel == null) return; // DOT kills (Killer == null) still drop embers
            if (e.Vessel.State.Owner != VesselOwner.Empty && e.Vessel.State.Owner != VesselOwner.Broken) return;
            if (!e.Vessel.State.IsFallenCorpse) return; // player-held body or the boss (boss drops on phase changes)
            int count = e.Vessel.State.Kind == VesselKind.Elite ? _settings.DropElite : _settings.DropNormal;
            Spawn(e.Vessel.transform.position, count);
        }

        private void Update()
        {
            BorrowedSouls.Debugging.DevWatchdog.Mark("embers");
            float dt = _root.Time.WorldDelta;
            if (dt <= 0f || _live.Count == 0) return;

            bool hasCollector;
            Vector3 collector;
            float magnet;
            if (_root.PlayerVessel != null)
            {
                hasCollector = true;
                collector = _root.PlayerVessel.transform.position + Vector3.up;
                magnet = _settings.MagnetRangeHoused * (1f + _root.Bonuses.EmberMagnetShare);
            }
            else if (_soul.IsFree)
            {
                hasCollector = true;
                collector = _soul.SoulPosition;
                magnet = _settings.MagnetRangeSoul * (1f + _root.Bonuses.EmberMagnetShare);
            }
            else
            {
                hasCollector = false;
                collector = default;
                magnet = 0f;
            }

            for (int i = _live.Count - 1; i >= 0; i--)
            {
                if (i >= _live.Count) continue; // a collect earlier in this loop shrank the list
                _live[i].Tick(dt, collector, magnet, hasCollector);
            }
        }
    }
}
