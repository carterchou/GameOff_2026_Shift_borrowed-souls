using System;
using BorrowedSouls.Core.Combat;
using UnityEngine;

namespace BorrowedSouls.Data
{
    [Serializable]
    public struct WaveEntry
    {
        public VesselClass Class;
        public bool Elite;
        [Min(1)] public int Count;
    }

    /// <summary>
    /// One wave. Entries spawn in order in small batches; <see cref="MaxAlive"/> caps living
    /// attackers (corpses and possessed hosts do not count). Tutorial waves put their teaching enemy first.
    /// </summary>
    [CreateAssetMenu(menuName = "Borrowed Souls/Wave Definition", fileName = "WAVE_")]
    public sealed class WaveDefinition : ScriptableObject
    {
        [SerializeField] private string _title = "Wave";
        [SerializeField] private WaveEntry[] _entries = new WaveEntry[0];
        [SerializeField, Min(1)] private int _maxAlive = 3;
        [SerializeField, Min(0f)] private float _spawnInterval = 1.2f;
        [SerializeField] private bool _isBossWave;
        [Tooltip("Tutorial: the first enemy fights alone; the rest wait until it is killed or possessed.")]
        [SerializeField] private bool _firstEnemyAlone;
        [Tooltip("Short contextual hint shown when the wave starts (tutorial merged into waves 1-2).")]
        [SerializeField, TextArea] private string _hint = "";

        public string Title => _title;
        public WaveEntry[] Entries => _entries;
        public int MaxAlive => _maxAlive;
        public float SpawnInterval => _spawnInterval;
        public bool IsBossWave => _isBossWave;
        public bool FirstEnemyAlone => _firstEnemyAlone;
        public string Hint => _hint;

        public int TotalCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _entries.Length; i++) n += _entries[i].Count;
                return n;
            }
        }
    }
}
