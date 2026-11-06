using BorrowedSouls.Data;
using UnityEngine;

namespace BorrowedSouls.Audio
{
    /// <summary>
    /// Grey-box sound effects synthesised at startup (no asset licences, safe for the public repo).
    /// The fallback for every empty AudioBank slot. Each cue has a few variants (different noise seed and a slight
    /// detune,); melodic cues sit in D minor like the music.
    /// </summary>
    public static class ProceduralSfx
    {
        private const int Rate = 44100;
        private const float D5 = 587.33f, F5 = 698.46f, G5 = 783.99f, A5 = 880f, C6 = 1046.5f, D6 = 1174.66f;

        /// <summary>Length of the rising telegraph clip; the director pitches it so it ends when the attack lands (A7).</summary>
        public const float TelegraphSeconds = 0.5f;

        /// <summary>Matches BossBrain's ring telegraph, the wait before the attack itself starts.</summary>
        public const float RingChargeSeconds = 0.6f;

        /// <summary>The ring's 1.1 s of travel, and the ground settling after.</summary>
        public const float RingRumbleSeconds = 1.4f;

        public static AudioClip[] Variants(SfxCue cue, int count)
        {
            var clips = new AudioClip[Mathf.Max(1, count)];
            for (int v = 0; v < clips.Length; v++) clips[v] = Create(cue, v);
            return clips;
        }

        private static AudioClip Create(SfxCue cue, int v)
        {
            float d = 1f + 0.035f * (v - 1); // detune per variant: -3.5 %, 0, +3.5 %, +7 %
            switch (cue)
            {
                case SfxCue.SwingLight: return Swing(cue, v, d);
                case SfxCue.SwingHeavy: return Swing(cue, v, 0.6f * d);
                case SfxCue.BowRelease: return BowRelease(cue, v, d);
                case SfxCue.FireCast: return FireCast(cue, v, d);
                case SfxCue.Dodge: return Dodge(cue, v, d);
                case SfxCue.HitTransient: return HitTransient(cue, v, d);
                case SfxCue.HitMetal: return HitMetal(cue, v, d);
                case SfxCue.HitWood: return HitWood(cue, v, d);
                case SfxCue.HitFire: return HitFire(cue, v, d);
                case SfxCue.HitSub: return HitSub(cue, v, d);
                case SfxCue.PlayerHurt: return PlayerHurt(cue, v, d);
                case SfxCue.KillChime: return KillChime(cue, v, d);
                case SfxCue.Death: return Death(cue, v, d);
                case SfxCue.SoulExit: return SoulExit(cue, v);
                case SfxCue.SoulEnter: return SoulEnter(cue, v);
                case SfxCue.SoulExpire: return SoulExpire(cue, v);
                case SfxCue.Ember: return Ember(cue, v);
                case SfxCue.Upgrade: return Arpeggio(cue, v, new[] { D5, F5, A5, D6 }, 0.09f, 0.35f);
                case SfxCue.Telegraph: return TelegraphRise(cue, v);
                case SfxCue.Heartbeat: return Heartbeat(cue, v);
                case SfxCue.WaveCleared: return Arpeggio(cue, v, new[] { D5, F5, G5, A5, C6, D6 }, 0.07f, 0.5f);
                case SfxCue.RunVictory: return Arpeggio(cue, v, new[] { D5, A5, D6, F5 * 2f, A5 * 2f }, 0.16f, 1.2f);
                case SfxCue.RunDefeat: return Arpeggio(cue, v, new[] { A5 / 2f, F5 / 2f, D5 / 2f, D5 / 4f }, 0.22f, 1.2f);
                case SfxCue.RingCharge: return RingCharge(cue, v);
                case SfxCue.RingSlam: return RingSlam(cue, v, d);
                case SfxCue.RingRumble: return RingRumble(cue, v);
                default: return Make(cue, v, 0.05f, (t, r) => 0f);
            }
        }

        private static AudioClip Swing(SfxCue cue, int v, float pitch) => Make(cue, v, 0.14f, (t, r) =>
            Noise(r) * Env(t, 0.01f, 0.13f) * 0.55f * Band(t, 900f * pitch, 2600f * pitch, 0.14f));

        private static AudioClip BowRelease(SfxCue cue, int v, float d) => Make(cue, v, 0.16f, (t, r) =>
            (Sine(220f * d, t) * 0.5f * Env(t, 0.001f, 0.06f) + Noise(r) * 0.35f) * Env(t, 0.001f, 0.15f));

        private static AudioClip FireCast(SfxCue cue, int v, float d) => Make(cue, v, 0.3f, (t, r) =>
        {
            float crackle = r.NextDouble() < 0.02 ? 1f : 0f;
            return (Noise(r) * 0.4f + crackle * 0.8f + Sine(70f * d, t) * 0.3f) * Env(t, 0.03f, 0.27f);
        });

        private static AudioClip Dodge(SfxCue cue, int v, float d) => Make(cue, v, 0.2f, (t, r) =>
            Noise(r) * 0.4f * Env(t, 0.02f, 0.18f) * Band(t, 400f * d, 1800f * d, 0.2f));

