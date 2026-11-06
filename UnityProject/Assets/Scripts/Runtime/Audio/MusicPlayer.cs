using BorrowedSouls.Core.Audio;
using BorrowedSouls.Data;
using UnityEngine;

namespace BorrowedSouls.Audio
{
    /// <summary>
    /// The music half of the AudioDirector (
    /// 2026-11-03-audio-candidates). Arena music is either the synthesized D-minor stems, layered by
    /// <see cref="MusicIntensity"/>, or — when the AudioBank lists licensed tracks — one full track per wave, crossfaded
    /// at each wave start and dimmed while things are calm. Boss and menu tracks crossfade on state changes.
    /// Plain C# so the director stays a thin event-to-sound map; the director owns the AudioSources.
    /// </summary>
    public sealed class MusicPlayer
    {
        private readonly AudioMixSettings _mix;
        private readonly System.Random _rng;
        private readonly AudioBank.MusicTrack[] _arenaTracks;
        private readonly AudioBank.MusicTrack[] _bossTracks;
        private readonly AudioBank.MusicTrack[] _menuTracks;
        private readonly bool _licensedArena;

        // Synthesized stems, or two decks that crossfade between licensed wave tracks.
        private readonly AudioSource[] _arena;
        private readonly float[] _arenaGain;
        private readonly float[] _arenaTrim; // per-track loudness trim (1 for synthesized stems)
        private int _liveDeck;
        private readonly AudioSource _boss, _menu;
        private float _bossGain, _menuGain;
        private float _bossTrim = 1f, _menuTrim = 1f;
        private MusicState _lastState = MusicState.Menu;

        public MusicPlayer(AudioMixSettings mix, AudioBank bank, System.Func<AudioSource> newSource, System.Random rng)
        {
            _mix = mix;
            _rng = rng;
            _arenaTracks = bank != null ? bank.ArenaTracks : System.Array.Empty<AudioBank.MusicTrack>();
            _bossTracks = bank != null ? bank.BossTracks : System.Array.Empty<AudioBank.MusicTrack>();
            _menuTracks = bank != null ? bank.MenuTracks : System.Array.Empty<AudioBank.MusicTrack>();
            _licensedArena = _arenaTracks.Length > 0;

            _arena = new AudioSource[_licensedArena ? 2 : ProceduralMusic.ArenaLayerCount];
            _arenaGain = new float[_arena.Length];
            _arenaTrim = new float[_arena.Length];
            for (int i = 0; i < _arena.Length; i++)
            {
                _arena[i] = newSource();
                _arenaTrim[i] = 1f;
                if (!_licensedArena) _arena[i].clip = ProceduralMusic.ArenaLayer(i);
            }
            _boss = newSource();
            if (_bossTracks.Length > 0) Load(_boss, Pick(_bossTracks), out _bossTrim);
            else _boss.clip = ProceduralMusic.Boss();
            _menu = newSource();
            if (_menuTracks.Length > 0) Load(_menu, Pick(_menuTracks), out _menuTrim);
            else _menu.clip = ProceduralMusic.Menu();
        }

        public void Start()
        {
            // Synthesized stems start on one DSP tick so they stay sample-locked while they fade in and out.
            double at = AudioSettings.dspTime + 0.1;
            if (!_licensedArena)
                for (int i = 0; i < _arena.Length; i++) _arena[i].PlayScheduled(at);
            _menu.PlayScheduled(at);
        }

        /// <summary>Licensed arena music: the wave's track (1-based wave, wrapping) takes over the other deck.</summary>
        public void OnWaveStarted(int waveNumber)
        {
            if (!_licensedArena) return;
            AudioBank.MusicTrack track = _arenaTracks[(Mathf.Max(1, waveNumber) - 1) % _arenaTracks.Length];
            if (_arena[_liveDeck].clip == track.Clip && _arena[_liveDeck].isPlaying) return;
            _liveDeck = 1 - _liveDeck;
            Load(_arena[_liveDeck], track, out _arenaTrim[_liveDeck]);
            _arena[_liveDeck].Play();
        }

        public void Update(in MusicContext context, float duck, float dt)
        {
            MusicState state = MusicIntensity.State(context);
            if (state != _lastState) Enter(state);
            _lastState = state;

            float volume = _mix.MusicVolume * duck;
            float trackStep = dt / _mix.TrackCrossfade;
            _menuGain = Fade(_menu, _menuGain, state == MusicState.Menu ? 1f : 0f, trackStep, volume * _menuTrim);
            float bossStep = state == MusicState.Victory ? dt / _mix.VictoryFade : trackStep;
            _bossGain = Fade(_boss, _bossGain, state == MusicState.Boss ? 1f : 0f, bossStep, volume * _bossTrim);

            bool arena = state == MusicState.Arena;
            float step = arena ? dt / _mix.LayerFade : trackStep;
            if (_licensedArena)
            {
                bool calm = MusicIntensity.Layers(context, 2, _mix.BusyEnemies, _mix.LowHp01) == 1;
                float live = arena ? (calm ? _mix.LicensedCalmGain : 1f) : 0f;
                for (int i = 0; i < _arena.Length; i++)
                    _arenaGain[i] = Fade(_arena[i], _arenaGain[i], i == _liveDeck ? live : 0f, trackStep, volume * _arenaTrim[i]);
                return;
            }

            int layers = MusicIntensity.Layers(context, _arena.Length, _mix.BusyEnemies, _mix.LowHp01);
            for (int i = 0; i < _arena.Length; i++)
                _arenaGain[i] = Fade(_arena[i], _arenaGain[i], arena && i < layers ? 1f : 0f, step, volume);
        }

        private void Enter(MusicState state)
        {
            // A new boss fight draws a fresh track; restart only from silence so a crossfade never jumps.
            if (state == MusicState.Boss && _bossGain <= 0f)
            {
                if (_bossTracks.Length > 0) Load(_boss, Pick(_bossTracks), out _bossTrim);
                _boss.Play();
            }
            if (state == MusicState.Menu && _menuGain <= 0f) _menu.Play();
        }

        private static float Fade(AudioSource source, float gain, float target, float step, float volume)
        {
            gain = Mathf.MoveTowards(gain, target, step);
            source.volume = volume * gain;
            return gain;
        }

        private AudioBank.MusicTrack Pick(AudioBank.MusicTrack[] tracks) => tracks[_rng.Next(tracks.Length)];

        private static void Load(AudioSource source, AudioBank.MusicTrack track, out float trim)
        {
            source.clip = track.Clip;
            trim = track.Volume;
        }
    }
}
