using UnityEngine;

namespace BorrowedSouls.Bootstrap
{
    /// <summary>
    /// Player-facing options. Persisted in PlayerPrefs; the tuning
    /// ScriptableObjects are never modified at runtime.
    /// </summary>
    public sealed class GameOptions
    {
        private const string Prefix = "BS.Options.";
        private static readonly float[] ShakeScales = { 0f, 0.5f, 1f, 1.5f };
        private static readonly string[] ShakeNames = { "關", "低", "標準", "高" };

        public int ShakeLevel { get; private set; } = 2;
        public bool ReduceFlashes { get; private set; }
        public bool TutorialHints { get; private set; } = true;
        public bool DamageNumbers { get; private set; } = true;
        public float MasterVolume { get; private set; } = 0.8f;

        public float ShakeScale => ShakeScales[Mathf.Clamp(ShakeLevel, 0, ShakeScales.Length - 1)];
        public string ShakeName => ShakeNames[Mathf.Clamp(ShakeLevel, 0, ShakeNames.Length - 1)];

        public static GameOptions Load()
        {
            var o = new GameOptions
            {
                ShakeLevel = PlayerPrefs.GetInt(Prefix + "Shake", 2),
                ReduceFlashes = PlayerPrefs.GetInt(Prefix + "ReduceFlashes", 0) == 1,
                TutorialHints = PlayerPrefs.GetInt(Prefix + "Hints", 1) == 1,
                DamageNumbers = PlayerPrefs.GetInt(Prefix + "DamageNumbers", 1) == 1,
                MasterVolume = PlayerPrefs.GetFloat(Prefix + "Volume", 0.8f)
            };
            o.Apply();
            return o;
        }

        public void CycleShake()
        {
            ShakeLevel = (ShakeLevel + 1) % ShakeScales.Length;
            Save();
        }

        public void ToggleReduceFlashes()
        {
            ReduceFlashes = !ReduceFlashes;
            Save();
        }

        public void ToggleHints()
        {
            TutorialHints = !TutorialHints;
            Save();
        }

        public void ToggleDamageNumbers()
        {
            DamageNumbers = !DamageNumbers;
            Save();
        }

        public void CycleVolume()
        {
            MasterVolume = MasterVolume >= 0.99f ? 0f : Mathf.Min(1f, MasterVolume + 0.2f);
            Save();
        }

        private void Save()
        {
            PlayerPrefs.SetInt(Prefix + "Shake", ShakeLevel);
            PlayerPrefs.SetInt(Prefix + "ReduceFlashes", ReduceFlashes ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "Hints", TutorialHints ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "DamageNumbers", DamageNumbers ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "Volume", MasterVolume);
            PlayerPrefs.Save();
            Apply();
        }

        private void Apply()
        {
            AudioListener.volume = MasterVolume;
        }
    }
}
