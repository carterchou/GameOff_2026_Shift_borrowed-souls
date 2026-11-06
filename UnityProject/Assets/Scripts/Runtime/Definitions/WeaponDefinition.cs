using UnityEngine;

namespace BorrowedSouls.Data
{
    /// <summary>Weapon = weak combo chain + strong + special.
    [CreateAssetMenu(menuName = "Borrowed Souls/Weapon Definition", fileName = "WPN_")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [SerializeField] private string _displayName = "Longsword";
        [SerializeField] private AttackDefinition[] _weakCombo = new AttackDefinition[0];
        [SerializeField] private AttackDefinition _strong;
        [SerializeField] private AttackDefinition _special;

        public string DisplayName => _displayName;
        public int WeakComboLength => _weakCombo.Length;
        public AttackDefinition GetWeak(int index) => _weakCombo.Length == 0 ? null : _weakCombo[Mathf.Clamp(index, 0, _weakCombo.Length - 1)];
        public AttackDefinition Strong => _strong;
        public AttackDefinition Special => _special;
    }
}
