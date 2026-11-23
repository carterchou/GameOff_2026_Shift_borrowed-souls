using BorrowedSouls.Core.Combat;
using UnityEngine;

namespace BorrowedSouls.Data
{
    public enum AttackDelivery
    {
        /// <summary>Arc check around the attacker during Active (sword swings).</summary>
        Melee,
        /// <summary>Real flying projectiles, blocked by pillars (arrows, fire bolts).</summary>
        Projectile,
        /// <summary>Melee-style arc that may hit each target several times (flame gust).</summary>
        Cone,
        /// <summary>Expanding ring from the attacker (boss soul-ring shockwave); dodge it with i-frames or by timing.</summary>
        Ring
    }

    /// <summary>What a projectile looks like in flight. Order matches the looks on the projectile prefab.</summary>
    public enum ProjectileLook
    {
        Arrow,
        PiercingArrow,
        FireBolt,
        FlameLance
    }

    /// <summary>Ground warning drawn during the wind-up and start-up.</summary>
    public enum TelegraphShape
    {
        None,
        Fan,
        Line,
        Ring
    }

    /// <summary>One attack move (e.g. longsword weak step 2).
    [CreateAssetMenu(menuName = "Borrowed Souls/Attack Definition", fileName = "ATK_")]
    public sealed class AttackDefinition : ScriptableObject
    {
        [SerializeField] private string _attackId = "attack";
        [SerializeField] private HitTier _tier = HitTier.Weak;
        [SerializeField] private AttackDelivery _delivery = AttackDelivery.Melee;

        [Header("Damage")]
        [SerializeField, Min(0f)] private float _damage = 15f;

        [Header("Timing (seconds, world time)")]
        [SerializeField, Min(0f)] private float _startup = 0.12f;
        [SerializeField, Min(0f)] private float _active = 0.08f;
        [SerializeField, Min(0f)] private float _recovery = 0.25f;
        [SerializeField, Min(0f)] private float _cooldown;

        [Header("Melee shape")]
        [SerializeField, Min(0f)] private float _range = 2.5f;
        [SerializeField, Range(10f, 360f)] private float _arcDegrees = 120f;

        [Header("Movement")]
        [Tooltip("Fraction of normal move speed allowed while this attack runs.")]
        [SerializeField, Range(0f, 1f)] private float _moveSpeedMultiplier = 0.5f;
        [Tooltip("Forward lunge distance applied over the active window (dash slash etc.).")]
        [SerializeField, Min(0f)] private float _lungeDistance;

        [Header("Projectile")]
        [SerializeField, Min(0f)] private float _projectileSpeed = 24f;
        [SerializeField, Min(0f)] private float _projectileMaxDistance = 16f;
        [SerializeField, Min(0.01f)] private float _projectileRadius = 0.2f;
        [Tooltip("Extra targets a projectile passes through (0 = stops at the first hit).")]
        [SerializeField, Min(0)] private int _pierce;
        [Tooltip("Shots fired during the active window (e.g. 3 for the triple shot), evenly spaced.")]
        [SerializeField, Min(1)] private int _shots = 1;
        [SerializeField, Min(0f)] private float _spreadDegrees;
        [SerializeField] private Color _projectileColor = new Color(1f, 0.9f, 0.6f);
        [Tooltip("Keep firing while the button is held (longbow weak).")]
        [SerializeField] private bool _repeatWhileHeld;

        [Header("Telegraph")]
        [SerializeField] private TelegraphShape _telegraph = TelegraphShape.None;
        [Tooltip("Ring delivery: thickness of the expanding damage band (metres).")]
        [SerializeField, Min(0.1f)] private float _ringBand = 0.9f;
        [Tooltip("Named on screen when a boss charges this attack, with any advice on a second line. Empty = not announced.")]
        [SerializeField, TextArea(1, 2)] private string _callout = "";

        [Header("Multi-hit (cone)")]
        [SerializeField, Min(0f)] private float _multiHitInterval;
        [SerializeField, Min(1)] private int _maxHitsPerTarget = 1;

        [Header("Status")]
        [SerializeField, Min(0f)] private float _burnDuration;
        [SerializeField, Min(0f)] private float _burnDamage;

        [Header("On hit")]
        [SerializeField, Min(0f)] private float _hitStun = 0.15f;
        [SerializeField, Min(0f)] private float _knockback = 2f;
        [Tooltip("Can this attack interrupt a non-armored target's strong attack / cast?")]
        [SerializeField] private bool _interrupts;
        [Tooltip("Can this attack itself be interrupted when its user is hit during startup?")]
        [SerializeField] private bool _interruptible;
        [SerializeField] private FeedbackProfile _feedback;

        [Header("Presentation")]
        [Tooltip("Animator state played by VesselAnimationDriver. The clip's length should match startup + active + recovery.")]
        [SerializeField] private string _animationState = "";
        [SerializeField] private ProjectileLook _projectileLook = ProjectileLook.Arrow;
        [Tooltip("Ribbon the blade leaves through the swing. Alpha 0 means none, which keeps light attacks clean.")]
        [SerializeField, ColorUsage(true, true)] private Color _bladeTrailColor = Color.clear;

        public string AnimationState => _animationState;
        public ProjectileLook ProjectileLook => _projectileLook;
        public bool HasBladeTrail => _bladeTrailColor.a > 0f;
        public Color BladeTrailColor => _bladeTrailColor;
        public string AttackId => _attackId;
        public HitTier Tier => _tier;
        public AttackDelivery Delivery => _delivery;
        public float ProjectileSpeed => _projectileSpeed;
        public float ProjectileMaxDistance => _projectileMaxDistance;
        public float ProjectileRadius => _projectileRadius;
        public int Pierce => _pierce;
        public int Shots => _shots;
        public float SpreadDegrees => _spreadDegrees;
        public Color ProjectileColor => _projectileColor;
        public bool RepeatWhileHeld => _repeatWhileHeld;
        public TelegraphShape Telegraph => _telegraph;
        public float RingBand => _ringBand;
        public string Callout => BorrowedSouls.Localization.Loc.T(_callout);
        public float MultiHitInterval => _multiHitInterval;
        public int MaxHitsPerTarget => _maxHitsPerTarget;
        public float BurnDuration => _burnDuration;
        public float BurnDamage => _burnDamage;
        public float Damage => _damage;
        public float Startup => _startup;
        public float Active => _active;
        public float Recovery => _recovery;
        public float Cooldown => _cooldown;
        public float Range => _range;
        public float ArcDegrees => _arcDegrees;
        public float MoveSpeedMultiplier => _moveSpeedMultiplier;
        public float LungeDistance => _lungeDistance;
        public float HitStun => _hitStun;
        public float Knockback => _knockback;
        public bool Interrupts => _interrupts;
        public bool Interruptible => _interruptible;
        public FeedbackProfile Feedback => _feedback;
    }
}
