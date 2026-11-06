using UnityEngine;

namespace BorrowedSouls.Data
{
    /// <summary>Global combat / feel tuning.
    [CreateAssetMenu(menuName = "Borrowed Souls/Combat Settings", fileName = "CombatSettings")]
    public sealed class CombatSettings : ScriptableObject
    {
        [Header("Rules")]
        [SerializeField, Min(1f)] private float _counterBonus = 1.2f;
        [SerializeField, Min(0f)] private float _weakComboResetWindow = 0.7f;
        [Tooltip("Seconds an attack/dodge press is remembered while busy, so combos feel responsive.")]
        [SerializeField, Min(0f)] private float _inputBufferTime = 0.2f;
        [Tooltip("at most 3 enemies telegraph/attack at once (melee, ranged and mage share the tokens).")]
        [SerializeField, Min(1)] private int _maxConcurrentAttackers = 3;
        [SerializeField, Min(0.1f)] private float _attackTokenLease = 2.5f;

        [Header("Feel")]
        [SerializeField, Min(0f)] private float _maxHitStopChain = 0.15f;
        [SerializeField, Min(0f)] private float _minHitStopGap = 0.2f;
        [SerializeField, Min(0f)] private float _killHitStopBonus = 0.04f;
        [SerializeField, Min(0f)] private float _knockbackDamping = 12f;

        [Header("Accessibility (player-facing settings later)")]
        [SerializeField, Range(0f, 2f)] private float _cameraShakeScale = 1f;
        [SerializeField] private bool _reduceFlashes;

        public float CounterBonus => _counterBonus;
        public float WeakComboResetWindow => _weakComboResetWindow;
        public float InputBufferTime => _inputBufferTime;
        public int MaxConcurrentAttackers => _maxConcurrentAttackers;
        public float AttackTokenLease => _attackTokenLease;
        public float MaxHitStopChain => _maxHitStopChain;
        public float MinHitStopGap => _minHitStopGap;
        public float KillHitStopBonus => _killHitStopBonus;
        public float KnockbackDamping => _knockbackDamping;
        public float CameraShakeScale => _cameraShakeScale;
        public bool ReduceFlashes => _reduceFlashes;
    }
}
