using System;
using System.Collections.Generic;

namespace BorrowedSouls.Core.Time
{
    public enum TimeRequestKind
    {
        Pause,
        HitStop,
        SlowMotion
    }

    /// <summary>Opaque handle returned by <see cref="TimeScaleStack"/>; pass it back to release the request.</summary>
    public readonly struct TimeRequestHandle
    {
        internal readonly int Id;

        internal TimeRequestHandle(int id)
        {
            Id = id;
        }

        public bool IsValid => Id != 0;
    }

    /// <summary>
    /// Single source of truth for the world time scale. Every system that wants to pause, freeze or slow the
    /// world pushes a request here instead of writing Time.timeScale. Pause wins over everything; otherwise the
    /// smallest requested scale wins.
    /// </summary>
    public sealed class TimeScaleStack
    {
        private struct Entry
        {
            public int Id;
            public TimeRequestKind Kind;
            public float Scale;
            public bool Timed;
            public float RemainingRealtime;
        }

        private readonly List<Entry> _entries = new List<Entry>(8);
        private int _nextId = 1;
        private int _hitStopId;
        private float _hitStopChainRealtime;
        private float _hitStopGapLeft;

        /// <summary>Upper bound for back-to-back hit stops, so rapid multi-hits never lock the screen.</summary>
        public float MaxHitStopChain { get; set; } = 0.15f;

        /// <summary>Real time that must pass after a hit stop ends before a new one may start.</summary>
        public float MinHitStopGap { get; set; } = 0.2f;

        public bool IsPaused
        {
            get
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (_entries[i].Kind == TimeRequestKind.Pause) return true;
                }
                return false;
            }
        }

        public float EffectiveScale
        {
            get
            {
                float scale = 1f;
                for (int i = 0; i < _entries.Count; i++)
                {
                    Entry e = _entries[i];
                    if (e.Kind == TimeRequestKind.Pause) return 0f;
                    if (e.Scale < scale) scale = e.Scale;
                }
                return scale < 0f ? 0f : scale;
            }
        }

        public int Count => _entries.Count;

        public TimeRequestHandle Push(TimeRequestKind kind, float scale = 0f)
        {
            return Add(kind, scale, false, 0f);
        }

        public TimeRequestHandle PushTimed(TimeRequestKind kind, float scale, float durationRealtime)
        {
            if (durationRealtime <= 0f) return default;
            return Add(kind, scale, true, durationRealtime);
        }

        /// <summary>
        /// Requests a hit stop. Overlapping requests merge into one stop (longest remaining wins) and the
        /// whole chain is capped by <see cref="MaxHitStopChain"/>.
        /// </summary>
        public void RequestHitStop(float durationRealtime, float scale = 0f)
        {
            if (durationRealtime <= 0f) return;

            int index = IndexOf(_hitStopId);
            if (index < 0)
            {
                if (_hitStopGapLeft > 0f) return; // too soon after the previous stop: keep the action readable
                _hitStopChainRealtime = 0f;
                float first = Math.Min(durationRealtime, MaxHitStopChain);
                _hitStopId = Add(TimeRequestKind.HitStop, scale, true, first).Id;
                return;
            }

            Entry e = _entries[index];
            float budget = MaxHitStopChain - _hitStopChainRealtime;
            float wanted = Math.Min(durationRealtime, budget);
            if (wanted > e.RemainingRealtime) e.RemainingRealtime = wanted;
            if (scale < e.Scale) e.Scale = scale;
            _entries[index] = e;
        }

        public bool Pop(TimeRequestHandle handle)
        {
            int index = IndexOf(handle.Id);
            if (index < 0) return false;
            _entries.RemoveAt(index);
            return true;
        }

        /// <summary>Advance timed requests with real (unscaled) time.</summary>
        public void Tick(float realDelta)
        {
            if (realDelta <= 0f) return;

            if (IndexOf(_hitStopId) >= 0) _hitStopChainRealtime += realDelta;
            else if (_hitStopGapLeft > 0f) _hitStopGapLeft -= realDelta;

            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                Entry e = _entries[i];
                if (!e.Timed) continue;
                e.RemainingRealtime -= realDelta;
                if (e.RemainingRealtime <= 0f)
                {
                    _entries.RemoveAt(i);
                    if (e.Id == _hitStopId)
                    {
                        _hitStopId = 0;
                        _hitStopGapLeft = MinHitStopGap;
                    }
                    continue;
                }
                _entries[i] = e;
            }
        }

        public void Clear()
        {
            _entries.Clear();
            _hitStopId = 0;
            _hitStopChainRealtime = 0f;
            _hitStopGapLeft = 0f;
        }

        private TimeRequestHandle Add(TimeRequestKind kind, float scale, bool timed, float duration)
        {
            int id = _nextId++;
            _entries.Add(new Entry
            {
                Id = id,
                Kind = kind,
                Scale = scale < 0f ? 0f : scale,
                Timed = timed,
                RemainingRealtime = duration
            });
            return new TimeRequestHandle(id);
        }

        private int IndexOf(int id)
        {
            if (id == 0) return -1;
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Id == id) return i;
            }
            return -1;
        }
    }
}
