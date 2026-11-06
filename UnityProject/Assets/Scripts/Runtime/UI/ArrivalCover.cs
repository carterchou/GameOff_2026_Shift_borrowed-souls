using System.Collections;
using BorrowedSouls.Bootstrap;
using BorrowedSouls.Core.Time;
using UnityEngine;

namespace BorrowedSouls.UI
{
    /// <summary>
    /// Keeps the loading page up after the new scene arrives, until its pools are full and its first frames have been drawn, so the player
    /// never sees those heavy frames. The run doesn't start counting until then. Does nothing when a scene is opened directly.
    /// </summary>
    public sealed class ArrivalCover : MonoBehaviour
    {
        [SerializeField] private LoadingScreen _page;
        [Tooltip("Left empty in a scene with no run to hold.")]
        [SerializeField] private GameRoot _root;
        [Tooltip("Frames drawn behind the page before it lifts: the first ones are the slowest, since materials are met for the first time.")]
        [SerializeField, Min(0)] private int _settleFrames = 3;

        private TimeRequestHandle _hold;

        public bool Holding { get; private set; }

        private void Awake()
        {
            if (!LoadingScreen.TakeArrival()) return;
            Holding = true;
            _page.Hold();
            if (_root != null) _hold = _root.Time.Stack.Push(TimeRequestKind.Pause);
            StartCoroutine(Reveal());
        }

        private IEnumerator Reveal()
        {
            float started = Time.realtimeSinceStartup;
            yield return null; // the pools start filling in this scene's Awake calls, which may come after this one
            while (PoolWarmup.Busy) yield return null;
            for (int i = 0; i < _settleFrames; i++) yield return null;
            Debug.Log($"[Loading] ready to play {Time.realtimeSinceStartup - started:0.00}s after arriving");
            if (_root != null && _hold.IsValid) _root.Time.Stack.Pop(_hold);
            yield return _page.Lift();
            Holding = false;
        }
    }
}
