using BorrowedSouls.Bootstrap;
using BorrowedSouls.Core.Soul;
using BorrowedSouls.Core.Stats;
using BorrowedSouls.Run;
using BorrowedSouls.Vessels;
using UnityEngine;

namespace BorrowedSouls.Soul
{
    /// <summary>
    /// Applies the soul's stat upgrades (SoulMight +10% damage, SoulHaste +8% speed, VesselVitality +10% MaxHP
    /// per level) as modifiers on whichever body the soul inhabits, removing them when it leaves.
    /// MaxHP changes keep the HP ratio, so swapping bodies or picking Vitality never heals.
    /// </summary>
    [DefaultExecutionOrder(101)] // after PlayerSoulController has put the soul in its first body, so talents reach that body too
    public sealed class SoulStatApplier : MonoBehaviour
    {
        [SerializeField] private GameRoot _root;
        [SerializeField] private PlayerSoulController _soul;
        [SerializeField, Min(0f)] private float _mightPerLevel = 0.10f;
        [SerializeField, Min(0f)] private float _hastePerLevel = 0.08f;
        [SerializeField, Min(0f)] private float _vitalityPerLevel = 0.10f;

        private VesselRuntime _applied;

        private void OnEnable()
        {
            _root.Events.Subscribe<SoulExited>(OnSoulExited);
            _root.Events.Subscribe<PossessionSucceeded>(OnPossessed);
            _root.Events.Subscribe<UpgradeChosen>(OnUpgradeChosen);
        }

        private void OnDisable()
        {
            _root.Events.Unsubscribe<SoulExited>(OnSoulExited);
            _root.Events.Unsubscribe<PossessionSucceeded>(OnPossessed);
            _root.Events.Unsubscribe<UpgradeChosen>(OnUpgradeChosen);
        }

        private void Start()
        {
            if (_root.PlayerVessel != null) Apply(_root.PlayerVessel);
        }

        private void OnSoulExited(SoulExited e)
        {
            if (e.PreviousVessel != null) Remove(e.PreviousVessel);
        }

        private void OnPossessed(PossessionSucceeded e)
        {
            Apply(e.To);
        }

        private void OnUpgradeChosen(UpgradeChosen e)
        {
            if (_root.PlayerVessel != null) Apply(_root.PlayerVessel);
        }

        private void Apply(VesselRuntime v)
        {
            if (_applied != null && _applied != v) Remove(_applied);
            ClearModifiers(v);
            PlayerSoul s = _soul.Soul;
            if (s.MightLevel > 0) v.DamageMultiplier.AddModifier(new StatModifier(s.MightLevel * _mightPerLevel, StatModType.AddPercent, this));
            if (s.HasteLevel > 0) v.MoveSpeed.AddModifier(new StatModifier(s.HasteLevel * _hastePerLevel, StatModType.AddPercent, this));
            if (s.VitalityLevel > 0) v.MaxHp.AddModifier(new StatModifier(s.VitalityLevel * _vitalityPerLevel, StatModType.AddPercent, this));
            float talentHp = _root.Bonuses.MaxHpShare;
            if (talentHp > 0f) v.MaxHp.AddModifier(new StatModifier(talentHp, StatModType.AddPercent, this));
            v.State.SetMaxHpKeepRatio(v.MaxHp.Value);
            _applied = v;
        }

        private void Remove(VesselRuntime v)
        {
            ClearModifiers(v);
            v.State.SetMaxHpKeepRatio(v.MaxHp.Value);
            if (_applied == v) _applied = null;
        }

        private void ClearModifiers(VesselRuntime v)
        {
            v.DamageMultiplier.RemoveAllFromSource(this);
            v.MoveSpeed.RemoveAllFromSource(this);
            v.MaxHp.RemoveAllFromSource(this);
        }
    }
}
