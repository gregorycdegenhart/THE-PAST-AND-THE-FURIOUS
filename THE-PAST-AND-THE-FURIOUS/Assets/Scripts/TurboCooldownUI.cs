using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Turbo charge meter. Fills up as cooldown elapses (0 → full), glows + pulses
/// when full (ready to use), and stays full + bright while turbo is firing.
/// Cleaner than the old color-shifting approach: a single visual language —
/// "is the bar full + glowing? you can boost."
/// </summary>
public class TurboCooldownUI : MonoBehaviour
{
    [Header("References")]
    public Image fillBar;
    public CarController carController;

    [Header("Fill Direction")]
    [Tooltip("How the bar fills as it charges. Horizontal = left-to-right, Vertical = bottom-to-top.")]
    public Image.FillMethod fillMethod = Image.FillMethod.Horizontal;
    [Tooltip("Which edge the fill starts from. For Horizontal: 0=Left, 1=Right. For Vertical: 0=Bottom, 1=Top.")]
    public int fillOrigin = 0;

    [Header("Bar Color")]
    [Tooltip("Bar color while charging (cooldown). Same color is brightened when ready/active.")]
    public Color barColor = new Color(0.25f, 0.85f, 1f, 1f);

    [Header("Empty Box (auto-styles fillBar's parent)")]
    [Tooltip("If true, the script auto-styles the fillBar's parent Image as a hollow 'empty box' frame so the empty/full contrast is obvious.")]
    public bool autoStyleFrame = true;
    [Tooltip("Background color of the empty box (the part visible when the meter is empty).")]
    public Color frameFillColor = new Color(0f, 0f, 0f, 0.55f);
    [Tooltip("Outline color around the empty box.")]
    public Color frameBorderColor = new Color(0.25f, 0.85f, 1f, 0.9f);
    [Tooltip("Outline thickness around the empty box.")]
    public float frameBorderWidth = 2f;

    [Header("Ready Glow")]
    [Tooltip("Glow color when the bar is full. Pulses while ready, solid while turbo is firing.")]
    public Color glowColor = new Color(0.4f, 1f, 1f, 1f);
    [Tooltip("Pulses per second while ready to fire.")]
    public float pulseRate = 1.5f;
    [Tooltip("Outline thickness range (px) — animated between min and max while pulsing.")]
    public float glowMinWidth = 1.5f;
    public float glowMaxWidth = 6f;
    [Tooltip("Brightness multiplier applied to the bar when ready or active.")]
    public float readyBrightness = 1.25f;

    private Outline glowOutline;

    private static Sprite s_whiteSprite;

    void Awake()
    {
        if (fillBar == null) return;

        // fillAmount only takes visual effect when Image.type is Filled AND the Image
        // has a sprite assigned. The scene was authoring the bar with NO sprite, which
        // makes Unity skip the fill clipping — that's why the bar looked full at all
        // times regardless of fillAmount. Force both: sprite + Filled type.
        if (fillBar.sprite == null)
            fillBar.sprite = GetWhiteSprite();
        fillBar.type = Image.Type.Filled;
        fillBar.fillMethod = fillMethod;
        fillBar.fillOrigin = fillOrigin;

        // Outline on the fill = the "glow" effect when full.
        glowOutline = fillBar.GetComponent<Outline>();
        if (glowOutline == null)
            glowOutline = fillBar.gameObject.AddComponent<Outline>();
        glowOutline.effectColor = glowColor;
        glowOutline.effectDistance = new Vector2(glowMinWidth, glowMinWidth);

        // Auto-style the parent (TurboBarBackground) into a clear empty-box frame so
        // the cooldown drain is visually obvious. Default existing color (dark red)
        // doesn't read as "empty container" — restyle to a translucent dark fill with
        // a cyan outline border that's always visible. Also force a sprite on the
        // background so its solid fill renders.
        if (autoStyleFrame)
        {
            Transform parent = fillBar.transform.parent;
            if (parent != null)
            {
                Image frame = parent.GetComponent<Image>();
                if (frame != null)
                {
                    if (frame.sprite == null) frame.sprite = GetWhiteSprite();
                    frame.color = frameFillColor;
                    Outline frameOutline = frame.GetComponent<Outline>();
                    if (frameOutline == null)
                        frameOutline = frame.gameObject.AddComponent<Outline>();
                    frameOutline.effectColor = frameBorderColor;
                    frameOutline.effectDistance = new Vector2(frameBorderWidth, frameBorderWidth);
                    frameOutline.enabled = true;
                }
            }
        }
    }

    /// <summary>
    /// Returns a cached 2x2 white sprite. Used as a fallback when an Image has no
    /// sprite assigned — necessary for fillAmount clipping to render visually.
    /// </summary>
    static Sprite GetWhiteSprite()
    {
        if (s_whiteSprite != null) return s_whiteSprite;
        Texture2D tex = new Texture2D(2, 2);
        tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        tex.Apply();
        s_whiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
        s_whiteSprite.name = "TurboCooldownUI.WhiteFallback";
        return s_whiteSprite;
    }

    void Update()
    {
        if (fillBar == null || carController == null) return;

        bool active = carController.IsTurboActive();
        float cooldown = carController.GetCooldownProgress(); // 0 = ready, 1 = just used
        bool ready = !active && cooldown <= 0f;

        // FILL: 0 when freshly used → 1 when fully charged. Stays full during active boost.
        fillBar.fillAmount = active ? 1f : (1f - cooldown);

        // COLOR: same hue throughout, brighter when ready or actively boosting.
        Color c = barColor;
        if (ready || active)
            c = new Color(
                Mathf.Clamp01(barColor.r * readyBrightness),
                Mathf.Clamp01(barColor.g * readyBrightness),
                Mathf.Clamp01(barColor.b * readyBrightness),
                barColor.a);
        fillBar.color = c;

        // GLOW: off while cooling, pulsing while ready (calls attention to "you can boost!"),
        // solid + thick while actively boosting (signals "boost firing").
        if (glowOutline == null) return;

        if (ready)
        {
            float t = Mathf.Sin(Time.unscaledTime * pulseRate * Mathf.PI * 2f) * 0.5f + 0.5f;
            float w = Mathf.Lerp(glowMinWidth, glowMaxWidth, t);
            float a = Mathf.Lerp(0.4f, 1f, t);
            glowOutline.effectDistance = new Vector2(w, w);
            Color g = glowColor; g.a *= a;
            glowOutline.effectColor = g;
            glowOutline.enabled = true;
        }
        else if (active)
        {
            glowOutline.effectDistance = new Vector2(glowMaxWidth, glowMaxWidth);
            glowOutline.effectColor = glowColor;
            glowOutline.enabled = true;
        }
        else
        {
            glowOutline.enabled = false;
        }
    }
}
