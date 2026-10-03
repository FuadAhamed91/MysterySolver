using UnityEngine;

/// <summary>
/// Procedurally synthesised horror ambience (no audio files needed). Every clip is generated once on Start.
/// Themes:
///   Clocktower - wind and drone, timber creaks in the roof, a rare tolling bell.
///   Lighthouse - storm wind, rain on the glass, waves on the rocks, groaning iron, a fog bell far below, thunder.
///   Greenhouse - crickets, dripping water, creaking glass, a distant owl.
/// Every theme adds faint whispers, a heartbeat that swells near the crime scene, and a stinger for each new clue.
/// </summary>
public class ScarySoundscape : MonoBehaviour
{
    public enum SoundTheme { Clocktower, Lighthouse, Greenhouse }

    [Header("Theme")]
    public SoundTheme Theme = SoundTheme.Clocktower;

    [Header("References")]
    public Transform Listener;
    [Tooltip("The heartbeat grows louder as the listener approaches this point.")]
    public Transform CrimeScene;
    [Tooltip("Random 3D sounds (creaks, drips) play inside this box, centred on this transform.")]
    public Vector3 AmbientArea = new Vector3(10f, 2f, 10f);
    public float AmbientHeight = 5f;

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

    AudioSource windSource, heartSource, stingerSource, thunderSource;
    AudioSource[] pool;
    int poolNext;
    AudioClip creak, bell, whisper, stinger, drip, owl, thunderNear, thunderFar;
    float nextCreak, nextBell, nextWhisper, nextDrip, nextOwl;
    Vector2 creakPitch = new Vector2(0.75f, 1.25f);

