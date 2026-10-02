using UnityEngine;

/// <summary>
/// Procedurally synthesised horror ambience for the clocktower (no audio files needed):
/// a looping wind-and-drone bed, random timber creaks in the roof, a rare tolling bell, faint whispers,
/// a heartbeat that swells near the crime scene, and a stinger whenever a new clue is discovered.
/// All clips are generated once on Start.
/// </summary>
public class ScarySoundscape : MonoBehaviour
{
    [Header("References")]
    public Transform Listener;
    [Tooltip("The heartbeat grows louder as the listener approaches this point (the fallen pendulum).")]
    public Transform CrimeScene;

    [Header("Levels")]
    [Range(0f, 1f)] public float MasterVolume = 0.8f;
    [Range(0f, 1f)] public float WindVolume = 0.32f;
    [Range(0f, 1f)] public float HeartbeatMaxVolume = 0.55f;
    [Range(0f, 1f)] public float StingerVolume = 0.55f;

    [Header("Timing (seconds)")]
    public Vector2 CreakInterval = new Vector2(7f, 18f);
    public Vector2 BellInterval = new Vector2(45f, 90f);
    public Vector2 WhisperInterval = new Vector2(35f, 75f);
    public float FirstBellDelay = 15f;
    public float HeartbeatFullDistance = 1.2f;
    public float HeartbeatSilentDistance = 4.5f;

    const int Rate = 22050;

    AudioSource windSource, heartSource, stingerSource, oneShot3D;
    AudioClip creak, bell, whisper, stinger;
    float nextCreak, nextBell, nextWhisper;

    void Start()
    {
        if (Listener == null && Camera.main != null) Listener = Camera.main.transform;

        windSource = Make2D("Wind", Wind(12f), true, WindVolume);
        heartSource = Make2D("Heartbeat", Heartbeat(), true, 0f);
        stingerSource = Make2D("Stinger", null, false, StingerVolume);
        creak = Creak();
        bell = Bell();
        whisper = Whisper();
        stinger = Stinger();

        var go = new GameObject("OneShot3D");
        go.transform.SetParent(transform, false);
        oneShot3D = go.AddComponent<AudioSource>();
        oneShot3D.playOnAwake = false;
        oneShot3D.spatialBlend = 1f;
        oneShot3D.rolloffMode = AudioRolloffMode.Linear;
        oneShot3D.minDistance = 2f;
        oneShot3D.maxDistance = 18f;

        windSource.Play();
        heartSource.Play();

        float now = Time.time;
        nextCreak = now + Random.Range(3f, 7f);
        nextBell = now + FirstBellDelay;
        nextWhisper = now + Random.Range(WhisperInterval.x, WhisperInterval.y);

        if (CaseDeductionManager.Instance != null)
            CaseDeductionManager.Instance.ClueDiscovered += OnClueDiscovered;
    }

    void OnDestroy()
    {
        if (CaseDeductionManager.Instance != null)
            CaseDeductionManager.Instance.ClueDiscovered -= OnClueDiscovered;
    }

    void Update()
    {
        float now = Time.time;
        windSource.volume = WindVolume * MasterVolume;

        if (Listener != null && CrimeScene != null)
        {
            float d = Vector3.Distance(Listener.position, CrimeScene.position);
            float k = 1f - Mathf.InverseLerp(HeartbeatFullDistance, HeartbeatSilentDistance, d);
            float target = HeartbeatMaxVolume * MasterVolume * k * k;
            heartSource.volume = Mathf.MoveTowards(heartSource.volume, target, Time.deltaTime * 0.5f);
        }

        if (now >= nextCreak)
        {
            nextCreak = now + Random.Range(CreakInterval.x, CreakInterval.y);
            PlayAt(creak, RandomPointInRoof(), Random.Range(0.75f, 1.25f), 0.7f);
        }
        if (now >= nextBell)
        {
            nextBell = now + Random.Range(BellInterval.x, BellInterval.y);
            PlayAt(bell, new Vector3(0f, 9f, -4f), 1f, 1f); // high in the tower above the clock face
        }
        if (now >= nextWhisper)
        {
            nextWhisper = now + Random.Range(WhisperInterval.x, WhisperInterval.y);
            Vector3 behind = Listener != null ? Listener.position - Listener.forward * 2.5f + Random.insideUnitSphere : Vector3.up;
            PlayAt(whisper, behind, Random.Range(0.85f, 1.1f), 0.45f);
        }
    }

