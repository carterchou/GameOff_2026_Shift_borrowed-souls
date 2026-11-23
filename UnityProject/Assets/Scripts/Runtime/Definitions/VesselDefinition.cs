using BorrowedSouls.Core.Combat;
using UnityEngine;

namespace BorrowedSouls.Data
{
    /// <summary>Body template shared by player and enemies.
    [CreateAssetMenu(menuName = "Borrowed Souls/Vessel Definition", fileName = "VSL_")]
    public sealed class VesselDefinition : ScriptableObject
    {
        [SerializeField] private string _displayName = "Swordsman";
        [SerializeField] private VesselClass _class = VesselClass.Melee;
        [SerializeField, Min(1f)] private float _maxHp = 120f;

        [Header("Locomotion")]
        [SerializeField, Min(0f)] private float _moveSpeed = 4.5f;
        [SerializeField, Min(0f)] private float _acceleration = 40f;
        [SerializeField, Min(0f)] private float _turnSpeedDegrees = 900f;

        [Header("Dodge")]
        [SerializeField, Min(0f)] private float _dodgeDistance = 3.2f;
        [SerializeField, Min(0.01f)] private float _dodgeDuration = 0.35f;
        [SerializeField, Min(0f)] private float _dodgeInvulnStart = 0.10f;
        [SerializeField, Min(0f)] private float _dodgeInvulnEnd = 0.28f;
        [SerializeField, Min(0f)] private float _dodgeCooldown = 0.8f;

        [Header("Class trait")]
        [Tooltip("Mage passive: periodically negates one projectile hit. 0 = none.")]
        [SerializeField, Min(0f)] private float _projectileShieldCooldown;
        [Tooltip("Boss: never staggered or knocked back by hits.")]
        [SerializeField] private bool _superArmor;

        [Header("Weapon")]
        [SerializeField] private WeaponDefinition _weapon;

        [Header("Grey-box look")]
        [SerializeField] private Color _bodyColor = new Color(0.55f, 0.58f, 0.62f);

        public string DisplayName => BorrowedSouls.Localization.Loc.T(_displayName);
        public VesselClass Class => _class;
        public float MaxHp => _maxHp;
        public float MoveSpeed => _moveSpeed;
        public float Acceleration => _acceleration;
        public float TurnSpeedDegrees => _turnSpeedDegrees;
        public float DodgeDistance => _dodgeDistance;
        public float DodgeDuration => _dodgeDuration;
        public float DodgeInvulnStart => _dodgeInvulnStart;
        public float DodgeInvulnEnd => _dodgeInvulnEnd;
        public float DodgeCooldown => _dodgeCooldown;
        public float ProjectileShieldCooldown => _projectileShieldCooldown;
        public bool SuperArmor => _superArmor;
        public WeaponDefinition Weapon => _weapon;
        public Color BodyColor => _bodyColor;
    }
}
