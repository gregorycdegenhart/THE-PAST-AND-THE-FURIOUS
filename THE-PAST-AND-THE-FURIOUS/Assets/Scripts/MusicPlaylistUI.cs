using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MusicPlaylistUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI songNameText;
    public TextMeshProUGUI trackNumberText;
    public GameObject panel;

    [Header("Volume")]
    public Slider volumeSlider;

    [Header("Play/Pause Visual (optional, set ONE of these)")]
    [Tooltip("Option A: Two child GameObjects named 'PlayIcon' / 'PauseIcon' that the script toggles on/off. Auto-found by name if blank.")]
    public GameObject playIcon;
    public GameObject pauseIcon;
    [Tooltip("Option B: Two sprites the script swaps on the PlayPauseBtn's Image. Drag your play and pause icon sprites here.")]
    public Sprite playSprite;
    public Sprite pauseSprite;
    private Image playPauseImage;

    [Header("Auto-Hide")]
    public float showDuration = 4f;
    public bool alwaysVisible = false;

    private float showTimer = 0f;
    private bool isVisible = false;
    private string lastSongName = "";
    private TextMeshProUGUI playPauseText;
    private bool wasPaused = false;

    void Start()
    {
        if (panel != null)
        {
            panel.SetActive(true);

            WireButton("PrevTrackBtn", OnPreviousButton);
            WireButton("PlayPauseBtn", OnPlayPauseButton);
            WireButton("NextTrackBtn", OnNextButton);

            // Play/Pause visual swap. Try (in priority order):
            //   1. PlayIcon / PauseIcon child GameObjects → toggle visibility
            //   2. playSprite + pauseSprite assigned + Image child found → swap sprite
            //   3. TextMeshProUGUI label → flip ">" / "||"
            var ppBtn = FindDeep(panel.transform, "PlayPauseBtn");
            if (ppBtn != null)
            {
                if (playIcon == null)
                {
                    var t = FindDeep(ppBtn, "PlayIcon");
                    if (t != null) playIcon = t.gameObject;
                }
                if (pauseIcon == null)
                {
                    var t = FindDeep(ppBtn, "PauseIcon");
                    if (t != null) pauseIcon = t.gameObject;
                }
                // Find the icon Image (skip the button's own background Image — that's
                // attached to the button GameObject itself, not a child).
                foreach (var img in ppBtn.GetComponentsInChildren<Image>(true))
                {
                    if (img.transform == ppBtn) continue;
                    playPauseImage = img;
                    break;
                }
                playPauseText = ppBtn.GetComponentInChildren<TextMeshProUGUI>(true);
            }

            if (songNameText == null)
            {
                var t = FindDeep(panel.transform, "SongNameText");
                if (t != null) songNameText = t.GetComponent<TextMeshProUGUI>();
            }
            if (trackNumberText == null)
            {
                var t = FindDeep(panel.transform, "TrackNumberText");
                if (t != null) trackNumberText = t.GetComponent<TextMeshProUGUI>();
            }

            if (volumeSlider == null)
            {
                var t = FindDeep(panel.transform, "VolumeSlider");
                if (t != null) volumeSlider = t.GetComponent<Slider>();
            }
        }

        if (volumeSlider != null)
        {
            if (MusicManager.Instance != null)
                volumeSlider.value = MusicManager.Instance.volume;
            // Same persistent-listener detection as the buttons (see WireButton).
            if (volumeSlider.onValueChanged.GetPersistentEventCount() == 0)
            {
                volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
                volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            }
        }
        else
        {
            Debug.LogWarning($"[MusicPlaylistUI] No VolumeSlider found under '{(panel != null ? panel.name : name)}'. Add a child named 'VolumeSlider' (with a Slider component) to match other music players.");
        }

        UpdatePlayPauseLabel();
        ShowWidget();
    }

    void Update()
    {
        if (MusicManager.Instance == null) return;

        // Visibility rule:
        //   - In a level (Map1/2/3): show ONLY while paused.
        //   - Out of level (MainMenu / Garage / WinScene / etc.): always show.
        //
        // We detect "level" by scene name rather than RaceManager.Instance, because
        // Map2 and Map3 currently don't have a RaceManager (only Map1 does), so the
        // RaceManager-based check incorrectly classifies them as menu scenes and
        // leaves the widget visible during racing.
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        bool inLevel = sceneName.StartsWith("Map");
        bool paused = Time.timeScale == 0f;
        bool shouldShow = !inLevel || paused;

        if (shouldShow && !isVisible) ShowWidget();
        else if (!shouldShow && isVisible) HideWidget();

        UpdateDisplay();
    }

    void UpdateDisplay()
    {
        if (MusicManager.Instance == null) return;

        if (songNameText != null)
            songNameText.text = MusicManager.Instance.GetCurrentSongName();

        if (trackNumberText != null)
        {
            int current = MusicManager.Instance.GetCurrentTrackIndex() + 1;
            int total = MusicManager.Instance.GetTrackCount();
            trackNumberText.text = current + " / " + total;
        }

        UpdatePlayPauseLabel();
    }

    private CanvasGroup visibilityGroup;

    /// <summary>
    /// MusicPlaylistUI lives ON the panel GameObject in our prefab, so calling
    /// panel.SetActive(false) deactivates THIS script's own GameObject — which
    /// stops Update from running, stranding the widget hidden until next scene
    /// load. To hide without killing ourselves, route visibility through a
    /// CanvasGroup (alpha + interactable). The script keeps ticking; only the
    /// rendering and input handling go away.
    /// </summary>
    CanvasGroup GetVisibilityGroup()
    {
        if (visibilityGroup != null) return visibilityGroup;
        if (panel == null) return null;
        visibilityGroup = panel.GetComponent<CanvasGroup>();
        if (visibilityGroup == null)
            visibilityGroup = panel.AddComponent<CanvasGroup>();
        return visibilityGroup;
    }

    void ShowWidget()
    {
        var cg = GetVisibilityGroup();
        if (cg != null)
        {
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }
        if (panel != null && !panel.activeSelf)
            panel.SetActive(true);
        isVisible = true;
        showTimer = showDuration;
    }

    void HideWidget()
    {
        var cg = GetVisibilityGroup();
        if (cg != null)
        {
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
        }
        // NOTE: intentionally NOT calling panel.SetActive(false). The script lives
        // on `panel`, and deactivating self would freeze Update so we could never
        // re-show. Hiding via CanvasGroup keeps the script alive.
        isVisible = false;
    }

    public void OnNextButton()
    {
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.NextTrack();
            UpdatePlayPauseLabel();
            ShowWidget();
        }
    }

    public void OnPreviousButton()
    {
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.PreviousTrack();
            UpdatePlayPauseLabel();
            ShowWidget();
        }
    }

    public void OnPlayPauseButton()
    {
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.TogglePause();
            UpdatePlayPauseLabel();
            ShowWidget();
        }
    }

    void UpdatePlayPauseLabel()
    {
        if (MusicManager.Instance == null) return;
        bool paused = MusicManager.Instance.IsPaused();

        // Option A: dedicated child GameObjects.
        if (playIcon != null || pauseIcon != null)
        {
            if (playIcon != null) playIcon.SetActive(paused);
            if (pauseIcon != null) pauseIcon.SetActive(!paused);
            return;
        }

        // Option B: swap a single Image's sprite. Requires both sprites assigned.
        if (playPauseImage != null && playSprite != null && pauseSprite != null)
        {
            playPauseImage.sprite = paused ? playSprite : pauseSprite;
            return;
        }

        // Option C: text label.
        if (playPauseText != null)
            playPauseText.text = paused ? ">" : "||";
    }

    public void OnVolumeChanged(float value)
    {
        if (MusicManager.Instance != null)
            MusicManager.Instance.SetVolume(value);
    }

    public void OnToggleWidget()
    {
        if (isVisible) HideWidget();
        else ShowWidget();
    }

    static Transform FindDeep(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeep(parent.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    void WireButton(string name, UnityEngine.Events.UnityAction action)
    {
        Transform t = FindDeep(panel.transform, name);
        if (t == null)
        {
            Debug.LogWarning($"[MusicPlaylistUI] Button '{name}' not found under '{panel.name}'.");
            return;
        }
        Button btn = t.GetComponent<Button>();
        if (btn == null) return;

        // CRITICAL: Skip script-side wiring if the Inspector already wired this button.
        // Otherwise a click fires both listeners → e.g. NextTrack runs twice → skips two
        // random tracks per click ("skipping like crazy"). The MainMenu prefab has Inspector
        // OnClick wired; the in-map prefab does not. This makes the script work for both.
        if (btn.onClick.GetPersistentEventCount() > 0) return;

        btn.onClick.RemoveListener(action);
        btn.onClick.AddListener(action);
    }
}
