using BorrowedSouls.Localization;
using System.Collections.Generic;
using BorrowedSouls.AI;
using BorrowedSouls.Bootstrap;
using BorrowedSouls.Combat;
using BorrowedSouls.Core.Combat;
using BorrowedSouls.Core.Soul;
using BorrowedSouls.Core.Time;
using BorrowedSouls.Data;
using BorrowedSouls.Soul;
using BorrowedSouls.UI;
using BorrowedSouls.Vessels;
using UnityEngine;

namespace BorrowedSouls.Run
{
    /// <summary>
    /// Runs the 8-wave arena:
    /// Intro → Fighting (batched spawns with a 0.6 s ground marker, ≥ 6 m from the player, MaxAlive cap) →
    /// Collecting (wave-end ember pull) → Upgrading (game paused, one 3-choice per banked upgrade) →
    /// WaitingForBody (player must be inside a body) → next wave; victory after the last wave, defeat when the
    /// soul expires. Possessed hosts and corpses never count as remaining enemies (RUN-03). Victory leaves the world
    /// running for its last moments: whatever the boss had summoned falls with it. Defeat freezes on the moment of death.
    /// </summary>
    public sealed class WaveDirector : MonoBehaviour
    {
        private enum Phase
        {
            NotStarted, // default (0): waiting for the title screen to call BeginRun
            Intro,
            Fighting,
            Collecting,
            Upgrading,
            WaitingForBody,
            Ended
        }

        private struct PendingSpawn
        {
            public WaveEntry Entry;
            public Vector3 Position;
            public float TimeLeft;
            public GameObject Marker;
        }

        [SerializeField] private GameRoot _root;
        [SerializeField] private PlayerSoulController _soul;
        [SerializeField] private EmberSystem _embers;
        [SerializeField] private EnemyCatalog _catalog;
        [SerializeField] private UpgradeCatalog _upgradeTexts;
        [SerializeField] private UpgradeSelectionPanel _upgradePanel;
        [SerializeField] private WaveDefinition[] _waves = new WaveDefinition[0];
        [SerializeField] private Transform[] _spawnPoints = new Transform[0];
        [SerializeField] private GameObject _spawnMarkerPrefab;
        [SerializeField] private Transform _enemyParent;
        [SerializeField, Min(0f)] private float _spawnTelegraph = 0.6f;
        [SerializeField, Min(0f)] private float _minSpawnDistance = 6f;
        [SerializeField, Min(0f)] private float _introDelay = 1.5f;
        [SerializeField, Min(0f)] private float _collectTimeout = 2.5f;
        [Tooltip("0 = random seed each run; otherwise fixed for reproducible balance runs.")]
        [SerializeField] private int _seed;

        private readonly Queue<WaveEntry> _queue = new Queue<WaveEntry>(32);
        private readonly List<PendingSpawn> _pending = new List<PendingSpawn>(8);
        private readonly List<UpgradeId> _choices = new List<UpgradeId>(3);
        private readonly List<UpgradeSelectionPanel.Card> _cards = new List<UpgradeSelectionPanel.Card>(3);
        private UpgradeService _upgrades;
        private System.Random _rng;
        private Transform _spawnHolder;
        private Phase _phase;
        private int _waveIndex = -1;
        private float _timer;
        private float _waveStartTime;
        private float _runStartTime;
        private int _kills;
        private int _spawnedThisWave;
        private VesselRuntime _boss;
        private readonly List<VesselRuntime> _spawned = new List<VesselRuntime>(64);
        private readonly Dictionary<VesselRuntime, float> _brokenSince = new Dictionary<VesselRuntime, float>();
        private float _cleanupTimer;
        private TimeRequestHandle _pause;
        private int _freePicks; // Editor test hook: upgrade choices to offer before the first wave

        public const string StartWaveSessionKey = "BorrowedSouls.StartWave";
        public const string StartUpgradesSessionKey = "BorrowedSouls.StartUpgrades";

