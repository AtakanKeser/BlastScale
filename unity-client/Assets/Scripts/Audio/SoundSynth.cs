using System;
using UnityEngine;

namespace BlastScale.Client.Audio
{
    /// <summary>
    /// Synthesises every sound of the game from scratch at startup (no audio files in the repo):
    /// short UI effects at 44.1 kHz and a 16 second ambient music loop at 22.05 kHz. Each clip is a
    /// sum of simple oscillators / filtered noise shaped by an ADSR envelope, normalised and
    /// soft-clipped so nothing clicks or distorts.
    /// </summary>
    public static class SoundSynth
    {
        private const int SfxRate = 44100;
        private const int MusicRate = 22050;

        // ------------------------------------------------------------------ sound effects

        /// <summary>Short soft tick for buttons: a fast decaying high sine with a touch of noise.</summary>
        public static AudioClip UiClick()
        {
            return Render("ui_click", 0.06f, SfxRate, (t, rng) =>
            {
                float env = Adsr(t, 0.06f, 0.002f, 0.03f, 0.0f, 0.02f);
                float tone = Mathf.Sin(2f * Mathf.PI * 1900f * t) * 0.8f + Mathf.Sin(2f * Mathf.PI * 3800f * t) * 0.2f;
                float noise = (rng() * 2f - 1f) * Mathf.Exp(-t * 300f) * 0.4f;
                return (tone + noise) * env;
            });
        }

        /// <summary>
        /// Block pop: a sine that sweeps down quickly plus a noise burst. The AudioSource pitch is
        /// raised with the group size at play time so bigger groups sound brighter.
        /// </summary>
        public static AudioClip Pop()
        {
            return Render("pop", 0.2f, SfxRate, (t, rng) =>
            {
                float env = Adsr(t, 0.2f, 0.003f, 0.12f, 0.15f, 0.06f);
                float freq = Mathf.Lerp(1100f, 320f, 1f - Mathf.Exp(-t * 28f));
                float phase = 2f * Mathf.PI * (320f * t + (1100f - 320f) / 28f * (1f - Mathf.Exp(-t * 28f)));
                float tone = Mathf.Sin(phase) * 0.9f + Mathf.Sin(phase * 2f) * 0.15f;
                float noise = (rng() * 2f - 1f) * Mathf.Exp(-t * 90f) * 0.5f;
                _ = freq;
                return (tone + noise) * env;
            });
        }

