using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Four kinds of sound, each on its own AudioSource: ambient, music, sfx and ui.
/// The clips are generated in code as quiet placeholders so it works straight away.
/// To use real sounds: put your clips on the sources, and create an Audio Mixer with four groups
/// (Ambient, Music, SFX, UI) and drag them into the four group fields on the Experience object.
/// </summary>
public partial class ExperienceController
{
    [Header("Audio (optional: assign Audio Mixer groups)")]
    public AudioMixerGroup ambientGroup;
    public AudioMixerGroup musicGroup;
    public AudioMixerGroup sfxGroup;
    public AudioMixerGroup uiGroup;

    AudioSource ambientSrc, musicSrc, sfxSrc, uiSrc;
    AudioClip boardClip, goClip, uiClip;

    partial void InitAudio()
    {
        ambientSrc = MakeSource("Ambient", ambientGroup, true, 0.6f);
        ambientSrc.clip = ToneBank.RoomTone();
        ambientSrc.Play();

        musicSrc = MakeSource("Music", musicGroup, true, 0.25f);
        musicSrc.clip = ToneBank.SoftPad();
        musicSrc.Play();

        sfxSrc = MakeSource("SFX", sfxGroup, false, 0.9f);
        uiSrc = MakeSource("UI", uiGroup, false, 0.7f);

        boardClip = ToneBank.Sweep(520f, 880f, 0.18f);   // soft "pop"
        goClip = ToneBank.Sweep(220f, 150f, 0.9f);       // low motor hum
        uiClip = ToneBank.Sweep(880f, 880f, 0.08f);      // short tick
    }

    partial void PlaySfx(string sfxName)
    {
        switch (sfxName)
        {
            case "board": sfxSrc.PlayOneShot(boardClip); break;
            case "go": sfxSrc.PlayOneShot(goClip); break;
            case "ui": uiSrc.PlayOneShot(uiClip); break;
        }
    }

    AudioSource MakeSource(string sourceName, AudioMixerGroup group, bool loop, float volume)
    {
        var go = new GameObject("Audio_" + sourceName);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = loop;
        src.volume = volume;
        src.spatialBlend = 0f;
        if (group != null) src.outputAudioMixerGroup = group;
        return src;
    }
}

/// <summary>Generates small placeholder sounds so the project has audio without any asset files.</summary>
public static class ToneBank
{
    const int Rate = 44100;

    public static AudioClip Sweep(float fromHz, float toHz, float seconds)
    {
        int n = (int)(Rate * seconds);
        var data = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            phase += 2f * Mathf.PI * Mathf.Lerp(fromHz, toHz, t) / Rate;
            data[i] = Mathf.Sin(phase) * Mathf.Sin(Mathf.PI * t) * 0.5f;
        }
        var clip = AudioClip.Create("sweep", n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // 8 seconds, whole number of cycles per note, so it loops without a click.
    public static AudioClip SoftPad()
    {
        int n = Rate * 8;
        var data = new float[n];
        float[] notes = { 262f, 330f, 392f };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float sum = 0f;
            foreach (var f in notes) sum += Mathf.Sin(2f * Mathf.PI * f * t);
            float swell = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * t / 8f);
            data[i] = sum / notes.Length * swell * 0.5f;
        }
        var clip = AudioClip.Create("pad", n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Quiet low-passed noise, a bit like the murmur and echo of a big hall.
    public static AudioClip RoomTone()
    {
        int n = Rate * 4;
        var data = new float[n];
        var rng = new System.Random(7);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += 0.03f * (white - lp);
            data[i] = lp * 0.6f;
        }
        var clip = AudioClip.Create("room", n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