        public int WaveNumber => _waveIndex + 1;
        public int TotalWaves => _waves.Length;
        public int Seed { get; private set; }

        /// <summary>One-line state for diagnostics (RunMetrics heartbeat); not used by gameplay.</summary>
        public string DebugState => $"phase={_phase} wave={WaveNumber} queue={_queue.Count} pending={_pending.Count} living={CountLivingEnemies()} boss={(_boss != null ? _boss.Hp.ToString("0") : "-")}";
        public int RemainingEnemies => CountLivingEnemies() + _queue.Count + _pending.Count;
        public VesselRuntime Boss => _boss != null && !_boss.IsDead ? _boss : null;
        public bool IsBossWave => _waveIndex >= 0 && _waves[_waveIndex].IsBossWave;

        private void Awake()
        {
            // Guid, not Environment.TickCount: TickCount gave identical seeds to player processes started apart.
            Seed = _seed != 0 ? _seed : System.Guid.NewGuid().GetHashCode();
            _rng = new System.Random(Seed);
            _upgrades = new UpgradeService(UpgradeService.DefaultMaxLevels());
            var holder = new GameObject("SpawnHolder (inactive)");
            holder.transform.SetParent(transform);
            holder.SetActive(false);
            _spawnHolder = holder.transform;
        }

        private void OnEnable()
        {
            _root.Events.Subscribe<SoulExpired>(OnSoulExpired);
            _root.Events.Subscribe<VesselDied>(OnVesselDied);
            _root.Events.Subscribe<BossSummonRequested>(OnBossSummon);
        }

        private void OnDisable()
        {
            _root.Events.Unsubscribe<SoulExpired>(OnSoulExpired);
            _root.Events.Unsubscribe<VesselDied>(OnVesselDied);
            _root.Events.Unsubscribe<BossSummonRequested>(OnBossSummon);
        }

        [Tooltip("Wait for the title screen to call BeginRun (false = start immediately, e.g. test scenes).")]
        [SerializeField] private bool _waitForTitle;

        private bool _started;

        private void Start()
        {
            if (!_waitForTitle) BeginRun();
        }

        /// <summary>Starts the run (called by the title screen, or on Start when no title is used).</summary>
        public void BeginRun()
        {
            if (_started) return;
            if (_waves.Length == 0)
            {
                Debug.LogError("WaveDirector has no waves assigned; run not started.", this);
                return;
            }
            _started = true;
            _runStartTime = Time.time;
            int start = 0;
#if UNITY_EDITOR
            // Editor-only test hook: start at a later wave (e.g. the boss) without playing through.
            start = Mathf.Clamp(UnityEditor.SessionState.GetInt(StartWaveSessionKey, 1) - 1, 0, _waves.Length - 1);
            _freePicks = UnityEditor.SessionState.GetInt(StartUpgradesSessionKey, 0);
#elif DEVELOPMENT_BUILD
            // Same hook for headless dev-build batches: "-startwave N".
            string[] args = System.Environment.GetCommandLineArgs();
            int arg = System.Array.IndexOf(args, "-startwave");
            if (arg >= 0 && arg + 1 < args.Length && int.TryParse(args[arg + 1], out int wave)) start = Mathf.Clamp(wave - 1, 0, _waves.Length - 1);
#endif
            if (_freePicks > 0)
            {
                _waveIndex = start - 1; // once the choices are made, the next wave is the one asked for
                BeginUpgrades();
                return;
            }
            BeginWave(start);
        }

        private void BeginWave(int index)
        {
            _waveIndex = index;
            WaveDefinition wave = _waves[index];
            _queue.Clear();
            foreach (WaveEntry e in wave.Entries)
            {
                for (int i = 0; i < e.Count; i++) _queue.Enqueue(new WaveEntry { Class = e.Class, Elite = e.Elite, Count = 1 });
            }
            _phase = Phase.Intro;
            _timer = _introDelay;
            _spawnedThisWave = 0;
            _waveStartTime = Time.time;
            _root.Events.Publish(new WaveStarted { WaveNumber = index + 1, TotalWaves = _waves.Length, Title = wave.Title, Hint = wave.Hint });
        }

