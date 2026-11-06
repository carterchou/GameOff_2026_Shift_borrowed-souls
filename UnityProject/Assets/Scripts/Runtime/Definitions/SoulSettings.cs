using BorrowedSouls.Core.Soul;
using UnityEngine;

namespace BorrowedSouls.Data
{
    /// <summary>Soul / possession tuning.
    [CreateAssetMenu(menuName = "Borrowed Souls/Soul Settings", fileName = "SoulSettings")]
    public sealed class SoulSettings : ScriptableObject
    {
        [Header("Soul flight")]
        [SerializeField, Min(0.5f)] private float _soulFreeLifetime = 5f;
        [SerializeField, Min(0f)] private float _soulMoveSpeed = 8f;
        [SerializeField, Min(0f)] private float _soulInteractRange = 2.5f;
        [SerializeField, Min(0f)] private float _soulExitHold = 0.25f;
        [Tooltip("Soul may pass interior walls but never leave this radius around the arena centre.")]
        [SerializeField, Min(1f)] private float _arenaRadius = 18.8f;

        [Header("Bodies")]
        [SerializeField, Min(0f)] private float _corpseClaimLifetime = 10f;
        [SerializeField, Range(0f, 1f)] private float _eliteThresholdMultiplier = 0.5f;
        [SerializeField] private float[] _invasionThresholdByLevel = { 0.15f, 0.25f, 0.35f, 0.45f, 0.55f };
        [SerializeField] private float[] _restoreRatioByLevel = { 0.65f, 0.75f, 0.85f, 0.95f };
        [SerializeField, Min(0f)] private float _soulDurationBonusPerLevel = 1f;
        [SerializeField, Min(0f)] private float _soulRangeBonusPerLevel = 0.5f;

        [Header("Soul embers")]
        [SerializeField, Min(0f)] private float _emberTimeBonus = 0.25f;
        [SerializeField, Min(0f)] private float _emberTimeBonusCap = 2f;

        [Header("Recovery")]
        [Tooltip("Share of MaxHP the held body regains when a wave ends. Kept well below the first-borrow heal, so borrowing stays the real recovery.")]
        [SerializeField, Range(0f, 1f)] private float _waveClearHealShare = 0.25f;
        [Tooltip("HP the held body regains per ember.")]
        [SerializeField, Min(0f)] private float _emberHealHp = 1f;
        [Tooltip("At or below this share of MaxHP, remind the player that borrowing a body heals.")]
        [SerializeField, Range(0f, 1f)] private float _borrowHintHpShare = 0.35f;
        [Tooltip("How near a legal body must be for that reminder, in metres. The soul can cover this before it runs out of time.")]
        [SerializeField, Min(0f)] private float _borrowHintRange = 12f;
        [SerializeField, Min(0f)] private float _borrowHintCooldown = 25f;

        [Header("Presentation")]
        [SerializeField, Range(0.05f, 1f)] private float _exitSlowMoScale = 0.4f;
        [SerializeField, Min(0f)] private float _exitSlowMoDuration = 0.4f;
        [SerializeField, Min(0.01f)] private float _visionFadeTime = 0.2f;

        public float SoulMoveSpeed => _soulMoveSpeed;
        public float SoulExitHold => _soulExitHold;
        public float ArenaRadius => _arenaRadius;
        public float CorpseClaimLifetime => _corpseClaimLifetime;
        public float ExitSlowMoScale => _exitSlowMoScale;
        public float ExitSlowMoDuration => _exitSlowMoDuration;
        public float VisionFadeTime => _visionFadeTime;
        public float WaveClearHealShare => _waveClearHealShare;
        public float EmberHealHp => _emberHealHp;
        public float BorrowHintHpShare => _borrowHintHpShare;
        public float BorrowHintRange => _borrowHintRange;
        public float BorrowHintCooldown => _borrowHintCooldown;

        /// <summary>Copies the asset values into the pure-logic config (the asset itself is never mutated).</summary>
        public SoulConfig ToConfig()
        {
            return new SoulConfig
            {
                SoulFreeLifetime = _soulFreeLifetime,
                CorpseClaimLifetime = _corpseClaimLifetime,
                SoulInteractRange = _soulInteractRange,
                EliteThresholdMultiplier = _eliteThresholdMultiplier,
                InvasionThresholdByLevel = (float[])_invasionThresholdByLevel.Clone(),
                RestoreRatioByLevel = (float[])_restoreRatioByLevel.Clone(),
                SoulDurationBonusPerLevel = _soulDurationBonusPerLevel,
                SoulRangeBonusPerLevel = _soulRangeBonusPerLevel,
                EmberTimeBonus = _emberTimeBonus,
                EmberTimeBonusCap = _emberTimeBonusCap
            };
        }
    }
}