    void OnClueDiscovered(string clueId, int count)
    {
        stingerSource.pitch = Random.Range(0.92f, 1.05f);
        stingerSource.PlayOneShot(stinger, StingerVolume * MasterVolume);
    }

    void PlayAt(AudioClip clip, Vector3 position, float pitch, float volume)
    {
        oneShot3D.transform.position = position;
        oneShot3D.pitch = pitch;
        oneShot3D.PlayOneShot(clip, volume * MasterVolume);
    }

    Vector3 RandomPointInRoof()
    {
        float a = Random.value * Mathf.PI * 2f;
        float r = Random.Range(2f, 5f);
        return new Vector3(Mathf.Cos(a) * r, Random.Range(4.6f, 6.5f), Mathf.Sin(a) * r);
    }

    AudioSource Make2D(string name, AudioClip clip, bool loop, float volume)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = loop;
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        s.volume = volume * MasterVolume;
        return s;
    }

    // ==================================================================== synthesis

    static AudioClip ToClip(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static float Noise(ref uint state)
    {
        state ^= state << 13; state ^= state >> 17; state ^= state << 5;
        return (state / (float)uint.MaxValue) * 2f - 1f;
    }

    /// <summary>Howling wind (brown noise with gusts and a whistling resonance) over a beating low drone. Loops seamlessly.</summary>
    static AudioClip Wind(float seconds)
    {
        int n = (int)(seconds * Rate), fade = Rate; // generate one extra second and cross-fade it into the start
        var raw = new float[n + fade];
        uint seed = 1234567u;
        float brown = 0f, lp = 0f, bp1 = 0f, bp2 = 0f;
        for (int i = 0; i < raw.Length; i++)
        {
            float t = i / (float)Rate;
            float white = Noise(ref seed);
            brown = (brown + 0.02f * white) / 1.02f;
            lp += (brown - lp) * 0.05f;
            float gust = 0.35f + 0.65f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / 6f), 2f)
                                        * (0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * t / 4f + 1f));
            // resonant "whistle" through the window slats: a narrow band that drifts with the gusts
            float f = 380f + 160f * Mathf.Sin(2f * Mathf.PI * t / 12f);
            float w = 2f * Mathf.Sin(Mathf.PI * f / Rate);
            bp1 += w * (white * 0.3f - bp1 - 0.02f * bp2); // state-variable band-pass
            bp2 += w * bp1;
            float drone = 0.18f * Mathf.Sin(2f * Mathf.PI * 55f * t) + 0.12f * Mathf.Sin(2f * Mathf.PI * 58.25f * t)
                        + 0.06f * Mathf.Sin(2f * Mathf.PI * 110f * t + Mathf.Sin(2f * Mathf.PI * t / 3f));
            raw[i] = (lp * 9f * gust + bp2 * 0.05f * gust * gust + drone) * 0.6f;
        }
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            if (i < fade)
            {
                float a = i / (float)fade;
                data[i] = raw[i] * a + raw[n + i] * (1f - a);
            }
            else data[i] = raw[i];
        }
        return ToClip("Wind", Normalize(data, 0.8f));
    }

    /// <summary>Old timber under strain: a slow pitch glide with stick-slip friction pulses.</summary>
    static AudioClip Creak()
    {
        int n = (int)(1.6f * Rate);
        var d = new float[n];
        uint seed = 99u;
        float phase = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 1.6f)) * (0.6f + 0.4f * Mathf.Sin(t * 9f));
            float freq = Mathf.Lerp(110f, 70f, t / 1.6f) + 8f * Mathf.Sin(t * 23f);
            phase += freq / Rate;
            float saw = 2f * (phase - Mathf.Floor(phase)) - 1f;
            float stick = phase - Mathf.Floor(phase) < 0.12f ? 1f : 0.25f; // friction "grab" each cycle
            lp += (saw * stick + Noise(ref seed) * 0.15f - lp) * 0.25f;
            d[i] = lp * env;
        }
        return ToClip("Creak", Normalize(d, 0.9f));
    }

    /// <summary>Deep church bell: inharmonic partials with long, staggered decays.</summary>
    static AudioClip Bell()
    {
        int n = 7 * Rate;
        var d = new float[n];
        float f0 = 98f;
        float[] ratios = { 0.5f, 1f, 1.183f, 1.506f, 2f, 2.514f, 2.662f, 3.011f };
        float[] amps = { 0.9f, 0.7f, 0.5f, 0.35f, 0.4f, 0.2f, 0.15f, 0.12f };
        float[] decay = { 0.35f, 0.45f, 0.6f, 0.75f, 0.9f, 1.3f, 1.5f, 1.8f };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate, s = 0f;
            for (int k = 0; k < ratios.Length; k++)
                s += amps[k] * Mathf.Exp(-t * decay[k]) * Mathf.Sin(2f * Mathf.PI * f0 * ratios[k] * t + k);
            float strike = Mathf.Min(1f, t * 400f); // hammer attack
            d[i] = s * strike;
        }
        return ToClip("Bell", Normalize(d, 0.95f));
    }

    /// <summary>Breathy, wordless whisper: band-passed noise shaped into a few syllables.</summary>
    static AudioClip Whisper()
    {
        int n = (int)(2.2f * Rate);
        var d = new float[n];
        uint seed = 4242u;
        float b1 = 0f, b2 = 0f, hp = 0f, prev = 0f;
        float[] syllables = { 0.15f, 0.55f, 0.85f, 1.35f, 1.7f };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float env = 0f;
            foreach (float c in syllables)
                env += Mathf.Exp(-Mathf.Pow((t - c) / 0.11f, 2f));
            float f = 2400f + 1400f * Mathf.Sin(t * 5.3f); // drifting "formant"
            float w = 2f * Mathf.Sin(Mathf.PI * f / Rate);
            float x = Noise(ref seed);
            hp = 0.97f * (hp + x - prev); prev = x; // high-pass for breathiness
            b1 += w * (hp - b1 - 0.35f * b2);
            b2 += w * b1;
            d[i] = b2 * env;
        }
        return ToClip("Whisper", Normalize(d, 0.7f));
    }

    /// <summary>Two low thumps per beat (lub-dub) at about 58 bpm. Loops.</summary>
    static AudioClip Heartbeat()
    {
        int n = (int)(Rate * 60f / 58f);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            d[i] = Thump(t, 0f, 52f) + 0.7f * Thump(t, 0.28f, 46f);
        }
        return ToClip("Heartbeat", Normalize(d, 0.9f));
    }

    static float Thump(float t, float start, float freq)
    {
        float u = t - start;
        if (u < 0f) return 0f;
        float pitchDrop = freq * (1f + 0.6f * Mathf.Exp(-u * 30f));
        return Mathf.Sin(2f * Mathf.PI * pitchDrop * u) * Mathf.Exp(-u * 14f) * Mathf.Min(1f, u * 300f);
    }

    /// <summary>Discovery stinger: a sub boom with a falling pitch plus a dissonant, shimmering high cluster.</summary>
    static AudioClip Stinger()
    {
        int n = (int)(2.6f * Rate);
        var d = new float[n];
        uint seed = 777u;
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float boomF = 38f + 50f * Mathf.Exp(-t * 6f);
            float boom = Mathf.Sin(2f * Mathf.PI * boomF * t) * Mathf.Exp(-t * 1.8f) * Mathf.Min(1f, t * 200f);
            lp += (Noise(ref seed) - lp) * 0.08f;
            float hit = lp * Mathf.Exp(-t * 9f) * 1.5f;
            float swell = Mathf.Clamp01(t / 0.4f) * Mathf.Exp(-t * 1.2f);
            float shimmer = (Mathf.Sin(2f * Mathf.PI * 740f * t) + Mathf.Sin(2f * Mathf.PI * 784f * t)
                             + 0.6f * Mathf.Sin(2f * Mathf.PI * 1108f * t)) * 0.12f * swell * (0.7f + 0.3f * Mathf.Sin(t * 37f));
            d[i] = boom + hit + shimmer;
        }
        return ToClip("Stinger", Normalize(d, 0.95f));
    }

    static float[] Normalize(float[] d, float peak)
    {
        float max = 1e-5f;
        foreach (float v in d) max = Mathf.Max(max, Mathf.Abs(v));
        float k = peak / max;
        for (int i = 0; i < d.Length; i++) d[i] *= k;
        return d;
    }
}
