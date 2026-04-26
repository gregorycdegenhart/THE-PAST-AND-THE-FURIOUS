using UnityEngine;
using UnityEngine.EventSystems;

public class UIAudioManager : MonoBehaviour
{
    public static UIAudioManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoSpawn()
    {
        if (Instance != null) return;
        var go = new GameObject("UIAudioManager");
        go.AddComponent<UIAudioManager>();
    }

    [Header("UI Sound Clips (optional - auto-generated if empty)")]
    public AudioClip hoverClip;
    public AudioClip clickClip;
    public AudioClip scrollClip;
    public AudioClip confirmClip;
    public AudioClip backClip;

    [Header("Volume")]
    public float volume = 0.4f;

    private AudioSource source;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = volume;
        // PauseMenu sets AudioListener.pause = true while paused. Without this flag,
        // UI hover/click sounds get muted along with the world audio — so the pause
        // menu and music widget would be silent on hover/click. MusicManager already
        // opts out the same way for its music source.
        source.ignoreListenerPause = true;

        if (hoverClip == null) hoverClip = GenerateHoverClip();
        if (clickClip == null) clickClip = GenerateClickClip();
        if (scrollClip == null) scrollClip = GenerateScrollClip();
        if (confirmClip == null) confirmClip = GenerateConfirmClip();
        if (backClip == null) backClip = GenerateBackClip();
    }

    public void PlayHover() => Play(hoverClip);
    public void PlayClick() => Play(clickClip);
    public void PlayScroll() => Play(scrollClip);
    public void PlayConfirm() => Play(confirmClip);
    public void PlayBack() => Play(backClip);

    void Play(AudioClip clip)
    {
        if (clip != null && source != null)
        {
            source.volume = volume;
            source.PlayOneShot(clip);
        }
    }

    // --- Procedural clip generators ---

    static AudioClip GenerateHoverClip()
    {
        // soft high-pitched tick
        int rate = 44100;
        int len = (int)(rate * 0.04f);
        float[] s = new float[len];
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / rate;
            float env = 1f - (float)i / len;
            s[i] = Mathf.Sin(2f * Mathf.PI * 2200f * t) * env * 0.3f;
        }
        var clip = AudioClip.Create("UI_Hover", len, 1, rate, false);
        clip.SetData(s, 0);
        return clip;
    }

    static AudioClip GenerateClickClip()
    {
        // punchy two-tone pop
        int rate = 44100;
        int len = (int)(rate * 0.08f);
        float[] s = new float[len];
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / rate;
            float progress = (float)i / len;
            float env = 1f - progress;
            env *= env;
            float freq = Mathf.Lerp(1400f, 800f, progress);
            s[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.5f;
        }
        var clip = AudioClip.Create("UI_Click", len, 1, rate, false);
        clip.SetData(s, 0);
        return clip;
    }

    static AudioClip GenerateScrollClip()
    {
        // quick subtle tick
        int rate = 44100;
        int len = (int)(rate * 0.025f);
        float[] s = new float[len];
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / rate;
            float env = 1f - (float)i / len;
            s[i] = Mathf.Sin(2f * Mathf.PI * 1800f * t) * env * 0.2f;
        }
        var clip = AudioClip.Create("UI_Scroll", len, 1, rate, false);
        clip.SetData(s, 0);
        return clip;
    }

    static AudioClip GenerateConfirmClip()
    {
        // rising two-note chime
        int rate = 44100;
        int len = (int)(rate * 0.2f);
        float[] s = new float[len];
        int half = len / 2;
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / rate;
            float freq = i < half ? 880f : 1320f;
            float localProg = i < half ? (float)i / half : (float)(i - half) / half;
            float env = 1f - localProg * 0.4f;
            s[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.4f;
        }
        var clip = AudioClip.Create("UI_Confirm", len, 1, rate, false);
        clip.SetData(s, 0);
        return clip;
    }

    static AudioClip GenerateBackClip()
    {
        // falling tone
        int rate = 44100;
        int len = (int)(rate * 0.12f);
        float[] s = new float[len];
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / rate;
            float progress = (float)i / len;
            float env = 1f - progress;
            float freq = Mathf.Lerp(900f, 400f, progress);
            s[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.4f;
        }
        var clip = AudioClip.Create("UI_Back", len, 1, rate, false);
        clip.SetData(s, 0);
        return clip;
    }
}
