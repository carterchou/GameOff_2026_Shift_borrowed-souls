using UnityEngine;

namespace BorrowedSouls.Audio
{
    /// <summary>
    /// Grey-box music synthesised at startup, the fallback for empty AudioBank music slots
    ///.
    /// All loops are in D minor. The arena layers share tempo and length so they can be stacked and faded in sync.
    /// Every note ends inside the loop, so the seams don't click.
    /// </summary>
    public static class ProceduralMusic
    {
        private const int Rate = 22050;
        private const int Bars = 4;
        private const float ArenaBpm = 100f, BossBpm = 132f, MenuBpm = 72f;

        private readonly struct Chord
        {
            public readonly float Root; // octave-2 root, Hz
            public readonly int Third;  // 3 = minor, 4 = major

            public Chord(float root, int third)
            {
                Root = root;
                Third = third;
            }
        }

        // i – VI – III – V (Dm, B♭, F, A): dark but resolving, loops cleanly back to Dm.
        private static readonly Chord[] ArenaChords = { new Chord(73.42f, 3), new Chord(58.27f, 4), new Chord(87.31f, 4), new Chord(55f, 4) };
        // i – ♭II – VI – V (Dm, E♭, B♭, A): the Neapolitan E♭ adds the boss's menace.
        private static readonly Chord[] BossChords = { new Chord(73.42f, 3), new Chord(77.78f, 4), new Chord(58.27f, 4), new Chord(55f, 4) };
        private static readonly float[] MenuMelody = { 587.33f, 698.46f, 880f, 783.99f, 698.46f, 587.33f, 523.25f, 587.33f };

        public const int ArenaLayerCount = 3;
        // Each layer is normalised alone, then trimmed so the stack stays balanced: hats must not outshout the pad.
        private static readonly float[] ArenaLayerGain = { 0.8f, 0.9f, 0.45f };

        /// <summary>Arena layer 0 = ambience pad, 1 = bass + kick, 2 = snare + hats.</summary>
        public static AudioClip ArenaLayer(int layer)
        {
            float beat = 60f / ArenaBpm;
            float[] buf = Buffer(beat * 4f * Bars);
            var rng = new System.Random(11 + layer);
            for (int bar = 0; bar < Bars; bar++)
            {
                Chord c = ArenaChords[bar];
                float t0 = bar * beat * 4f;
                if (layer == 0) AddPad(buf, t0, beat * 4f, c, 0.5f);
                for (int b = 0; b < 4; b++)
                {
                    float tb = t0 + b * beat;
                    if (layer == 1)
                    {
                        if (b == 0 || b == 2) AddKick(buf, tb, 0.9f);
                        AddBass(buf, tb, beat * 0.45f, c.Root, 0.45f, 1f);
                        AddBass(buf, tb + beat * 0.5f, beat * 0.4f, c.Root * (b == 3 ? 1.5f : 1f), 0.35f, 1f);
                    }
                    else if (layer == 2)
                    {
                        if (b == 1 || b == 3) AddSnare(buf, tb, 0.6f, rng);
                        AddHat(buf, tb, 0.25f, rng);
                        AddHat(buf, tb + beat * 0.5f, 0.18f, rng);
                    }
                }
            }
            return Finish($"bgm_arena_{layer}", buf, ArenaLayerGain[layer]);
        }

        public static AudioClip Boss()
        {
            float beat = 60f / BossBpm;
            float[] buf = Buffer(beat * 4f * Bars);
            var rng = new System.Random(23);
            for (int bar = 0; bar < Bars; bar++)
            {
                Chord c = BossChords[bar];
                float t0 = bar * beat * 4f;
                AddPad(buf, t0, beat * 4f, c, 0.35f);
                for (int b = 0; b < 4; b++)
                {
                    float tb = t0 + b * beat;
                    AddKick(buf, tb, 1f);
                    if (b == 1 || b == 3) AddSnare(buf, tb, 0.7f, rng);
                    for (int s = 0; s < 4; s++) AddHat(buf, tb + s * beat * 0.25f, s == 0 ? 0.22f : 0.12f, rng);
                    for (int e = 0; e < 2; e++) AddBass(buf, tb + e * beat * 0.5f, beat * 0.42f, c.Root, 0.5f, 3f);
                }
            }
            return Finish("bgm_boss", buf, 1f);
        }

