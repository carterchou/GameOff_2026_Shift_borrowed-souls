using BorrowedSouls.Bootstrap;
using BorrowedSouls.Combat;
using BorrowedSouls.Run;
using BorrowedSouls.Soul;
using UnityEngine;

namespace BorrowedSouls.Debugging
{
    /// <summary>
    /// Logs one line per gameplay milestone ("[RUN] ...") so playtests and autopilot runs leave balance data
    /// in the Editor log. Read-only listener; no gameplay effect.
    /// </summary>
    public sealed class RunMetrics : MonoBehaviour
    {
        [SerializeField] private GameRoot _root;
        [SerializeField] private PlayerSoulController _soul;
        [SerializeField] private WaveDirector _waves;
        [SerializeField, Min(1f)] private float _heartbeatSeconds = 20f;

        private float _nextHeartbeat;
        private readonly System.Text.StringBuilder _foes = new System.Text.StringBuilder(256);

        private int _waveKills;
        private int _wavePossessions;
        private int _waveDeathsOfBody;
        private int _wave;

        private void OnEnable()
        {
            _root.Events.Subscribe<WaveStarted>(OnWaveStarted);
            _root.Events.Subscribe<WaveCleared>(OnWaveCleared);
            _root.Events.Subscribe<VesselDied>(OnVesselDied);
            _root.Events.Subscribe<PossessionSucceeded>(OnPossessed);
            _root.Events.Subscribe<SoulExited>(OnSoulExited);
            _root.Events.Subscribe<UpgradeChosen>(OnUpgrade);
            _root.Events.Subscribe<RunEnded>(OnRunEnded);
            _root.Events.Subscribe<CorpseSmashed>(OnCorpseSmashed);
        }

        private void OnDisable()
        {
            _root.Events.Unsubscribe<WaveStarted>(OnWaveStarted);
            _root.Events.Unsubscribe<WaveCleared>(OnWaveCleared);
            _root.Events.Unsubscribe<VesselDied>(OnVesselDied);
            _root.Events.Unsubscribe<PossessionSucceeded>(OnPossessed);
            _root.Events.Unsubscribe<SoulExited>(OnSoulExited);
            _root.Events.Unsubscribe<UpgradeChosen>(OnUpgrade);
            _root.Events.Unsubscribe<RunEnded>(OnRunEnded);
            _root.Events.Unsubscribe<CorpseSmashed>(OnCorpseSmashed);
        }

        private static void OnCorpseSmashed(CorpseSmashed e)
        {
            Debug.Log($"[RUN] corpse smashed {(e.Corpse != null ? e.Corpse.name : "?")} by {(e.By != null ? e.By.name : "?")} t={Time.time:0.0}");
        }

        private void Update()
        {
            // Heartbeat on real time so a stalled run is visible in headless logs: a frozen world keeps ticking
            // here with t unchanged (the time requests show why); a hung main thread stops the lines entirely.
            if (_waves == null || Time.realtimeSinceStartup < _nextHeartbeat) return;
            _nextHeartbeat = Time.realtimeSinceStartup + _heartbeatSeconds;
            var v = _root.PlayerVessel;
            string body = v != null ? $"{v.name} {v.Hp:0}/{v.MaxHp.Value:0}" : (_soul.IsFree ? "soul(free)" : "none");
            if (v != null)
            {
                Vector3 p = v.transform.position;
                float nearest = float.MaxValue;
                var vessels = _root.Vessels;
                for (int i = 0; i < vessels.Count; i++)
                {
                    var o = vessels[i];
                    // Enemy-controlled bodies only: vacated shells are not on the player's team either.
                    if (o == v || o.IsDead || o.State.Owner != BorrowedSouls.Core.Soul.VesselOwner.EnemyAI) continue;
                    nearest = Mathf.Min(nearest, Vector3.Distance(o.transform.position, p));
                    Vector3 op = o.transform.position;
                    _foes.Append($" {o.name}({op.x:0.0},{op.z:0.0}){o.Hp:0}{(o.Combat.IsAttacking ? "!" : "")}");
                }
                body += $" pos=({p.x:0.0},{p.z:0.0}) nearestFoe={(nearest < float.MaxValue ? nearest.ToString("0.0") : "-")} foes:{_foes}";
                _foes.Clear();
            }
            Debug.Log($"[RUN] tick t={Time.time:0} rt={Time.realtimeSinceStartup:0} ts={Time.timeScale:0.00} paused={_root.Time.IsPaused} timeReqs={_root.Time.Stack.Count} {_waves.DebugState} body={body}");
        }

        private void OnWaveStarted(WaveStarted e)
        {
            _wave = e.WaveNumber;
            _waveKills = _wavePossessions = _waveDeathsOfBody = 0;
            Debug.Log($"[RUN] wave {e.WaveNumber}/{e.TotalWaves} start '{e.Title}' t={Time.time:0.0}" + (e.WaveNumber == 1 && _waves != null ? $" seed={_waves.Seed}" : ""));
        }

        private void OnWaveCleared(WaveCleared e)
        {
            var v = _root.PlayerVessel;
            string body = v != null ? $"{v.Definition.DisplayName} {v.Hp:0}/{v.MaxHp.Value:0}" : "soul";
            Debug.Log($"[RUN] wave {e.WaveNumber} cleared in {e.ElapsedSeconds:0.0}s kills={_waveKills} possessions={_wavePossessions} bodiesLost={_waveDeathsOfBody} body={body}");
        }

        private void OnVesselDied(VesselDied e)
        {
            if (e.Vessel == null) return;
            if (e.Vessel.State.IsFallenCorpse) _waveKills++;
            else if (e.Vessel.State.IsBroken && e.Vessel == _lastPlayerBody) _waveDeathsOfBody++;
        }

        private Vessels.VesselRuntime _lastPlayerBody;

        private void OnPossessed(PossessionSucceeded e)
        {
            _wavePossessions++;
            _lastPlayerBody = e.To;
            Debug.Log($"[RUN] possess {e.To.name} ({e.To.Definition.DisplayName}) firstClaim={e.FirstClaim} suppressed={e.SuppressedEnemySoul} healed={e.Healed:0}");
        }

        private void OnSoulExited(SoulExited e)
        {
            if (e.PreviousVessel != null) _lastPlayerBody = e.PreviousVessel;
            Debug.Log($"[RUN] soul exit from {(e.PreviousVessel != null ? e.PreviousVessel.name : "?")} forced={e.Forced} enemyResumed={e.EnemySoulResumed} brokeOnExit={(e.PreviousVessel != null && e.PreviousVessel.State.IsBroken)}");
        }

        private void OnUpgrade(UpgradeChosen e) => Debug.Log($"[RUN] upgrade {e.Id} -> Lv{e.ResultingLevel}");

        private void OnRunEnded(RunEnded e)
        {
            Debug.Log($"[RUN] END victory={e.Victory} waves={e.WavesCleared} time={e.Seconds:0.0}s kills={e.Kills} possessions={e.Possessions} avgFps={Time.frameCount / Mathf.Max(0.01f, Time.realtimeSinceStartup):0}");
        }
    }
}
