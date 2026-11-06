using System.Collections.Generic;

namespace BorrowedSouls.Core.Stats
{
    public enum StatModType
    {
        Flat,
        AddPercent,
        Multiply
    }

    public readonly struct StatModifier
    {
        public readonly float Value;
        public readonly StatModType Type;
        public readonly object Source;

        public StatModifier(float value, StatModType type, object source)
        {
            Value = value;
            Type = type;
            Source = source;
        }
    }

    /// <summary>
    /// Runtime stat: base value copied from definition data plus removable modifiers.
    /// Value = (Base + ΣFlat) × (1 + ΣAddPercent) × ΠMultiply. Soul upgrades are modifiers whose Source is the
    /// upgrade, so they can be removed when the soul leaves a vessel.
    /// </summary>
    public sealed class Stat
    {
        private readonly List<StatModifier> _modifiers = new List<StatModifier>(4);
        private float _baseValue;
        private float _cached;
        private bool _dirty = true;

        public Stat(float baseValue)
        {
            _baseValue = baseValue;
        }

        public float BaseValue
        {
            get => _baseValue;
            set
            {
                _baseValue = value;
                _dirty = true;
            }
        }

        public float Value
        {
            get
            {
                if (_dirty)
                {
                    _cached = Calculate();
                    _dirty = false;
                }
                return _cached;
            }
        }

        public int ModifierCount => _modifiers.Count;

        public void AddModifier(StatModifier modifier)
        {
            _modifiers.Add(modifier);
            _dirty = true;
        }

        public int RemoveAllFromSource(object source)
        {
            int removed = _modifiers.RemoveAll(m => ReferenceEquals(m.Source, source));
            if (removed > 0) _dirty = true;
            return removed;
        }

        private float Calculate()
        {
            float flat = 0f;
            float addPercent = 0f;
            float multiply = 1f;
            for (int i = 0; i < _modifiers.Count; i++)
            {
                StatModifier m = _modifiers[i];
                switch (m.Type)
                {
                    case StatModType.Flat: flat += m.Value; break;
                    case StatModType.AddPercent: addPercent += m.Value; break;
                    case StatModType.Multiply: multiply *= m.Value; break;
                }
            }
            return (_baseValue + flat) * (1f + addPercent) * multiply;
        }
    }
}