        public static AudioClip Menu()
        {
            float beat = 60f / MenuBpm;
            float[] buf = Buffer(beat * 4f * Bars);
            for (int bar = 0; bar < Bars; bar++)
            {
                float t0 = bar * beat * 4f;
                AddPad(buf, t0, beat * 4f, ArenaChords[bar], 0.6f);
                AddBell(buf, t0, MenuMelody[bar * 2], 0.22f, beat * 1.8f);
                AddBell(buf, t0 + beat * 2f, MenuMelody[bar * 2 + 1], 0.18f, beat * 1.8f);
            }
            return Finish("bgm_menu", buf, 1f);
        }

        private static float[] Buffer(float seconds) => new float[Mathf.RoundToInt(seconds * Rate)];

        private static void AddPad(float[] buf, float start, float duration, Chord c, float amp)
        {
            float third = c.Root * Mathf.Pow(2f, c.Third / 12f);
            float fifth = c.Root * 1.4983f;
            float fade = Mathf.Min(0.35f, duration * 0.25f);
            Each(buf, start, duration, (t, i) =>
            {
                float window = Mathf.Clamp01(t / fade) * Mathf.Clamp01((duration - t) / fade);
                float tremolo = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 0.5f * t);
                float tone = ProceduralSfx.Sine(c.Root, t) * 0.5f + ProceduralSfx.Sine(c.Root * 2f, t) * 0.3f
                             + ProceduralSfx.Sine(third * 2f, t) * 0.25f + ProceduralSfx.Sine(fifth * 2f, t) * 0.25f
                             + ProceduralSfx.Sine(c.Root * 4.004f, t) * 0.06f;
                return tone * window * tremolo * amp;
            });
        }

        private static void AddKick(float[] buf, float start, float amp) => Each(buf, start, 0.22f, (t, i) =>
            ProceduralSfx.Sine(Mathf.Lerp(110f, 42f, Mathf.Clamp01(t / 0.08f)), t) * ProceduralSfx.Env(t, 0.002f, 0.2f) * amp);

        private static void AddBass(float[] buf, float start, float duration, float freq, float amp, float drive) => Each(buf, start, duration, (t, i) =>
        {
            float raw = ProceduralSfx.Sine(freq, t) + ProceduralSfx.Sine(freq * 2f, t) * 0.3f;
            float shaped = drive > 1f ? (float)System.Math.Tanh(raw * drive) : raw;
            return shaped * ProceduralSfx.Env(t, 0.005f, duration) * amp;
        });

        private static void AddSnare(float[] buf, float start, float amp, System.Random rng) => Each(buf, start, 0.16f, (t, i) =>
            (ProceduralSfx.Noise(rng) * 0.7f + ProceduralSfx.Sine(190f, t) * 0.4f) * ProceduralSfx.Env(t, 0.001f, 0.14f) * amp);

        private static void AddHat(float[] buf, float start, float amp, System.Random rng)
        {
            float previous = 0f;
            Each(buf, start, 0.05f, (t, i) =>
            {
                float n = ProceduralSfx.Noise(rng);
                float high = n - previous; // first difference: a crude high-pass so it hisses instead of rumbles
                previous = n;
                return high * ProceduralSfx.Env(t, 0.0005f, 0.045f) * amp;
            });
        }

        private static void AddBell(float[] buf, float start, float freq, float amp, float ring) => Each(buf, start, ring, (t, i) =>
            (ProceduralSfx.Sine(freq, t) * 0.6f + ProceduralSfx.Sine(freq * 2.76f, t) * 0.15f) * ProceduralSfx.Env(t, 0.004f, ring) * amp);

        private delegate float Voice(float t, int index);

        private static void Each(float[] buf, float start, float duration, Voice voice)
        {
            int from = Mathf.RoundToInt(start * Rate);
            int to = Mathf.Min(buf.Length, from + Mathf.RoundToInt(duration * Rate));
            for (int i = from; i < to; i++) buf[i] += voice((float)(i - from) / Rate, i);
        }

        private static AudioClip Finish(string name, float[] buf, float gain)
        {
            float peak = 0.0001f;
            for (int i = 0; i < buf.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(buf[i]));
            float norm = 0.89f * gain / peak;
            for (int i = 0; i < buf.Length; i++) buf[i] *= norm;
            var clip = AudioClip.Create(name, buf.Length, 1, Rate, false);
            clip.SetData(buf, 0);
            return clip;
        }
    }
}