        // A2 hit layers: transient ("it connected") + weapon body + optional sub for weight.
        private static AudioClip HitTransient(SfxCue cue, int v, float d) => Make(cue, v, 0.025f, (t, r) =>
            (Noise(r) * 0.8f + Sine(3200f * d, t) * 0.5f) * Env(t, 0.0003f, 0.012f));

        private static AudioClip HitMetal(SfxCue cue, int v, float d) => Make(cue, v, 0.22f, (t, r) =>
        {
            float ring = Sine(1250f * d, t) * 0.4f + Sine(1870f * d, t) * 0.3f + Sine(2650f * d, t) * 0.2f;
            return ring * Env(t, 0.001f, 0.2f) + Noise(r) * 0.6f * Env(t, 0.001f, 0.05f);
        });

        private static AudioClip HitWood(SfxCue cue, int v, float d) => Make(cue, v, 0.14f, (t, r) =>
        {
            float f = Mathf.Lerp(320f, 170f, t / 0.14f) * d;
            return Sine(f, t) * 0.8f * Env(t, 0.001f, 0.12f) + Noise(r) * 0.5f * Env(t, 0.001f, 0.03f);
        });

        private static AudioClip HitFire(SfxCue cue, int v, float d) => Make(cue, v, 0.28f, (t, r) =>
        {
            float crackle = r.NextDouble() < 0.035 ? 1f : 0f;
            return Noise(r) * 0.55f * Env(t, 0.002f, 0.2f) + crackle * 0.7f * Env(t, 0.01f, 0.26f)
                   + Sine(Mathf.Lerp(120f, 60f, t / 0.28f) * d, t) * 0.5f * Env(t, 0.003f, 0.22f);
        });

        private static AudioClip HitSub(SfxCue cue, int v, float d) => Make(cue, v, 0.3f, (t, r) =>
            Sine(Mathf.Lerp(65f, 38f, t / 0.3f) * d, t) * Env(t, 0.004f, 0.28f));

        /// <summary>A1 player hurt: a dull low thud plus a short high "sting", the most urgent sound in the mix.</summary>
        private static AudioClip PlayerHurt(SfxCue cue, int v, float d) => Make(cue, v, 0.24f, (t, r) =>
        {
            float thud = Sine(Mathf.Lerp(120f, 55f, t / 0.24f) * d, t) * 0.9f * Env(t, 0.002f, 0.22f);
            float sting = (Sine(2900f * d, t) * 0.5f + Noise(r) * 0.3f) * Env(t, 0.0005f, 0.045f);
            return thud + sting;
        });

        /// <summary>A3 kill confirm: a short bell on D with an inharmonic partial, over a glassy crack.</summary>
        private static AudioClip KillChime(SfxCue cue, int v, float d) => Make(cue, v, 0.55f, (t, r) =>
        {
            float bell = Sine(D6 * d, t) * 0.5f + Sine(D6 * 2.76f * d, t) * 0.2f * Env(t, 0.001f, 0.2f);
            return bell * Env(t, 0.001f, 0.52f) + Noise(r) * 0.4f * Env(t, 0.0005f, 0.03f);
        });

        private static AudioClip Death(SfxCue cue, int v, float d) => Make(cue, v, 0.4f, (t, r) =>
            (Sine(Mathf.Lerp(90f, 40f, t / 0.4f) * d, t) * 0.8f + Noise(r) * 0.3f * Env(t, 0.001f, 0.1f)) * Env(t, 0.002f, 0.38f));

        private static AudioClip SoulExit(SfxCue cue, int v) => Make(cue, v, 0.6f, (t, r) =>
        {
            float f = Mathf.Lerp(A5 * 0.75f, A5 / 4f, t / 0.6f);
            return (Sine(f, t) * 0.45f + Sine(f * 2.01f, t) * 0.2f + Noise(r) * 0.08f) * Env(t, 0.02f, 0.55f);
        });

        private static AudioClip SoulEnter(SfxCue cue, int v) => Make(cue, v, 0.45f, (t, r) =>
        {
            float f = Mathf.Lerp(A5 / 4f, A5, Mathf.Clamp01(t / 0.2f));
            return (Sine(f, t) * 0.5f + Sine(D6, t) * 0.18f * Env(t, 0.15f, 0.3f)) * Env(t, 0.005f, 0.42f);
        });

        private static AudioClip SoulExpire(SfxCue cue, int v) => Make(cue, v, 1.0f, (t, r) =>
            (Sine(Mathf.Lerp(330f, 70f, t), t) * 0.5f + Noise(r) * 0.15f * (1f - t)) * Env(t, 0.01f, 0.95f));

        /// <summary>Ember root on D6; the director climbs the minor pentatonic from here (A6).</summary>
        private static AudioClip Ember(SfxCue cue, int v) => Make(cue, v, 0.12f, (t, r) =>
            (Sine(D6, t) * 0.45f + Sine(D6 * 2f, t) * 0.15f) * Env(t, 0.002f, 0.11f));

