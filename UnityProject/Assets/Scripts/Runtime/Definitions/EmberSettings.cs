using UnityEngine;

namespace BorrowedSouls.Data
{
    /// <summary>Soul ember tuning.</summary>
    [CreateAssetMenu(menuName = "Borrowed Souls/Ember Settings", fileName = "EmberSettings")]
    public sealed class EmberSettings : ScriptableObject
    {
        [Header("Drops")]
        [SerializeField, Min(0)] private int _dropNormal = 3;
        [SerializeField, Min(0)] private int _dropElite = 8;
        [SerializeField, Min(0)] private int _dropBossPhase = 10;

        [Header("Pop arc")]
        [SerializeField, Min(0.05f)] private float _popDuration = 0.8f;
        [SerializeField] private Vector2 _popHeight = new Vector2(1.6f, 2.6f);
        [SerializeField, Min(0f)] private float _scatterRadius = 2f;
        [SerializeField] private Vector2 _spinDegrees = new Vector2(300f, 600f);

        [Header("Magnet")]
        [SerializeField, Min(0f)] private float _magnetRangeHoused = 2.5f;
        [SerializeField, Min(0f)] private float _magnetRangeSoul = 5f;
        [SerializeField, Min(0f)] private float _homingStartSpeed = 5f;
        [SerializeField, Min(0f)] private float _homingAcceleration = 60f;
        [SerializeField, Min(0f)] private float _homingMaxSpeed = 30f;
        [SerializeField, Min(0.05f)] private float _collectDistance = 0.6f;
        [SerializeField, Min(0f)] private float _waveEndPullSpeed = 22f;

        [Header("Economy")]
        [SerializeField, Min(1f)] private float _upgradeBaseCost = 12f;
        [SerializeField, Min(1f)] private float _upgradeCostGrowth = 1.2f;
        [Tooltip("Pickups within this many seconds count as a combo (pitch +1 semitone each).")]
        [SerializeField, Min(0f)] private float _comboWindow = 0.4f;
        [SerializeField, Min(1)] private int _comboMax = 12;

        public int DropNormal => _dropNormal;
        public int DropElite => _dropElite;
        public int DropBossPhase => _dropBossPhase;
        public float PopDuration => _popDuration;
        public Vector2 PopHeight => _popHeight;
        public float ScatterRadius => _scatterRadius;
        public Vector2 SpinDegrees => _spinDegrees;
        public float MagnetRangeHoused => _magnetRangeHoused;
        public float MagnetRangeSoul => _magnetRangeSoul;
        public float HomingStartSpeed => _homingStartSpeed;
        public float HomingAcceleration => _homingAcceleration;
        public float HomingMaxSpeed => _homingMaxSpeed;
        public float CollectDistance => _collectDistance;
        public float WaveEndPullSpeed => _waveEndPullSpeed;
        public float UpgradeBaseCost => _upgradeBaseCost;
        public float UpgradeCostGrowth => _upgradeCostGrowth;
        public float ComboWindow => _comboWindow;
        public int ComboMax => _comboMax;
    }
}
