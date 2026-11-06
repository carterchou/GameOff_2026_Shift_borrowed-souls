using System.Collections;
using BorrowedSouls.Bootstrap;
using BorrowedSouls.Data;
using BorrowedSouls.Debugging;
using BorrowedSouls.Meta;
using BorrowedSouls.UI;
using UnityEngine;

namespace BorrowedSouls.Run
{
    /// <summary>
    /// Ends a run: pays its crystals, saves, holds on the moment of death, or counts down on a victory, then fades back to the rest room,
    /// which shows the results. A bot run stops here instead, since it exits or starts over by itself.
    /// </summary>
    public sealed class ReturnToRestRoom : MonoBehaviour
    {
        [SerializeField] private GameRoot _root;
        [SerializeField] private TalentCatalog _talents;
        [SerializeField] private ScreenFade _fade;
        [SerializeField] private LoadingScreen _loading;
        [Tooltip("Real seconds the last moment of the run stays on screen.")]
        [SerializeField, Min(0f)] private float _holdSeconds = 1.6f;
        [SerializeField, Min(0.05f)] private float _fadeSeconds = 0.8f;
        [Tooltip("Real seconds counted down on screen after a victory.")]
        [SerializeField, Min(0f)] private float _victorySeconds = 5f;

        /// <summary>True from the moment the run ends, so nothing else opens over the way out.</summary>
        public bool Leaving { get; private set; }

        /// <summary>Real seconds left of the victory countdown; 0 when there is none.</summary>
        public float VictoryLeft { get; private set; }

        private void OnEnable() => _root.Events.Subscribe<RunEnded>(OnRunEnded);
        private void OnDisable() => _root.Events.Unsubscribe<RunEnded>(OnRunEnded);

        private void OnRunEnded(RunEnded e)
        {
            Leaving = true;
            if (AutoPilot.RequestedAtLaunch) return;
            int crystals = _talents.Released ? _talents.RewardFor(e.WavesCleared, e.Kills, e.Victory) : 0;
            ProfileStore.Current(_talents).AddCrystals(crystals);
            ProfileStore.Save(_talents);
            RunTransfer.Hand(new RunTransfer.Summary { Victory = e.Victory, WavesCleared = e.WavesCleared, Seconds = e.Seconds, Kills = e.Kills, Crystals = crystals });
            if (e.Victory) StartCoroutine(CountDownThenLeave());
            else StartCoroutine(Leave());
        }

        private IEnumerator CountDownThenLeave()
        {
            VictoryLeft = _victorySeconds;
            while (VictoryLeft > 0f)
            {
                yield return null;
                VictoryLeft = Mathf.Max(0f, VictoryLeft - _root.Time.RealDelta);
            }
            _holdSecondsOverride = 0f;
            yield return Leave();
        }

        /// <summary>The pause menu's way home: a run given up pays nothing.</summary>
        public void LeaveNow()
        {
            if (Leaving) return;
            Leaving = true;
            _holdSecondsOverride = 0f;
            StartCoroutine(Leave());
        }

        private float? _holdSecondsOverride;

        private IEnumerator Leave()
        {
            yield return new WaitForSecondsRealtime(_holdSecondsOverride ?? _holdSeconds);
            yield return _fade.FadeTo(1f, _fadeSeconds);
            yield return _loading.Load(GameScenes.RestRoom);
        }
    }
}