        private void Update()
        {
            if (_phase == Phase.NotStarted) return;
            BorrowedSouls.Debugging.DevWatchdog.Mark("wave director");
            float dt = _root.Time.WorldDelta;
            CleanupBroken(dt);
            switch (_phase)
            {
                case Phase.Intro:
                    _timer -= dt;
                    if (_timer <= 0f)
                    {
                        _phase = Phase.Fighting;
                        if (_waves[_waveIndex].IsBossWave) SpawnBoss();
                    }
                    break;
                case Phase.Fighting:
                    UpdateSpawning(dt);
                    bool bossWave = _waves[_waveIndex].IsBossWave;
                    if (bossWave ? (_boss != null && _boss.IsDead) : (_queue.Count == 0 && _pending.Count == 0 && CountLivingEnemies() == 0)) OnWaveCleared();
                    break;
                case Phase.Collecting:
                    _timer -= dt;
                    if (_embers.LiveCount == 0 || _timer <= 0f) BeginUpgrades();
                    break;
                case Phase.WaitingForBody:
                    if (_root.PlayerVessel != null) AdvanceWave();
                    break;
            }
        }

        private void UpdateSpawning(float dt)
        {
            WaveDefinition wave = _waves[_waveIndex];
            _timer -= dt;
            int cap = wave.FirstEnemyAlone && _spawnedThisWave == 1 ? 1 : wave.MaxAlive; // tutorial gate
            if (_queue.Count > 0 && _timer <= 0f && CountLivingEnemies() + _pending.Count < cap)
            {
                _timer = wave.SpawnInterval;
                WaveEntry entry = _queue.Dequeue();
                Vector3 pos = PickSpawnPoint();
                GameObject marker = _spawnMarkerPrefab != null ? Instantiate(_spawnMarkerPrefab, pos + Vector3.up * 0.03f, Quaternion.identity) : null;
                _pending.Add(new PendingSpawn { Entry = entry, Position = pos, TimeLeft = _spawnTelegraph, Marker = marker });
                _spawnedThisWave++;
            }

            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                PendingSpawn p = _pending[i];
                p.TimeLeft -= dt;
                if (p.TimeLeft > 0f)
                {
                    _pending[i] = p;
                    continue;
                }
                if (p.Marker != null) Destroy(p.Marker);
                Spawn(p.Entry, p.Position);
                _pending.RemoveAt(i);
            }
        }

        private Vector3 PickSpawnPoint()
        {
            Vector3 player = _root.PlayerVessel != null ? _root.PlayerVessel.transform.position : (_soul.IsFree ? _soul.SoulPosition : Vector3.zero);
            int start = _rng.Next(_spawnPoints.Length);
            for (int k = 0; k < _spawnPoints.Length; k++)
            {
                Transform t = _spawnPoints[(start + k) % _spawnPoints.Length];
                Vector3 d = t.position - player;
                d.y = 0f;
                if (d.sqrMagnitude >= _minSpawnDistance * _minSpawnDistance) return new Vector3(t.position.x, 0f, t.position.z);
            }
            Transform far = _spawnPoints[start];
            return new Vector3(far.position.x, 0f, far.position.z);
        }

