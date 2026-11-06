using System;
using BorrowedSouls.Core.Combat;
using UnityEngine;

namespace BorrowedSouls.Data
{
    /// <summary>Every sound effect the AudioDirector can play. Unity serializes enums as ints, so append only.</summary>
    public enum SfxCue
    {
        SwingLight,
        SwingHeavy,
        BowRelease,
        FireCast,
        Dodge,
        HitTransient,
        HitMetal,
        HitWood,
        HitFire,
        HitSub,
        PlayerHurt,
        KillChime,
        Death,
        SoulExit,
        SoulEnter,
        SoulExpire,
        Ember,
        Upgrade,
        Telegraph,
        Heartbeat,
        WaveCleared,
        RunVictory,
        RunDefeat,
        RingCharge,
        RingSlam,
        RingRumble
    }

    /// <summary>
    /// A body's voice lines. Voices belong to the body, not the soul: borrowing a swordsman's body borrows its voice,
    /// and the same voice cries out when a swordsman enemy falls.
    /// </summary>
    public enum VoiceLine
    {
        Attack,
        Hurt,
        Death
    }

    /// <summary>
    /// Recorded / licensed clips per cue.
    /// Third-party files live in the git-ignored Assets/Audio/ThirdParty/ and never reach the public repo, so every
    /// slot may be empty: an empty SFX slot or music track falls back to ProceduralSfx / ProceduralMusic, which keeps a
    /// fresh clone buildable and playable. Several clips in one slot are played as variants.
    /// </summary>
    [CreateAssetMenu(menuName = "Borrowed Souls/Audio Bank", fileName = "AudioBank")]
    public sealed class AudioBank : ScriptableObject
    {
        [Serializable]
        public struct SfxSlot
        {
            public SfxCue Cue;
            public AudioClip[] Clips;
        }

        /// <summary>A licensed music track with a loudness trim, so tracks mastered at different levels play alike.</summary>
        [Serializable]
        public struct MusicTrack
        {
            public AudioClip Clip;
            [Tooltip("Loudness trim (dB, ≤ 0). ThirdPartyAudio fills it from ffmpeg ebur128 measurements.")]
            public float GainDb;

            public float Volume => Mathf.Pow(10f, GainDb / 20f);
        }

        [Serializable]
        public struct ClassVoice
        {
            public VesselClass Class;
            public AudioClip[] Attack;
            public AudioClip[] Hurt;
            public AudioClip[] Death;
        }

        [SerializeField] private SfxSlot[] _sfx = Array.Empty<SfxSlot>();

        [Header("Voices (no synthesized fallback: empty = silent)")]
        [SerializeField] private ClassVoice[] _classVoices = Array.Empty<ClassVoice>();
        [SerializeField] private AudioClip[] _bossPhaseVoice = Array.Empty<AudioClip>();

        [Header("Music (licensed full tracks; empty lists fall back to ProceduralMusic)")]
        [Tooltip("One track per wave in order, wrapping. Replaces the synthesized layered arena loop.")]
        [SerializeField] private MusicTrack[] _arenaTracks = Array.Empty<MusicTrack>();
        [Tooltip("One is picked at random each time a boss appears.")]
        [SerializeField] private MusicTrack[] _bossTracks = Array.Empty<MusicTrack>();
        [SerializeField] private MusicTrack[] _menuTracks = Array.Empty<MusicTrack>();

        public MusicTrack[] ArenaTracks => WithClips(_arenaTracks);
        public MusicTrack[] BossTracks => WithClips(_bossTracks);
        public MusicTrack[] MenuTracks => WithClips(_menuTracks);
        public AudioClip[] BossPhaseVoice => NonEmpty(_bossPhaseVoice);

        /// <summary>Licensed clips for <paramref name="cue"/>; empty when the slot is unset. Called once per cue at startup.</summary>
        public AudioClip[] Clips(SfxCue cue)
        {
            for (int i = 0; i < _sfx.Length; i++)
                if (_sfx[i].Cue == cue) return NonEmpty(_sfx[i].Clips);
            return Array.Empty<AudioClip>();
        }

        /// <summary>Voice clips for a body of class <paramref name="vesselClass"/>; empty when unset.</summary>
        public AudioClip[] Voice(VesselClass vesselClass, VoiceLine line)
        {
            for (int i = 0; i < _classVoices.Length; i++)
            {
                if (_classVoices[i].Class != vesselClass) continue;
                ClassVoice v = _classVoices[i];
                return NonEmpty(line == VoiceLine.Attack ? v.Attack : line == VoiceLine.Hurt ? v.Hurt : v.Death);
            }
            return Array.Empty<AudioClip>();
        }

        private static MusicTrack[] WithClips(MusicTrack[] tracks)
        {
            if (tracks == null) return Array.Empty<MusicTrack>();
            return Array.FindAll(tracks, t => t.Clip != null); // startup only
        }

        private static AudioClip[] NonEmpty(AudioClip[] clips)
        {
            if (clips == null) return Array.Empty<AudioClip>();
            int count = 0;
            for (int i = 0; i < clips.Length; i++) if (clips[i] != null) count++;
            if (count == clips.Length) return clips;
            var result = new AudioClip[count];
            for (int i = 0, j = 0; i < clips.Length; i++) if (clips[i] != null) result[j++] = clips[i];
            return result;
        }
    }
}