        /// <summary>Invalid tap: a low, slightly rough two-tone buzz.</summary>
        public static AudioClip Invalid()
        {
            return Render("invalid", 0.28f, SfxRate, (t, rng) =>
            {
                float pulse = t < 0.12f ? Adsr(t, 0.12f, 0.004f, 0.02f, 0.7f, 0.04f)
                    : t > 0.14f ? Adsr(t - 0.14f, 0.14f, 0.004f, 0.02f, 0.7f, 0.06f) : 0f;
                float f = t < 0.13f ? 180f : 140f;
                float tone = Mathf.Sin(2f * Mathf.PI * f * t) + 0.35f * Mathf.Sin(2f * Mathf.PI * f * 3f * t) + 0.15f * Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * 2f * t));
                return tone * pulse * 0.6f;
            });
        }

        /// <summary>Screen transition: filtered noise whose brightness rises then falls.</summary>
        public static AudioClip Whoosh()
        {
            float[] samples = new float[(int)(SfxRate * 0.32f)];
            var rng = new System.Random(7);
            float low = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)SfxRate;
                float p = i / (float)samples.Length;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                // One-pole low-pass whose cutoff follows a bell curve: dull -> bright -> dull.
                float cutoff = Mathf.Lerp(0.02f, 0.35f, Mathf.Sin(p * Mathf.PI));
                low += (white - low) * cutoff;
                float env = Mathf.Sin(p * Mathf.PI);
                env *= env;
                samples[i] = low * env * 2.2f;
                _ = t;
            }
            return Finish("whoosh", samples, SfxRate);
        }

        /// <summary>Bright short blip for counting coins.</summary>
        public static AudioClip CoinTick()
        {
            return Render("coin_tick", 0.09f, SfxRate, (t, rng) =>
            {
                float env = Adsr(t, 0.09f, 0.002f, 0.05f, 0.0f, 0.03f);
                return (Mathf.Sin(2f * Mathf.PI * 2400f * t) * 0.7f + Mathf.Sin(2f * Mathf.PI * 3600f * t) * 0.3f) * env;
            });
        }

        /// <summary>Four rising coin blips in a row (daily reward, purchase).</summary>
        public static AudioClip CoinBurst()
        {
            float[] freqs = { 1568f, 1976f, 2349f, 3136f };
            return Render("coin_burst", 0.45f, SfxRate, (t, rng) =>
            {
                float total = 0f;
                for (int i = 0; i < freqs.Length; i++)
                {
                    float start = i * 0.09f;
                    if (t < start) continue;
                    float lt = t - start;
                    float env = Adsr(lt, 0.14f, 0.002f, 0.08f, 0.0f, 0.04f);
                    total += (Mathf.Sin(2f * Mathf.PI * freqs[i] * lt) * 0.7f + Mathf.Sin(2f * Mathf.PI * freqs[i] * 1.5f * lt) * 0.3f) * env;
                }
                return total * 0.8f;
            });
        }

        /// <summary>Bell: inharmonic partials (1x, 2.7x, 5.4x) with fast decay and light vibrato.</summary>
        public static AudioClip StarChime()
        {
            return Render("star_chime", 0.8f, SfxRate, (t, rng) =>
            {
                float vibrato = 1f + 0.003f * Mathf.Sin(2f * Mathf.PI * 5.5f * t);
                float f = 1318.5f * vibrato; // E6
                float p1 = Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 3.5f);
                float p2 = Mathf.Sin(2f * Mathf.PI * f * 2.7f * t) * Mathf.Exp(-t * 7f) * 0.5f;
                float p3 = Mathf.Sin(2f * Mathf.PI * f * 5.4f * t) * Mathf.Exp(-t * 12f) * 0.25f;
                float env = Adsr(t, 0.8f, 0.003f, 0.1f, 0.9f, 0.3f);
                return (p1 + p2 + p3) * env * 0.8f;
            });
        }

        /// <summary>Win: a C major arpeggio (C5 E5 G5 C6) over a soft sustained pad.</summary>
        public static AudioClip WinJingle()
        {
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            return Render("win_jingle", 1.7f, SfxRate, (t, rng) =>
            {
                float total = 0f;
                for (int i = 0; i < notes.Length; i++)
                {
                    float start = i * 0.14f;
                    if (t < start) continue;
                    float lt = t - start;
                    float env = Adsr(lt, 1.7f - start, 0.005f, 0.25f, 0.35f, 0.5f);
                    total += (Mathf.Sin(2f * Mathf.PI * notes[i] * lt) + 0.3f * Mathf.Sin(2f * Mathf.PI * notes[i] * 2f * lt)) * env * 0.5f;
                }
                // pad: the chord an octave down with slow attack
                float pad = 0f;
                float[] padNotes = { 261.63f, 329.63f, 392f };
                for (int i = 0; i < padNotes.Length; i++)
                {
                    pad += Mathf.Sin(2f * Mathf.PI * padNotes[i] * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * padNotes[i] * 1.005f * t);
                }
                pad *= Adsr(t, 1.7f, 0.25f, 0.3f, 0.6f, 0.6f) * 0.12f;
                return total + pad;
            });
        }

        /// <summary>Lose: three descending minor notes with a wobble.</summary>
        public static AudioClip LoseSting()
        {
            float[] notes = { 329.63f, 293.66f, 246.94f }; // E4 D4 B3
            return Render("lose_sting", 1.0f, SfxRate, (t, rng) =>
            {
                float total = 0f;
                for (int i = 0; i < notes.Length; i++)
                {
                    float start = i * 0.22f;
                    if (t < start) continue;
                    float lt = t - start;
                    float vib = 1f + 0.01f * Mathf.Sin(2f * Mathf.PI * 6f * lt);
                    float env = Adsr(lt, i == notes.Length - 1 ? 0.55f : 0.3f, 0.01f, 0.1f, 0.6f, 0.2f);
                    total += (Mathf.Sin(2f * Mathf.PI * notes[i] * vib * lt) + 0.4f * Mathf.Sin(2f * Mathf.PI * notes[i] * 2f * lt)) * env * 0.5f;
                }
                return total;
            });
        }

        /// <summary>Combo banner: a rising major chord swelling up.</summary>
        public static AudioClip ComboSwell()
        {
            float[] notes = { 523.25f, 659.25f, 783.99f };
            return Render("combo_swell", 0.55f, SfxRate, (t, rng) =>
            {
                float rise = 1f + 0.06f * Mathf.SmoothStep(0f, 1f, t / 0.55f);
                float total = 0f;
                for (int i = 0; i < notes.Length; i++)
                {
                    total += Mathf.Sin(2f * Mathf.PI * notes[i] * rise * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * notes[i] * 2f * rise * t);
                }
                float env = Adsr(t, 0.55f, 0.12f, 0.1f, 0.9f, 0.2f);
                return total * env * 0.3f;
            });
        }

        /// <summary>Booster activation: a fast upward sweep with shimmer.</summary>
        public static AudioClip BoosterUse()
        {
            return Render("booster_use", 0.4f, SfxRate, (t, rng) =>
            {
                float p = t / 0.4f;
                float f = Mathf.Lerp(300f, 1400f, p * p);
                float phase = 2f * Mathf.PI * (300f * t + (1400f - 300f) * t * t * t / (3f * 0.4f * 0.4f));
                float shimmer = Mathf.Sin(2f * Mathf.PI * f * 3.01f * t) * 0.2f;
                float env = Adsr(t, 0.4f, 0.01f, 0.1f, 0.7f, 0.12f);
                return (Mathf.Sin(phase) + shimmer) * env * 0.7f;
            });
        }

        // ------------------------------------------------------------------ music

        /// <summary>
        /// 16 second ambient loop: soft pad chords I–vi–IV–V in C major (4 s each) with a gentle
        /// eighth-note arpeggio two octaves up. Rendered at 22.05 kHz to keep generation cheap.
        /// </summary>
        public static AudioClip MusicLoop()
        {
            const float chordSeconds = 4f;
            float[][] chords =
            {
                new[] { 130.81f, 164.81f, 196.00f, 261.63f }, // C  (I)
                new[] { 110.00f, 130.81f, 164.81f, 220.00f }, // Am (vi)
                new[] { 87.31f, 130.81f, 174.61f, 220.00f },  // F  (IV)
                new[] { 98.00f, 123.47f, 146.83f, 196.00f }   // G  (V)
            };
            int total = (int)(MusicRate * chordSeconds * chords.Length);
            float[] samples = new float[total];
            const float eighth = 0.25f; // seconds (120 bpm)
            for (int i = 0; i < total; i++)
            {
                float t = i / (float)MusicRate;
                int chordIndex = (int)(t / chordSeconds) % chords.Length;
                float ct = t - chordIndex * chordSeconds;
                float[] chord = chords[chordIndex];
                float[] next = chords[(chordIndex + 1) % chords.Length];

                // Pad: sum of chord tones with a slightly detuned copy and slow tremolo; the last
                // 0.25 s crossfades into the next chord so the loop point is seamless.
                float cross = ct > chordSeconds - 0.25f ? (ct - (chordSeconds - 0.25f)) / 0.25f : 0f;
                float pad = 0f;
                for (int n = 0; n < chord.Length; n++)
                {
                    pad += (1f - cross) * (Mathf.Sin(2f * Mathf.PI * chord[n] * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * chord[n] * 1.004f * t + 0.3f));
                    pad += cross * (Mathf.Sin(2f * Mathf.PI * next[n] * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * next[n] * 1.004f * t + 0.3f));
                }
                float tremolo = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 0.25f * t);
                pad *= 0.09f * tremolo;

                // Arpeggio: chord tones two octaves up, one per eighth note, soft sine with decay.
                int step = (int)(ct / eighth);
                float st = ct - step * eighth;
                float arpFreq = chord[step % chord.Length] * 4f;
                if ((step / chord.Length) % 2 == 1) arpFreq = chord[(chord.Length - 1) - step % chord.Length] * 4f;
                float arpEnv = Mathf.Exp(-st * 9f) * (1f - Mathf.Exp(-st * 400f));
                float arp = (Mathf.Sin(2f * Mathf.PI * arpFreq * t) + 0.2f * Mathf.Sin(2f * Mathf.PI * arpFreq * 2f * t)) * arpEnv * 0.11f;

                samples[i] = pad + arp;
            }
            return Finish("music_loop", samples, MusicRate, 0.6f);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Attack/decay/sustain/release envelope; <paramref name="length"/> is the total note length.</summary>
        private static float Adsr(float t, float length, float attack, float decay, float sustain, float release)
        {
            if (t < 0f || t > length) return 0f;
            float env;
            if (t < attack)
            {
                env = t / attack;
            }
            else if (t < attack + decay)
            {
                env = Mathf.Lerp(1f, sustain, (t - attack) / decay);
            }
            else
            {
                env = sustain;
            }
            float untilEnd = length - t;
            if (untilEnd < release)
            {
                env *= untilEnd / release;
            }
            return env;
        }

        /// <summary>Renders a generator function into a clip; the generator also gets a deterministic noise source.</summary>
        private static AudioClip Render(string name, float seconds, int rate, Func<float, Func<float>, float> generator)
        {
            int count = Mathf.CeilToInt(seconds * rate);
            float[] samples = new float[count];
            var rng = new System.Random(name.GetHashCode());
            Func<float> noise = () => (float)rng.NextDouble();
            for (int i = 0; i < count; i++)
            {
                samples[i] = generator(i / (float)rate, noise);
            }
            return Finish(name, samples, rate);
        }

        /// <summary>Normalises to <paramref name="ceiling"/>, soft-clips, fades the last samples and creates the clip.</summary>
        private static AudioClip Finish(string name, float[] samples, int rate, float ceiling = 0.9f)
        {
            float max = 0.0001f;
            for (int i = 0; i < samples.Length; i++)
            {
                max = Mathf.Max(max, Mathf.Abs(samples[i]));
            }
            float gain = ceiling / max;
            int tail = Mathf.Min(samples.Length, rate / 200); // 5 ms declick at the very end
            for (int i = 0; i < samples.Length; i++)
            {
                float s = samples[i] * gain;
                s = (float)Math.Tanh(s * 1.2f) / (float)Math.Tanh(1.2f) * ceiling; // gentle soft clip
                if (i >= samples.Length - tail)
                {
                    s *= (samples.Length - i) / (float)tail;
                }
                samples[i] = s;
            }
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