        private void Spawn(WaveEntry entry, Vector3 position)
        {
            if (!_catalog.TryGet(entry.Class, out EnemyCatalog.Entry def)) return;
            // Spawn under an inactive parent so Configure runs before Awake, then activate by reparenting.
            GameObject go = Instantiate(def.Prefab, position, Quaternion.LookRotation(-position.normalized + Vector3.forward * 0.001f), _spawnHolder);
            go.name = $"{def.Class}{(entry.Elite ? "_Elite" : "")}_{WaveNumber}";
            var brain = go.GetComponent<ClassBrain>();
            if (brain == null) brain = go.AddComponent<ClassBrain>();
            brain.Configure(def.Ai);
            var vessel = go.GetComponent<VesselRuntime>();
            float hpMul = entry.Elite ? _catalog.EliteHpMultiplier : 1f;
            float atkMul = entry.Elite ? _catalog.EliteAttackMultiplier : 1f;
            vessel.Configure(_root, Team.Enemy, entry.Elite ? VesselKind.Elite : VesselKind.Normal, brain, hpMul, atkMul);
            if (entry.Elite) go.transform.localScale *= _catalog.EliteScale;
            go.transform.SetParent(_enemyParent, true);
            _spawned.Add(vessel);
        }

        private void SpawnBoss()
        {
            if (_catalog.BossPrefab == null)
            {
                Debug.LogError("[WaveDirector] Boss wave but EnemyCatalog has no boss prefab; the wave cannot be won.", this);
                return;
            }
            Vector3 player = _root.PlayerVessel != null ? _root.PlayerVessel.transform.position : Vector3.zero;
            Vector3 pos = new Vector3(-Mathf.Sign(player.x == 0f ? 1f : player.x) * 2f, 0f, player.z > 0f ? -9f : 9f);
            GameObject go = Instantiate(_catalog.BossPrefab, pos, Quaternion.LookRotation(new Vector3(-pos.x, 0f, -pos.z)), _spawnHolder);
            go.name = "SoulWarden";
            var brain = go.GetComponent<BossBrain>();
            _boss = go.GetComponent<VesselRuntime>();
            _boss.Configure(_root, Team.Enemy, VesselKind.Boss, brain, 1f, 1f);
            go.transform.SetParent(_enemyParent, true);
        }

        private void OnBossSummon(BossSummonRequested e)
        {
            if (_phase != Phase.Fighting) return;
            // Possessable adds: random normal classes, still bound by the wave's MaxAlive and the 0.6 s marker.
            for (int i = 0; i < e.Count; i++)
            {
                var cls = (VesselClass)_rng.Next(3);
                _queue.Enqueue(new WaveEntry { Class = cls, Elite = false, Count = 1 });
            }
            _timer = 0f;
        }

        /// <summary>Shattered enemy bodies (dissolved corpses, destroyed hosts) are removed a few seconds later.</summary>
        private void CleanupBroken(float dt)
        {
            _cleanupTimer -= dt;
            if (_cleanupTimer > 0f) return;
            _cleanupTimer = 1f;
            float now = Time.time;
            for (int i = _spawned.Count - 1; i >= 0; i--)
            {
                VesselRuntime v = _spawned[i];
                if (v == null)
                {
                    _spawned.RemoveAt(i);
                    continue;
                }
                if (!v.State.IsBroken || v == _root.PlayerVessel)
                {
                    _brokenSince.Remove(v);
                    continue;
                }
                if (!_brokenSince.TryGetValue(v, out float since))
                {
                    _brokenSince[v] = now;
                    continue;
                }
                if (now - since < 3f) continue;
                _brokenSince.Remove(v);
                _spawned.RemoveAt(i);
                Destroy(v.gameObject);
            }
        }

        private int CountLivingEnemies()
        {
            int n = 0;
            var vessels = _root.Vessels;
            for (int i = 0; i < vessels.Count; i++)
            {
                if (vessels[i].State.Owner == VesselOwner.EnemyAI && vessels[i] != _boss) n++;
            }
            return n;
        }

        private void HealHeldBody()
        {
            VesselRuntime body = _root.PlayerVessel;
            if (body == null) return;
            float healed = BodyRecovery.OnWaveCleared(body.State, _root.SoulSettings.WaveClearHealShare + _root.Bonuses.WaveHealShare);
            if (healed > 0f) _root.Events.Publish(new BodyHealed { Vessel = body, Amount = healed });
        }

