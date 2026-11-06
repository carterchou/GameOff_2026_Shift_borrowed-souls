using UnityEngine;

namespace BorrowedSouls.Data
{
    /// <summary>
    /// Per-class enemy behaviour tuning. Enemies use the same weapon data as the player;
    /// this profile only changes positioning and rhythm, plus an extra wind-up so every attack is readable.
    /// </summary>
    [CreateAssetMenu(menuName = "Borrowed Souls/AI Profile", fileName = "AI_")]
    public sealed class AiProfile : ScriptableObject
    {
        [Header("Positioning (metres)")]
        [SerializeField, Min(0f)] private float _senseRange = 14f;
        [Tooltip("Seconds the player's body may stay beyond sense range before this enemy goes after it (AiPositioning).")]
        [SerializeField, Min(0f)] private float _outOfSenseHold = 2f;
        [SerializeField, Min(0f)] private float _preferredMin;
        [SerializeField, Min(0f)] private float _preferredMax = 1.8f;
        [Tooltip("Back off when the target is closer than this (archer 4 m).")]
        [SerializeField, Min(0f)] private float _retreatDistance;
        [SerializeField, Range(0f, 1f)] private float _strafeSpeed = 0.35f;

        [Header("Attacking")]
        [SerializeField, Min(0f)] private float _attackRange = 2.2f;
        [SerializeField, Min(0f)] private float _attackCooldown = 1.2f;
        [SerializeField, Range(0f, 1f)] private float _strongChance = 0.25f;
        [SerializeField, Range(0f, 1f)] private float _specialChance = 0.1f;
        [SerializeField, Min(0f)] private float _specialMaxDistance = 99f;
        [Tooltip("Extra wind-up before pressing an attack.")]
        [SerializeField, Min(0f)] private float _extraTelegraph = 0.25f;
        [SerializeField] private bool _requiresLineOfSight;

        [Header("Elite: deny corpses")]
        [Tooltip("Elites walk to a lying corpse within this radius (and closer to them than the player) and smash it with their strong attack (0 = never).")]
        [SerializeField, Min(0f)] private float _corpseSmashRadius = 9f;
        [Tooltip("Wind-up before the smash, so the player can see it coming and race for the corpse.")]
        [SerializeField, Min(0f)] private float _corpseSmashTelegraph = 0.8f;
        [SerializeField, Min(0f)] private float _corpseSmashCooldown = 7f;
        [Tooltip("Give up the walk to the corpse and fight when the player gets this close.")]
        [SerializeField, Min(0f)] private float _corpseSmashAbortDistance = 2.5f;

        [Header("Thinking")]
        [SerializeField, Min(0.05f)] private float _decisionInterval = 0.2f;

        public float SenseRange => _senseRange;
        public float OutOfSenseHold => _outOfSenseHold;
        public float PreferredMin => _preferredMin;
        public float PreferredMax => _preferredMax;
        public float RetreatDistance => _retreatDistance;
        public float StrafeSpeed => _strafeSpeed;
        public float AttackRange => _attackRange;
        public float AttackCooldown => _attackCooldown;
        public float StrongChance => _strongChance;
        public float SpecialChance => _specialChance;
        public float SpecialMaxDistance => _specialMaxDistance;
        public float ExtraTelegraph => _extraTelegraph;
        public bool RequiresLineOfSight => _requiresLineOfSight;
        public float DecisionInterval => _decisionInterval;
        public float CorpseSmashRadius => _corpseSmashRadius;
        public float CorpseSmashTelegraph => _corpseSmashTelegraph;
        public float CorpseSmashCooldown => _corpseSmashCooldown;
        public float CorpseSmashAbortDistance => _corpseSmashAbortDistance;
    }
}
