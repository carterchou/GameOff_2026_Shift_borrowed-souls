namespace BorrowedSouls.Run
{
    public struct WaveStarted
    {
        public int WaveNumber;
        public int TotalWaves;
        public string Title;
        public string Hint;
    }

    public struct WaveCleared
    {
        public int WaveNumber;
        public float ElapsedSeconds;
    }

    public struct RunEnded
    {
        public bool Victory;
        public int WavesCleared;
        public float Seconds;
        public int Kills;
        public int Possessions;
    }

    /// <summary>Upgrade picked at wave end.</summary>
    public struct UpgradeChosen
    {
        public Core.Soul.UpgradeId Id;
        public int ResultingLevel;
    }
}