        private void OnWaveCleared()
        {
            _root.Events.Publish(new WaveCleared { WaveNumber = WaveNumber, ElapsedSeconds = Time.time - _waveStartTime });
            if (_waveIndex >= _waves.Length - 1)
            {
                EndRun(victory: true);
                return;
            }
            HealHeldBody();
            _embers.PullAll();
            _phase = Phase.Collecting;
            _timer = _collectTimeout;
        }

        private void BeginUpgrades()
        {
            _phase = Phase.Upgrading;
            if (!_pause.IsValid) _pause = _root.Time.Stack.Push(TimeRequestKind.Pause);
            OfferNextUpgrade();
        }

        private void OfferNextUpgrade()
        {
            if (_freePicks > 0) _freePicks--;
            else if (!_embers.Wallet.ConsumeBanked())
            {
                FinishUpgrades();
                return;
            }

            _upgrades.RollChoices(_soul.Soul, 3, _rng, _choices);
            if (_choices.Count == 0)
            {
                FinishUpgrades();
                return;
            }

            _cards.Clear();
            for (int i = 0; i < _choices.Count; i++)
            {
                UpgradeId id = _choices[i];
                int level = UpgradeService.GetLevel(_soul.Soul, id);
                _cards.Add(new UpgradeSelectionPanel.Card
                {
                    Title = $"{_upgradeTexts.NameOf(id)}  Lv{level} → {level + 1}",
                    Body = _upgradeTexts.Describe(id, level)
                });
            }
            int remaining = _embers.Wallet.BankedUpgrades + _freePicks;
            string header = remaining > 0 ? Loc.F("靈魂強化（之後還有 {0} 次）", remaining) : Loc.T("靈魂強化");
            _upgradePanel.Show(header, _cards, OnUpgradePicked);
        }

        private void OnUpgradePicked(int index)
        {
            UpgradeId id = _choices[index];
            if (_upgrades.Apply(_soul.Soul, id))
            {
                _root.Events.Publish(new UpgradeChosen { Id = id, ResultingLevel = UpgradeService.GetLevel(_soul.Soul, id) });
            }
            OfferNextUpgrade();
        }

        private void FinishUpgrades()
        {
            if (_pause.IsValid)
            {
                _root.Time.Stack.Pop(_pause);
                _pause = default;
            }
            _phase = Phase.WaitingForBody; // the next wave starts only once the soul is inside a body
        }

        private void AdvanceWave()
        {
            BeginWave(_waveIndex + 1);
        }

        private void OnVesselDied(VesselDied e)
        {
            if (e.Vessel != null && e.Vessel.State.IsFallenCorpse) _kills++;
        }

        private void OnSoulExpired(SoulExpired e)
        {
            EndRun(victory: false);
        }

        private void EndRun(bool victory)
        {
            if (_phase == Phase.Ended) return;
            _phase = Phase.Ended;
            for (int i = 0; i < _pending.Count; i++) if (_pending[i].Marker != null) Destroy(_pending[i].Marker);
            _pending.Clear();
            _queue.Clear();
            if (!victory && !_pause.IsValid) _pause = _root.Time.Stack.Push(TimeRequestKind.Pause);
            _root.Events.Publish(new RunEnded
            {
                Victory = victory,
                WavesCleared = victory ? _waves.Length : _waveIndex,
                Seconds = Time.time - _runStartTime,
                Kills = _kills,
                Possessions = _soul.Soul.Possessions
            });
            if (victory) FellTheRest(); // after the results were taken: they fell with the boss, the player didn't kill them
        }

        private void FellTheRest()
        {
            _soul.StopTheClock();
            var vessels = _root.Vessels;
            for (int i = vessels.Count - 1; i >= 0; i--)
            {
                if (vessels[i].State.Owner == VesselOwner.EnemyAI) vessels[i].FallWithoutAHit();
            }
        }
    }
}
