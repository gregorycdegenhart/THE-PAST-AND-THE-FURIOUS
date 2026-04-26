using UnityEngine;

public class CarAudio : MonoBehaviour
{
    [Header("Engine")]
    public float minPitch = 0.6f;
    public float maxPitch = 2.0f;
    public float engineVolume = 0.3f;

    [Header("SFX")]
    public float turboVolume = 0.5f;
    public float checkpointVolume = 0.6f;

    private CarController carController;
    private AudioSource engineSource;
    private AudioSource turboSource;
    private AudioSource sfxSource;
    private bool wasTurboActive = false;

    void Awake()
    {
        carController = GetComponent<CarController>();

        engineSource = gameObject.AddComponent<AudioSource>();
        engineSource.clip = GenerateEngineClip();
        engineSource.loop = true;
        engineSource.volume = engineVolume;
        engineSource.spatialBlend = 0f;
        engineSource.Play();

        turboSource = gameObject.AddComponent<AudioSource>();
        turboSource.clip = GenerateTurboClip();
        turboSource.loop = false;
        turboSource.volume = turboVolume;
        turboSource.spatialBlend = 0f;
        turboSource.playOnAwake = false;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.clip = GenerateCheckpointClip();
        sfxSource.loop = false;
        sfxSource.volume = checkpointVolume;
        sfxSource.spatialBlend = 0f;
        sfxSource.playOnAwake = false;
    }

    void Update()
    {
        if (carController == null) return;

        // engine pitch based on speed
        float speed = carController.rb.linearVelocity.magnitude;
        float t = Mathf.Clamp01(speed / carController.maxForwardSpeed);
        engineSource.pitch = Mathf.Lerp(minPitch, maxPitch, t);

        // turbo whoosh
        bool turboActive = carController.IsTurboActive();
        if (turboActive && !wasTurboActive)
            turboSource.Play();
        wasTurboActive = turboActive;
    }

    public void PlayCheckpointSound()
    {
        if (sfxSource != null)
            sfxSource.Play();
    }

    static AudioClip GenerateEngineClip()
    {
        int sampleRate = 44100;
        int length = sampleRate; // 1 second
        float[] samples = new float[length];

        for (int i = 0; i < length; i++)
        {
            float time = (float)i / sampleRate;
            // Smooth sine-based engine tone - deep rumble, not harsh
            float sample = 0f;
            sample += Mathf.Sin(2f * Mathf.PI * 75f * time) * 0.4f;        // deep fundamental
            sample += Mathf.Sin(2f * Mathf.PI * 150f * time) * 0.25f;       // first harmonic
            sample += Mathf.Sin(2f * Mathf.PI * 225f * time) * 0.1f;        // second harmonic
            sample += Mathf.Sin(2f * Mathf.PI * 300f * time) * 0.05f;       // subtle overtone
            // light noise for character, not harshness
            sample += (Random.value * 2f - 1f) * 0.02f;
            samples[i] = sample * 0.35f;
        }

        AudioClip clip = AudioClip.Create("Engine", length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    static AudioClip GenerateTurboClip()
    {
        int sampleRate = 44100;
        int length = (int)(sampleRate * 1.4f); // 1.4 seconds total
        float[] samples = new float[length];

        // Use a seeded System.Random so the clip is deterministic
        System.Random rng = new System.Random(42);

        // Phase accumulators for smooth frequency sweeps
        float phase1 = 0f;
        float phase2 = 0f;
        float phase3 = 0f;

        for (int i = 0; i < length; i++)
        {
            float time = (float)i / sampleRate;
            float progress = (float)i / length;
            float sample = 0f;

            // === PHASE 1: Turbo spool-up whine (0.0 - 0.3) ===
            if (progress < 0.3f)
            {
                float spoolT = progress / 0.3f;
                float spoolEnv = spoolT * spoolT; // ramp in
                // Rising whine from 800 Hz to 3500 Hz
                float freq = Mathf.Lerp(800f, 3500f, spoolT * spoolT);
                phase1 += freq / sampleRate;
                sample += Mathf.Sin(2f * Mathf.PI * phase1) * 0.3f * spoolEnv;
                // Sub-harmonic for body
                sample += Mathf.Sin(2f * Mathf.PI * phase1 * 0.5f) * 0.15f * spoolEnv;
            }

            // === PHASE 2: Blow-off whoosh (0.2 - 0.8) ===
            if (progress > 0.2f && progress < 0.8f)
            {
                float whooshT = (progress - 0.2f) / 0.6f;
                // Sharp attack, smooth decay
                float whooshEnv = whooshT < 0.15f
                    ? whooshT / 0.15f
                    : Mathf.Pow(1f - (whooshT - 0.15f) / 0.85f, 2f);

                // Filtered noise: band-pass via mixing two noise octaves
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float prevSample = i > 0 ? samples[i - 1] : 0f;
                // Simple low-pass to soften the noise
                float cutoff = Mathf.Lerp(0.6f, 0.15f, whooshT);
                float filtered = prevSample + cutoff * (noise - prevSample);

                sample += filtered * 0.5f * whooshEnv;

                // Descending resonant tone for the "pssshhh" character
                float blowFreq = Mathf.Lerp(2500f, 400f, whooshT);
                phase2 += blowFreq / sampleRate;
                sample += Mathf.Sin(2f * Mathf.PI * phase2) * 0.12f * whooshEnv;
            }

            // === PHASE 3: Low rumble/thrust feel (0.1 - 1.0) ===
            if (progress > 0.1f)
            {
                float rumbleT = (progress - 0.1f) / 0.9f;
                // Fade in then long fade out
                float rumbleEnv = rumbleT < 0.1f
                    ? rumbleT / 0.1f
                    : Mathf.Pow(1f - (rumbleT - 0.1f) / 0.9f, 1.5f);

                // Deep sub-bass pulse
                float rumbleFreq = Mathf.Lerp(120f, 60f, rumbleT);
                phase3 += rumbleFreq / sampleRate;
                sample += Mathf.Sin(2f * Mathf.PI * phase3) * 0.25f * rumbleEnv;
                // Second harmonic for fullness
                sample += Mathf.Sin(2f * Mathf.PI * phase3 * 2f) * 0.08f * rumbleEnv;
            }

            // === Tail: airy hiss fade-out (0.7 - 1.0) ===
            if (progress > 0.7f)
            {
                float tailT = (progress - 0.7f) / 0.3f;
                float tailEnv = 1f - tailT;
                tailEnv *= tailEnv;
                float hiss = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.15f * tailEnv;
                sample += hiss;
            }

            // Master soft-clip to prevent harshness
            sample = Mathf.Clamp(sample, -1f, 1f);
            sample = sample * (1.5f - 0.5f * sample * sample); // gentle saturation

            samples[i] = sample * 0.55f;
        }

        AudioClip clip = AudioClip.Create("Turbo", length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    static AudioClip GenerateCheckpointClip()
    {
        int sampleRate = 44100;
        int length = (int)(sampleRate * 0.3f); // 0.3 seconds
        float[] samples = new float[length];
        int half = length / 2;

        for (int i = 0; i < length; i++)
        {
            float time = (float)i / sampleRate;
            float freq = i < half ? 880f : 1760f;

            // quick decay within each tone
            float localProgress = i < half ? (float)i / half : (float)(i - half) / half;
            float envelope = 1f - localProgress * 0.5f;

            samples[i] = Mathf.Sin(2f * Mathf.PI * freq * time) * envelope * 0.5f;
        }

        AudioClip clip = AudioClip.Create("Checkpoint", length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

}
