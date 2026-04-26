using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoSpawn()
    {
        if (Instance != null) return;
        var prefab = Resources.Load<MusicManager>("MusicManager");
        if (prefab != null)
            Instantiate(prefab);
        else
        {
            var go = new GameObject("MusicManager");
            go.AddComponent<MusicManager>();
        }
    }

    [Header("Playlist")]
    [Tooltip("Drag your music AudioClips here")]
    public AudioClip[] playlist;
    public string[] songNames;

    [Header("Settings")]
    public float volume = 0.5f;
    public bool shuffle = true;

    [Tooltip("Minimum seconds between user-driven track changes (Next/Prev/Pause clicks). " +
             "Prevents rapid-fire calls — duplicate listeners, fast double-clicks, scene-reload " +
             "ghost listeners — from cascading into 3-song skips.")]
    public float userActionCooldown = 0.25f;

    private AudioSource musicSource;
    private int currentTrackIndex = 0;
    private bool isPaused = false;
    private float trackStartTime = -1f;
    private float lastUserActionTime = -10f;
    private float lastTimeIsPlayingWasTrue = -1f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = false;
        musicSource.spatialBlend = 0f;
        musicSource.playOnAwake = false;
        musicSource.ignoreListenerPause = true;

        if (PlayerPrefs.HasKey("MusicVolume"))
            volume = PlayerPrefs.GetFloat("MusicVolume");
        musicSource.volume = volume;

        if (playlist == null || playlist.Length == 0)
        {
            AudioClip[] loaded = Resources.LoadAll<AudioClip>("Music");
            if (loaded != null && loaded.Length > 0)
                playlist = loaded;
        }

        SpawnPersistentWidget();
    }

    /// <summary>
    /// Loads Resources/MusicPlaylistWidget.prefab and instantiates it as a child of
    /// this MusicManager. Because MusicManager is DontDestroyOnLoad, the widget
    /// persists across scenes — same instance, same anchors, same look everywhere.
    ///
    /// We create a Screen Space - Overlay Canvas to host it. Widget prefabs are just
    /// the widget GameObject itself (no Canvas), and UI doesn't render outside a
    /// Canvas — without this wrapper, the persistent widget is silently invisible.
    ///
    /// To use this:
    ///   1. In Unity, drag the working MusicPlaylistWidget GameObject from a scene
    ///      into Assets/Resources/ to create the prefab. (No Canvas needed in the
    ///      prefab — this code creates one.)
    ///   2. Delete the in-scene MusicPlaylistWidget from each scene so only the
    ///      persistent widget exists at runtime.
    /// </summary>
    void SpawnPersistentWidget()
    {
        // Already spawned in this game session? Don't spawn another.
        if (GetComponentInChildren<MusicPlaylistUI>(true) != null) return;

        GameObject prefab = Resources.Load<GameObject>("MusicPlaylistWidget");
        if (prefab == null) return; // not authored yet — per-scene widgets handle it

        // 1. Canvas wrapper. Sortable above scene UI via a high sortingOrder so the
        //    widget always shows on top of MainMenuCanvas / RaceCanvas etc.
        GameObject canvasGO = new GameObject("MusicWidgetCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // 2. Instantiate the widget under the canvas.
        GameObject instance = Instantiate(prefab, canvasGO.transform);
        instance.name = "MusicPlaylistWidget (Persistent)";
    }

    void Start()
    {
        if (playlist != null && playlist.Length > 0)
        {
            if (shuffle)
                currentTrackIndex = Random.Range(0, playlist.Length);
            PlayTrack(currentTrackIndex);
        }
    }

    void Update()
    {
        if (musicSource == null || isPaused || playlist == null || playlist.Length == 0) return;

        // Robust auto-advance: detect "song ended" via time-since-last-isPlaying-true.
        //
        // Why not just `if (!musicSource.isPlaying) NextTrackInternal()`:
        // - MP3 import has preloadAudioData=0, so calling Play() triggers a load
        //   gap (~10–100ms). isPlaying returns false during that window.
        // - Even after the song starts, isPlaying can flicker false for a single
        //   frame on buffer transitions (especially with Time.timeScale = 0).
        // Either case used to cascade into rapid auto-advance: every false read
        // fired NextTrackInternal, picked a new random track, restarted the load
        // gap, repeat. User saw 3–5 songs flash by per click.
        //
        // Fix: stamp lastTimeIsPlayingWasTrue every frame isPlaying is true. Only
        // auto-advance if (a) we've seen isPlaying=true at least once for the
        // current track AND (b) it's been false for a sustained ~0.5s.
        if (musicSource.isPlaying)
        {
            lastTimeIsPlayingWasTrue = Time.unscaledTime;
            return;
        }

        bool everPlayedThisTrack = lastTimeIsPlayingWasTrue > trackStartTime;
        if (!everPlayedThisTrack) return; // still in initial load gap

        if (Time.unscaledTime - lastTimeIsPlayingWasTrue > 0.5f)
            NextTrackInternal();
    }

    public void PlayTrack(int index)
    {
        if (playlist == null || playlist.Length == 0) return;

        currentTrackIndex = Mathf.Clamp(index, 0, playlist.Length - 1);
        musicSource.clip = playlist[currentTrackIndex];
        musicSource.Play();
        isPaused = false;
        trackStartTime = Time.unscaledTime;
        // Arming: lastTimeIsPlayingWasTrue is from the PREVIOUS track (or -1 initially),
        // so `lastTimeIsPlayingWasTrue > trackStartTime` is now false. Update will wait
        // until isPlaying becomes true for THIS track before allowing auto-advance.

    }

    /// <summary>
    /// User-facing Next: always sequential (idx+1), even if `shuffle` is on. Shuffle
    /// applies only to auto-advance when a track ends naturally — clicking Next is
    /// predictable so the player can step through the playlist one song at a time.
    /// Cooldown-gated to absorb duplicate listener fires or rapid double-clicks.
    /// </summary>
    public void NextTrack()
    {
        if (Time.unscaledTime - lastUserActionTime < userActionCooldown) return;
        lastUserActionTime = Time.unscaledTime;
        if (playlist == null || playlist.Length == 0) return;
        currentTrackIndex = (currentTrackIndex + 1) % playlist.Length;
        PlayTrack(currentTrackIndex);
    }

    /// <summary>
    /// User-facing Previous: always sequential (idx-1), wraps at 0. Cooldown-gated.
    /// </summary>
    public void PreviousTrack()
    {
        if (Time.unscaledTime - lastUserActionTime < userActionCooldown) return;
        lastUserActionTime = Time.unscaledTime;
        if (playlist == null || playlist.Length == 0) return;
        currentTrackIndex--;
        if (currentTrackIndex < 0)
            currentTrackIndex = playlist.Length - 1;
        PlayTrack(currentTrackIndex);
    }

    /// <summary>
    /// Internal Next: no cooldown. Used by the auto-advance path so the song-end transition
    /// isn't accidentally suppressed by a recent user click.
    /// </summary>
    private void NextTrackInternal()
    {
        if (playlist == null || playlist.Length == 0) return;

        if (shuffle)
            currentTrackIndex = Random.Range(0, playlist.Length);
        else
            currentTrackIndex = (currentTrackIndex + 1) % playlist.Length;

        PlayTrack(currentTrackIndex);
    }

    /// <summary>
    /// Pause/resume toggle: cooldown-gated.
    /// </summary>
    public void TogglePause()
    {
        if (Time.unscaledTime - lastUserActionTime < userActionCooldown) return;
        lastUserActionTime = Time.unscaledTime;

        if (isPaused)
        {
            musicSource.UnPause();
            isPaused = false;
        }
        else
        {
            musicSource.Pause();
            isPaused = true;
        }
    }

    public void SetVolume(float vol)
    {
        volume = Mathf.Clamp01(vol);
        musicSource.volume = volume;
        PlayerPrefs.SetFloat("MusicVolume", volume);
    }

    public string GetCurrentSongName()
    {
        if (songNames != null && currentTrackIndex < songNames.Length && !string.IsNullOrEmpty(songNames[currentTrackIndex]))
            return songNames[currentTrackIndex];

        if (playlist != null && currentTrackIndex < playlist.Length && playlist[currentTrackIndex] != null)
            return playlist[currentTrackIndex].name;

        return "No Track";
    }

    public int GetCurrentTrackIndex() => currentTrackIndex;
    public int GetTrackCount() => playlist != null ? playlist.Length : 0;
    public bool IsPlaying() => musicSource != null && musicSource.isPlaying;
    public bool IsPaused() => isPaused;
}