        /// <summary>A7: low growl whose pitch and loudness rise and stop dead at the end, so the player hears when to dodge.</summary>
        private static AudioClip TelegraphRise(SfxCue cue, int v) => Make(cue, v, TelegraphSeconds, (t, r) =>
        {
            float k = t / TelegraphSeconds;
            float f = Mathf.Lerp(98f, 294f, k * k);
            float tail = k > 0.97f ? (1f - k) / 0.03f : 1f;
            return (Sine(f, t) * 0.6f + Sine(f * 1.5f, t) * 0.3f) * (0.25f + 0.75f * k) * tail;
        });

        /// <summary>The boss drawing power in, over the part of the charge the telegraph's rise doesn't cover: air rushing inward, growing.</summary>
        private static AudioClip RingCharge(SfxCue cue, int v) => Make(cue, v, RingChargeSeconds, (t, r) =>
        {
            float k = t / RingChargeSeconds;
            float wobble = 0.65f + 0.35f * Sine(Mathf.Lerp(5f, 16f, k), t);
            return (Noise(r) * 0.35f * wobble + Sine(Mathf.Lerp(70f, 150f, k), t) * 0.5f) * k * k;
        });

        /// <summary>The boss hitting the ground. A crack and a mid-range body as well as the low boom, since laptop speakers lose everything under about 100 Hz.</summary>
        private static AudioClip RingSlam(SfxCue cue, int v, float d) => Make(cue, v, 0.9f, (t, r) =>
        {
            float boom = Sine(Mathf.Lerp(85f, 34f, Mathf.Clamp01(t / 0.5f)) * d, t) * Env(t, 0.003f, 0.85f);
            float body = (Sine(190f * d, t) * 0.5f + Sine(287f * d, t) * 0.35f) * Env(t, 0.002f, 0.28f);
            float crack = Noise(r) * 0.9f * Env(t, 0.0005f, 0.09f);
            return boom + body + crack;
        });

        /// <summary>The ground breaking as the wave crosses it: a low roar with rocks cracking through it. As long as the wave and its settling.</summary>
        private static AudioClip RingRumble(SfxCue cue, int v)
        {
            float low = 0f;
            float grain = 0f;
            float grainTone = 500f;
            return Make(cue, v, RingRumbleSeconds, (t, r) =>
            {
                float n = Noise(r);
                low += (n - low) * 0.035f; // a one-pole low-pass: roar, not hiss
                if (r.NextDouble() < 0.0007)
                {
                    grain = 1f; // a rock breaking
                    grainTone = 280f + (float)r.NextDouble() * 900f;
                }
                grain *= 0.9986f;
                float crack = (n * 0.55f + Sine(grainTone, t) * 0.45f) * grain;
                float shape = Mathf.Clamp01(t / 0.04f) * Mathf.Clamp01((RingRumbleSeconds - t) / 0.35f);
                return (low * 3.2f + crack * 0.7f + Sine(48f, t) * 0.3f) * shape;
            });
        }

        private static AudioClip Heartbeat(SfxCue cue, int v) => Make(cue, v, 0.35f, (t, r) =>
        {
            float a = Env(t, 0.005f, 0.12f);
            float b = t > 0.16f ? Env(t - 0.16f, 0.005f, 0.14f) : 0f;
            return Sine(55f, t) * (a + b * 0.8f) * 0.9f;
        });

        private static AudioClip Arpeggio(SfxCue cue, int v, float[] notes, float step, float ring)
        {
            float length = step * (notes.Length - 1) + ring;
            return Make(cue, v, length, (t, r) =>
            {
                float sum = 0f;
                for (int i = 0; i < notes.Length; i++)
                {
                    float local = t - i * step;
                    if (local < 0f) break;
                    sum += (Sine(notes[i], local) * 0.4f + Sine(notes[i] * 2f, local) * 0.1f) * Env(local, 0.005f, ring);
                }
                return sum;
            });
        }

        private delegate float Generator(float t, System.Random rng);

        private static AudioClip Make(SfxCue cue, int variant, float seconds, Generator gen)
        {
            int length = Mathf.RoundToInt(seconds * Rate);
            var data = new float[length];
            var rng = new System.Random((int)cue * 97 + variant);
            float peak = 0.0001f;
            for (int i = 0; i < length; i++)
            {
                data[i] = gen((float)i / Rate, rng);
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }
            float norm = 0.89f / peak; // normalise to about -1 dBFS (Unity 準則 §七)
            for (int i = 0; i < length; i++) data[i] *= norm;
            var clip = AudioClip.Create($"sfx_{cue}_{variant}", length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        internal static float Env(float t, float attack, float release)
        {
            if (t < attack) return t / attack;
            float d = (t - attack) / Mathf.Max(0.0001f, release);
            return Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d);
        }

        internal static float Sine(float frequency, float t) => Mathf.Sin(2f * Mathf.PI * frequency * t);

        internal static float Noise(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

        private static float Band(float t, float from, float to, float length)
        {
            // Cheap "whoosh" colouring: amplitude wobble whose rate sweeps from -> to.
            float f = Mathf.Lerp(from, to, Mathf.Clamp01(t / length));
            return 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * f * t * 0.01f);
        }
    }
}
