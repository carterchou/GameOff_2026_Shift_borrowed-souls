using BorrowedSouls.Core.Time;
using UnityEngine;

namespace BorrowedSouls.Bootstrap
{
    /// <summary>
    /// The only writer of Time.timeScale. Exposes the three clocks from:
    /// WorldDelta (hit stop / slow-mo / pause), GameplayUnscaledDelta (pause only — soul &amp; corpse timers),
    /// RealDelta (UI).
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class TimeService : MonoBehaviour
    {
        private readonly TimeScaleStack _stack = new TimeScaleStack();

        public TimeScaleStack Stack => _stack;
        public float WorldDelta => Time.deltaTime;
        public float GameplayUnscaledDelta => _stack.IsPaused ? 0f : Time.unscaledDeltaTime;
        public float RealDelta => Time.unscaledDeltaTime;
        public bool IsPaused => _stack.IsPaused;

        public void Configure(float maxHitStopChain, float minHitStopGap)
        {
            _stack.MaxHitStopChain = maxHitStopChain;
            _stack.MinHitStopGap = minHitStopGap;
        }

        private void Update()
        {
            _stack.Tick(Time.unscaledDeltaTime);
            Time.timeScale = _stack.EffectiveScale;
        }

        private void OnDestroy()
        {
            _stack.Clear();
            Time.timeScale = 1f;
        }
    }
}
