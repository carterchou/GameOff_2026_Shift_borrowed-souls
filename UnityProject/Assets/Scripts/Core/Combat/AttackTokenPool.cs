using System.Collections.Generic;

namespace BorrowedSouls.Core.Combat
{
    /// <summary>
    /// Limits how many enemies may telegraph/attack at once (at most 3 attack tokens, shared by melee,
    /// ranged and mage). Tokens are leases that expire on their own, so a brain that stops running (death,
    /// possession, despawn) can never leak a token.
    /// </summary>
    public sealed class AttackTokenPool
    {
        private readonly Dictionary<int, float> _leases = new Dictionary<int, float>(8);
        private readonly List<int> _expired = new List<int>(8);

        public AttackTokenPool(int capacity)
        {
            Capacity = capacity < 1 ? 1 : capacity;
        }

        public int Capacity { get; set; }
        public int InUse => _leases.Count;

        public bool Holds(int ownerId) => _leases.ContainsKey(ownerId);

        /// <summary>Acquire or renew a token until <paramref name="now"/> + <paramref name="lease"/>.</summary>
        public bool TryAcquire(int ownerId, float now, float lease)
        {
            Expire(now);
            if (_leases.ContainsKey(ownerId))
            {
                _leases[ownerId] = now + lease;
                return true;
            }
            if (_leases.Count >= Capacity) return false;
            _leases[ownerId] = now + lease;
            return true;
        }

        public void Release(int ownerId)
        {
            _leases.Remove(ownerId);
        }

        public void Expire(float now)
        {
            _expired.Clear();
            foreach (var pair in _leases)
            {
                if (pair.Value <= now) _expired.Add(pair.Key);
            }
            for (int i = 0; i < _expired.Count; i++) _leases.Remove(_expired[i]);
        }

        public void Clear()
        {
            _leases.Clear();
        }
    }
}
