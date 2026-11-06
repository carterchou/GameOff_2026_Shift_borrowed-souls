namespace BorrowedSouls.Core.Soul
{
    /// <summary>
    /// Soul ember counter that banks upgrades: the n-th banked upgrade costs round(Base × Growth^(n-1)) embers.
    /// Banked upgrades are cashed at wave end. Handles multiple thresholds crossed by one pickup
    ///.
    /// </summary>
    public sealed class EmberWallet
    {
        private readonly float _baseCost;
        private readonly float _growth;

        public EmberWallet(float baseCost, float growth)
        {
            _baseCost = baseCost < 1f ? 1f : baseCost;
            _growth = growth < 1f ? 1f : growth;
        }

        /// <summary>Embers collected toward the next banked upgrade.</summary>
        public int Progress { get; private set; }
        public int TotalCollected { get; private set; }
        public int UpgradesEarned { get; private set; }
        public int BankedUpgrades { get; private set; }
        public int NextCost => CostOf(UpgradesEarned + 1);
        public float Fill01 => (float)Progress / NextCost;

        public int CostOf(int nth)
        {
            if (nth < 1) nth = 1;
            double cost = _baseCost * System.Math.Pow(_growth, nth - 1);
            return (int)System.Math.Round(cost, System.MidpointRounding.AwayFromZero);
        }

        /// <summary>Adds embers; returns how many upgrades were banked by this pickup.</summary>
        public int Add(int embers)
        {
            if (embers <= 0) return 0;
            TotalCollected += embers;
            Progress += embers;
            int banked = 0;
            while (Progress >= NextCost)
            {
                Progress -= NextCost;
                UpgradesEarned++;
                BankedUpgrades++;
                banked++;
            }
            return banked;
        }

        /// <summary>Consume one banked upgrade (wave-end selection). Returns false if none left.</summary>
        /// <summary>Banks upgrades that weren't earned with embers, so they don't raise the price of the next one.</summary>
        public void Grant(int upgrades)
        {
            if (upgrades > 0) BankedUpgrades += upgrades;
        }

        public bool ConsumeBanked()
        {
            if (BankedUpgrades <= 0) return false;
            BankedUpgrades--;
            return true;
        }

        public void Reset()
        {
            Progress = 0;
            TotalCollected = 0;
            UpgradesEarned = 0;
            BankedUpgrades = 0;
        }
    }
}