    void Start()
    {
        if (Listener == null && Camera.main != null) Listener = Camera.main.transform;

        windSource = Make2D("Wind", Wind(12f), true, WindVolume);
        heartSource = Make2D("Heartbeat", Heartbeat(), true, 0f);
        stingerSource = Make2D("Stinger", null, false, StingerVolume);
        thunderSource = Make2D("Thunder", null, false, 1f);
        creak = Creak();
        whisper = Whisper();
        stinger = Stinger();

        switch (Theme)
        {
            case SoundTheme.Clocktower:
                bell = Bell(98f, 7f);
                break;
            case SoundTheme.Lighthouse:
                bell = Bell(262f, 5f); // the fog bell being rung at the foot of the tower
                BellInterval = new Vector2(18f, 32f);
                FirstBellDelay = 8f;
                creakPitch = new Vector2(0.45f, 0.7f); // iron groaning in the wind
                Make2D("Rain", Rain(10f), true, 0.3f).Play();
                Make2D("Waves", Waves(16f), true, 0.38f).Play();
                thunderNear = Thunder(true);
                thunderFar = Thunder(false);
                break;
            case SoundTheme.Greenhouse:
                WindVolume *= 0.45f;
                creakPitch = new Vector2(1.6f, 2.2f); // glass panes ticking and creaking as they cool
                Make2D("Crickets", Crickets(8f), true, 0.22f).Play();
                drip = Drip();
                owl = Owl();
                break;
        }

        pool = new AudioSource[4];
        for (int i = 0; i < pool.Length; i++)
        {
            var go = new GameObject("OneShot3D_" + i);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 2f;
            s.maxDistance = 30f;
            pool[i] = s;
        }

        windSource.Play();
        heartSource.Play();

        float now = Time.time;
        nextCreak = now + Random.Range(3f, 7f);
        nextBell = now + FirstBellDelay;
        nextWhisper = now + Random.Range(WhisperInterval.x, WhisperInterval.y);
        nextDrip = now + Random.Range(1f, 3f);
        nextOwl = now + Random.Range(12f, 25f);

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
            PlayAt(creak, RandomAmbientPoint(AmbientHeight), Random.Range(creakPitch.x, creakPitch.y),
                   Theme == SoundTheme.Greenhouse ? 0.35f : 0.7f);
        }
        if (bell != null && now >= nextBell)
        {
            nextBell = now + Random.Range(BellInterval.x, BellInterval.y);
            if (Theme == SoundTheme.Lighthouse)
                PlayAt(bell, transform.position + new Vector3(4f, -22f, 3f), Random.Range(0.97f, 1.03f), 0.9f);
            else
                PlayAt(bell, new Vector3(0f, 9f, -4f), 1f, 1f); // high in the tower above the clock face
        }
        if (now >= nextWhisper)
        {
            nextWhisper = now + Random.Range(WhisperInterval.x, WhisperInterval.y);
            Vector3 behind = Listener != null ? Listener.position - Listener.forward * 2.5f + Random.insideUnitSphere : Vector3.up;
            PlayAt(whisper, behind, Random.Range(0.85f, 1.1f), 0.45f);
        }
        if (drip != null && now >= nextDrip)
        {
            nextDrip = now + Random.Range(1.2f, 4.5f);
            PlayAt(drip, RandomAmbientPoint(1.0f), Random.Range(0.8f, 1.35f), 0.4f);
        }
        if (owl != null && now >= nextOwl)
        {
            nextOwl = now + Random.Range(30f, 60f);
            Vector3 far = transform.position + new Vector3(Random.Range(-1f, 1f), 0.3f, Random.Range(-1f, 1f)).normalized * 25f + Vector3.up * 6f;
            PlayAt(owl, far, Random.Range(0.95f, 1.05f), 0.8f);
        }
    }

    /// <summary>Called by the LightningStorm a moment after each flash. 1 = right overhead, 0 = far away.</summary>
    public void PlayThunder(float nearness)
    {
        if (thunderNear == null) thunderNear = Thunder(true);
        if (thunderFar == null) thunderFar = Thunder(false);
        thunderSource.pitch = Random.Range(0.85f, 1.05f);
        thunderSource.PlayOneShot(nearness > 0.55f ? thunderNear : thunderFar, (0.45f + 0.55f * nearness) * MasterVolume);
    }

    void OnClueDiscovered(string clueId, int count)
    {
        stingerSource.pitch = Random.Range(0.92f, 1.05f);
        stingerSource.PlayOneShot(stinger, StingerVolume * MasterVolume);
    }

    void PlayAt(AudioClip clip, Vector3 position, float pitch, float volume)
    {
        // Round-robin over a few 3D sources so overlapping sounds keep their own positions.
        var s = pool[poolNext];
        poolNext = (poolNext + 1) % pool.Length;
        s.transform.position = position;
        s.pitch = pitch;
        s.PlayOneShot(clip, volume * MasterVolume);
    }

    Vector3 RandomAmbientPoint(float height)
    {
        Vector3 c = transform.position;
        return new Vector3(c.x + Random.Range(-AmbientArea.x, AmbientArea.x) * 0.5f,
                           height + Random.Range(0f, AmbientArea.y),
                           c.z + Random.Range(-AmbientArea.z, AmbientArea.z) * 0.5f);
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

    /// <summary>Generates <paramref name="seconds"/> + 1 s and cross-fades the extra second into the start, so it loops seamlessly.</summary>
    static float[] Loop(float[] raw, int n)
    {
        int fade = raw.Length - n;
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
        return data;
    }

    /// <summary>Howling wind (brown noise with gusts and a whistling resonance) over a beating low drone. Loops seamlessly.</summary>
    static AudioClip Wind(float seconds)
    {
        int n = (int)(seconds * Rate);
        var raw = new float[n + Rate];
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
            float f = 380f + 160f * Mathf.Sin(2f * Mathf.PI * t / 12f);
            float w = 2f * Mathf.Sin(Mathf.PI * f / Rate);
            bp1 += w * (white * 0.3f - bp1 - 0.02f * bp2);
            bp2 += w * bp1;
            float drone = 0.18f * Mathf.Sin(2f * Mathf.PI * 55f * t) + 0.12f * Mathf.Sin(2f * Mathf.PI * 58.25f * t)
                        + 0.06f * Mathf.Sin(2f * Mathf.PI * 110f * t + Mathf.Sin(2f * Mathf.PI * t / 3f));
            raw[i] = (lp * 9f * gust + bp2 * 0.05f * gust * gust + drone) * 0.6f;
        }
        return ToClip("Wind", Normalize(Loop(raw, n), 0.8f));
    }

    /// <summary>Steady rain on glass: hissing high-passed noise with scattered droplet ticks. Loops.</summary>
    static AudioClip Rain(float seconds)
    {
        int n = (int)(seconds * Rate);
        var raw = new float[n + Rate];
        uint seed = 8080u;
        float hp = 0f, prev = 0f, lp = 0f, tick = 0f;
        for (int i = 0; i < raw.Length; i++)
        {
            float t = i / (float)Rate;
            float x = Noise(ref seed);
            hp = 0.9f * (hp + x - prev); prev = x;
            lp += (hp - lp) * 0.35f;
            if (Noise(ref seed) > 0.9993f) tick = 1f; // a heavier drop hits the pane
            tick *= 0.985f;
            float swell = 0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * t / 5f);
            raw[i] = lp * swell * 0.5f + tick * Noise(ref seed) * 0.6f;
        }
        return ToClip("Rain", Normalize(Loop(raw, n), 0.7f));
    }

    /// <summary>Waves rolling in and breaking on the rocks far below. Loops.</summary>
    static AudioClip Waves(float seconds)
    {
        int n = (int)(seconds * Rate);
        var raw = new float[n + Rate];
        uint seed = 4040u;
        float brown = 0f, lp = 0f, hp = 0f, prev = 0f;
        for (int i = 0; i < raw.Length; i++)
        {
            float t = i / (float)Rate;
            float x = Noise(ref seed);
            brown = (brown + 0.025f * x) / 1.025f;
            lp += (brown - lp) * 0.08f;
            hp = 0.95f * (hp + x - prev); prev = x;
            float phase = (t % 8f) / 8f;                       // one wave every 8 s
            float swell = Mathf.Pow(Mathf.Sin(Mathf.PI * phase), 3f);
            float crash = phase > 0.45f && phase < 0.75f ? Mathf.Sin(Mathf.PI * (phase - 0.45f) / 0.3f) : 0f;
            raw[i] = lp * 10f * (0.25f + 0.75f * swell) + hp * 0.12f * crash;
        }
        return ToClip("Waves", Normalize(Loop(raw, n), 0.85f));
    }

    /// <summary>Thunder: an optional sharp crack, then a long rolling rumble.</summary>
    static AudioClip Thunder(bool near)
    {
        int n = (int)(5f * Rate);
        var d = new float[n];
        uint seed = near ? 1717u : 2929u;
        float brown = 0f, lp = 0f, roll = 1f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float x = Noise(ref seed);
            brown = (brown + 0.03f * x) / 1.03f;
            lp += (brown - lp) * 0.06f;
            if (i % 2205 == 0) roll = 0.5f + 0.5f * Mathf.Abs(Noise(ref seed)); // irregular rolling every 0.1 s
            float env = Mathf.Min(1f, t * (near ? 40f : 4f)) * Mathf.Exp(-t * 0.75f);
            float crack = near ? x * Mathf.Exp(-t * 30f) * 1.2f : 0f;
            d[i] = lp * 14f * env * roll + crack;
        }
        return ToClip(near ? "ThunderNear" : "ThunderFar", Normalize(d, 0.95f));
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
            float stick = phase - Mathf.Floor(phase) < 0.12f ? 1f : 0.25f;
            lp += (saw * stick + Noise(ref seed) * 0.15f - lp) * 0.25f;
            d[i] = lp * env;
        }
        return ToClip("Creak", Normalize(d, 0.9f));
    }

    /// <summary>Bell: inharmonic partials with long, staggered decays.</summary>
    static AudioClip Bell(float f0, float seconds)
    {
        int n = (int)(seconds * Rate);
        var d = new float[n];
        float[] ratios = { 0.5f, 1f, 1.183f, 1.506f, 2f, 2.514f, 2.662f, 3.011f };
        float[] amps = { 0.9f, 0.7f, 0.5f, 0.35f, 0.4f, 0.2f, 0.15f, 0.12f };
        float[] decay = { 0.35f, 0.45f, 0.6f, 0.75f, 0.9f, 1.3f, 1.5f, 1.8f };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate, s = 0f;
            for (int k = 0; k < ratios.Length; k++)
                s += amps[k] * Mathf.Exp(-t * decay[k]) * Mathf.Sin(2f * Mathf.PI * f0 * ratios[k] * t + k);
            d[i] = s * Mathf.Min(1f, t * 400f);
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
            float f = 2400f + 1400f * Mathf.Sin(t * 5.3f);
            float w = 2f * Mathf.Sin(Mathf.PI * f / Rate);
            float x = Noise(ref seed);
            hp = 0.97f * (hp + x - prev); prev = x;
            b1 += w * (hp - b1 - 0.35f * b2);
            b2 += w * b1;
            d[i] = b2 * env;
        }
        return ToClip("Whisper", Normalize(d, 0.7f));
    }

    /// <summary>Three crickets chirping at different rates (each period divides the loop length, so it loops cleanly).</summary>
    static AudioClip Crickets(float seconds)
    {
        int n = (int)(seconds * Rate);
        var d = new float[n];
        uint seed = 3131u;
        (float freq, float period, float offset, float amp)[] crickets =
        {
            (4300f, 0.5f, 0.05f, 1f), (4750f, 0.4f, 0.21f, 0.7f), (5150f, 0.32f, 0.13f, 0.45f),
        };
        foreach (var c in crickets)
        {
            for (float start = c.offset; start < seconds; start += c.period)
            {
                float jitterAmp = c.amp * (0.75f + 0.25f * Mathf.Abs(Noise(ref seed)));
                for (int p = 0; p < 3; p++) // each chirp is three quick pulses
                {
                    int s0 = (int)((start + p * 0.03f) * Rate);
                    int len = (int)(0.018f * Rate);
                    for (int i = 0; i < len; i++)
                    {
                        int idx = (s0 + i) % n;
                        float t = i / (float)Rate;
                        float env = Mathf.Sin(Mathf.PI * i / len);
                        d[idx] += Mathf.Sin(2f * Mathf.PI * c.freq * t) * env * jitterAmp;
                    }
                }
            }
        }
        return ToClip("Crickets", Normalize(d, 0.6f));
    }

    /// <summary>A single water drop: a quick falling "plink".</summary>
    static AudioClip Drip()
    {
        int n = (int)(0.5f * Rate);
        var d = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float f = 700f + 1300f * Mathf.Exp(-t * 35f);
            phase += f / Rate;
            d[i] = Mathf.Sin(2f * Mathf.PI * phase) * Mathf.Exp(-t * 14f) * Mathf.Min(1f, t * 2000f);
        }
        return ToClip("Drip", Normalize(d, 0.8f));
    }

    /// <summary>Tawny owl: "hoo... hoo-hoo".</summary>
    static AudioClip Owl()
    {
        int n = (int)(1.9f * Rate);
        var d = new float[n];
        (float start, float len)[] hoots = { (0.0f, 0.45f), (0.95f, 0.22f), (1.25f, 0.55f) };
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float env = 0f, f = 400f;
            foreach (var h in hoots)
            {
                if (t >= h.start && t < h.start + h.len)
                {
                    float u = (t - h.start) / h.len;
                    env = Mathf.Sin(Mathf.PI * u);
                    f = 420f - 50f * u + 6f * Mathf.Sin(t * 40f);
                }
            }
            phase += f / Rate;
            d[i] = (Mathf.Sin(2f * Mathf.PI * phase) + 0.15f * Mathf.Sin(4f * Mathf.PI * phase)) * env;
        }
        return ToClip("Owl", Normalize(d, 0.8f));
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
