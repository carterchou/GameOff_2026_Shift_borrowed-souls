using UnityEngine;

namespace BorrowedSouls.Data
{
    /// <summary>
    /// Mix, routing and music tuning for the AudioDirector
    ///.
    /// Not named AudioSettings to avoid clashing with UnityEngine.AudioSettings.
    /// </summary>
    [CreateAssetMenu(menuName = "Borrowed Souls/Audio Mix Settings", fileName = "AudioMixSettings")]
    public sealed class AudioMixSettings : ScriptableObject
    {
        [Header("Buses")]
        [SerializeField, Range(0f, 1f)] private float _sfxVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float _musicVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] private float _voiceVolume = 0.8f;
        [Tooltip("Synthesized variants per fallback SFX cue (A5: never the same clip twice in a row).")]
        [SerializeField, Range(1, 6)] private int _proceduralVariants = 4;

        [Header("Cue volumes")]
        [SerializeField, Range(0f, 1f)] private float _playerSwingVolume = 0.6f;
        [SerializeField, Range(0f, 1f)] private float _enemySwingVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _deathVolume = 0.6f;
        [SerializeField, Range(0f, 1f)] private float _soulVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float _emberVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _uiVolume = 0.6f;
        [Tooltip("Ember chains climb the minor pentatonic up to this step (5 steps = one octave).")]
        [SerializeField, Range(0, 15)] private int _emberMaxStep = 10;

        [Header("Hit routing (A1–A3)")]
        [SerializeField, Range(0f, 1f)] private float _playerDealtVolume = 0.75f;
        [SerializeField, Range(0f, 1f)] private float _playerHurtVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float _bystanderVolume = 0.3f;
        [Tooltip("The 'got it' click on top of each player hit, relative to the hit body.")]
        [SerializeField, Range(0f, 1f)] private float _transientVolume = 0.7f;
        [Tooltip("Kill-confirm chime lands this long after the killing hit (A3).")]
        [SerializeField, Min(0f)] private float _killChimeDelay = 0.06f;
        [SerializeField, Range(0f, 0.2f)] private float _pitchJitter = 0.06f;

        [Header("Combo pitch (A6)")]
        [SerializeField, Min(0.05f)] private float _comboWindow = 0.8f;
        [SerializeField, Range(0, 8)] private int _comboMaxStep = 4;
        [SerializeField, Range(0f, 3f)] private float _comboSemitonesPerStep = 1f;

        [Header("Ducking and soul low-pass (A4)")]
        [Tooltip("Music dip during hit stop on strong hits, kills and boss hits.")]
        [SerializeField, Range(-24f, 0f)] private float _duckDb = -7.5f;
        [SerializeField, Min(0f)] private float _duckHold = 0.08f;
        [SerializeField, Min(0.01f)] private float _duckRelease = 0.15f;
        [Tooltip("Everything is muffled to this cutoff while the soul flies (the world 'moves away').")]
        [SerializeField, Min(200f)] private float _soulLowPassHz = 1100f;
        [SerializeField, Min(0.01f)] private float _lowPassSweep = 0.25f;

        [Header("Telegraph (A7)")]
        [SerializeField, Range(0f, 1f)] private float _telegraphVolume = 0.45f;
        [Header("The boss's ring attack")]
        [SerializeField, Range(0f, 1f)] private float _ringChargeVolume = 0.6f;
        [SerializeField, Range(0f, 1f)] private float _ringSlamVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float _ringRumbleVolume = 0.75f;
        [Tooltip("Seconds at least between the boss shouting as it charges, so it doesn't wear thin.")]
        [SerializeField, Min(0f)] private float _bossShoutCooldown = 14f;
        [SerializeField] private Vector2 _telegraphPitchRange = new Vector2(0.5f, 2.5f);

        [Header("Voices")]
        [Tooltip("Chance of a shout on the player's strong / special attacks (every swing gets tiring).")]
        [SerializeField, Range(0f, 1f)] private float _attackShoutChance = 0.35f;
        [SerializeField, Min(0f)] private float _hurtVoiceCooldown = 0.6f;

        [Header("Music (B)")]
        [SerializeField, Min(0.05f)] private float _layerFade = 1.5f;
        [SerializeField, Min(0.05f)] private float _trackCrossfade = 2f;
        [Tooltip("Seconds the boss track takes to die away once the run is won.")]
        [SerializeField, Min(0.05f)] private float _victoryFade = 4.5f;
        [Tooltip("Enemies left in the wave at which the top layer joins.")]
        [SerializeField, Min(1)] private int _busyEnemies = 5;
        [Tooltip("Player body HP fraction at or below which the top layer joins.")]
        [SerializeField, Range(0f, 1f)] private float _lowHp01 = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _stingerVolume = 0.7f;
        [Tooltip("A licensed arena track can't drop layers, so between waves / while choosing it just plays this much quieter.")]
        [SerializeField, Range(0f, 1f)] private float _licensedCalmGain = 0.55f;

        public float SfxVolume => _sfxVolume;
        public float MusicVolume => _musicVolume;
        public float VoiceVolume => _voiceVolume;
        public int ProceduralVariants => _proceduralVariants;
        public float PlayerSwingVolume => _playerSwingVolume;
        public float EnemySwingVolume => _enemySwingVolume;
        public float DeathVolume => _deathVolume;
        public float SoulVolume => _soulVolume;
        public float EmberVolume => _emberVolume;
        public float UiVolume => _uiVolume;
        public int EmberMaxStep => _emberMaxStep;
        public float PlayerDealtVolume => _playerDealtVolume;
        public float PlayerHurtVolume => _playerHurtVolume;
        public float BystanderVolume => _bystanderVolume;
        public float TransientVolume => _transientVolume;
        public float KillChimeDelay => _killChimeDelay;
        public float PitchJitter => _pitchJitter;
        public float ComboWindow => _comboWindow;
        public int ComboMaxStep => _comboMaxStep;
        public float ComboSemitonesPerStep => _comboSemitonesPerStep;
        public float DuckDb => _duckDb;
        public float DuckHold => _duckHold;
        public float DuckRelease => _duckRelease;
        public float SoulLowPassHz => _soulLowPassHz;
        public float LowPassSweep => _lowPassSweep;
        public float TelegraphVolume => _telegraphVolume;
        public float RingChargeVolume => _ringChargeVolume;
        public float RingSlamVolume => _ringSlamVolume;
        public float RingRumbleVolume => _ringRumbleVolume;
        public float BossShoutCooldown => _bossShoutCooldown;
        public Vector2 TelegraphPitchRange => _telegraphPitchRange;
        public float AttackShoutChance => _attackShoutChance;
        public float HurtVoiceCooldown => _hurtVoiceCooldown;
        public float LayerFade => _layerFade;
        public float TrackCrossfade => _trackCrossfade;
        public float VictoryFade => _victoryFade;
        public int BusyEnemies => _busyEnemies;
        public float LowHp01 => _lowHp01;
        public float StingerVolume => _stingerVolume;
        public float LicensedCalmGain => _licensedCalmGain;
    }
}
