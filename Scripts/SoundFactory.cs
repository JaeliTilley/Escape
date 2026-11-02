using UnityEngine;

/// <summary>
/// Synthesizes the game's placeholder sound effects in code (no audio files needed).
/// Each one can be replaced by dragging a real clip into the matching slot on the
/// SoundManager in the Inspector.
/// </summary>
public static class SoundFactory
{
    const int SampleRate = 44100;

    /// <summary>A soft, low thud of a step on old floorboards.</summary>
    public static AudioClip Footstep()
    {
        return Make("Footstep", 0.09f, (t, i) =>
        {
            float env = Mathf.Exp(-t * 55f);
            float thump = Mathf.Sin(2f * Mathf.PI * 95f * t);
            return (thump * 0.7f + Noise() * 0.3f) * env * 0.6f;
        }, lowPass: 0.25f);
    }

    /// <summary>Bright three-note chime when a key is picked up.</summary>
    public static AudioClip KeyPickup()
    {
        float[] notes = { 880f, 1108.7f, 1318.5f }; // A5, C#6, E6
        return Make("KeyPickup", 0.45f, (t, i) =>
        {
            int n = Mathf.Min((int)(t / 0.1f), notes.Length - 1);
            float local = t - n * 0.1f;
            float env = Mathf.Exp(-local * 9f);
            return Mathf.Sin(2f * Mathf.PI * notes[n] * t) * env * 0.45f;
        });
    }

    /// <summary>Low creaking sweep when the exit door unlocks.</summary>
    public static AudioClip DoorUnlock()
    {
        float phase = 0f;
        return Make("DoorUnlock", 0.9f, (t, i) =>
        {
            float freq = Mathf.Lerp(140f, 70f, t / 0.9f) + Mathf.Sin(t * 40f) * 12f;
            phase += freq / SampleRate;
            float saw = (phase % 1f) * 2f - 1f;
            float env = Mathf.Sin(Mathf.PI * t / 0.9f);
            return (saw * 0.6f + Noise() * 0.15f) * env * 0.4f;
        }, lowPass: 0.15f);
    }

    /// <summary>Guttural growl the monster makes when it spots the player.</summary>
    public static AudioClip MonsterGrowl()
    {
        float phase = 0f;
        return Make("MonsterGrowl", 1.1f, (t, i) =>
        {
            float wobble = 1f + 0.25f * Mathf.Sin(2f * Mathf.PI * 13f * t);
            phase += 62f * wobble / SampleRate;
            float saw = (phase % 1f) * 2f - 1f;
            float env = Mathf.Clamp01(t * 8f) * Mathf.Clamp01((1.1f - t) * 3f);
            return (saw * 0.7f + Noise() * 0.35f) * env * 0.55f;
        }, lowPass: 0.12f);
    }

    /// <summary>High screech when the flashlight scares the monster off.</summary>
    public static AudioClip MonsterShriek()
    {
        float phase = 0f;
        return Make("MonsterShriek", 0.6f, (t, i) =>
        {
            float freq = Mathf.Lerp(900f, 350f, t / 0.6f);
            phase += freq / SampleRate;
            float env = Mathf.Exp(-t * 4f);
            return (Mathf.Sin(2f * Mathf.PI * phase) * 0.6f + Noise() * 0.3f) * env * 0.4f;
        }, lowPass: 0.5f);
    }

    /// <summary>Flashlight switch click.</summary>
    public static AudioClip Click()
    {
        return Make("Click", 0.04f, (t, i) => Noise() * Mathf.Exp(-t * 180f) * 0.5f);
    }

    /// <summary>One "lub-dub" heartbeat. Played faster and louder as the monster gets closer.</summary>
    public static AudioClip Heartbeat()
    {
        return Make("Heartbeat", 0.45f, (t, i) =>
        {
            float beat1 = Mathf.Exp(-t * 30f) * Mathf.Sin(2f * Mathf.PI * 55f * t);
            float t2 = t - 0.18f;
            float beat2 = t2 > 0f ? Mathf.Exp(-t2 * 34f) * Mathf.Sin(2f * Mathf.PI * 48f * t2) * 0.75f : 0f;
            return (beat1 + beat2) * 0.9f;
        }, lowPass: 0.3f);
    }

    /// <summary>Descending, dissonant sting when the player is caught.</summary>
    public static AudioClip GameOver()
    {
        return Make("GameOver", 1.6f, (t, i) =>
        {
            float f = Mathf.Lerp(220f, 90f, t / 1.6f);
            float tone = Mathf.Sin(2f * Mathf.PI * f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * f * 1.06f * t);
            float env = Mathf.Clamp01(t * 20f) * Mathf.Exp(-t * 1.6f);
            return tone * env * 0.35f;
        });
    }

    /// <summary>Rising major arpeggio when the player escapes.</summary>
    public static AudioClip Victory()
    {
        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f }; // C5 E5 G5 C6
        return Make("Victory", 1.4f, (t, i) =>
        {
            int n = Mathf.Min((int)(t / 0.15f), notes.Length - 1);
            float local = t - n * 0.15f;
            float decay = n == notes.Length - 1 ? 2.2f : 8f;
            float env = Mathf.Exp(-local * decay);
            float tone = Mathf.Sin(2f * Mathf.PI * notes[n] * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * notes[n] * t);
            return tone * env * 0.35f;
        });
    }

    // ---------- Synthesis helpers ----------

    delegate float SampleFunc(float time, int index);

    static AudioClip Make(string name, float seconds, SampleFunc func, float lowPass = 1f)
    {
        int count = Mathf.CeilToInt(seconds * SampleRate);
        float[] data = new float[count];
        float smoothed = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            float s = func(t, i);
            // Simple one-pole low-pass filter: lower values = darker, muffled sound.
            smoothed += (s - smoothed) * lowPass;
            data[i] = Mathf.Clamp(smoothed, -1f, 1f);
        }

        // Short fade-out so no clip ends with a pop.
        int fade = Mathf.Min(256, count);
        for (int i = 0; i < fade; i++) data[count - 1 - i] *= i / (float)fade;

        AudioClip clip = AudioClip.Create(name, count, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static float Noise() => Random.value * 2f - 1f;
}
