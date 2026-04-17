using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoSpawn()
    {
        if (Instance != null) return;
        // Try to load from Resources, otherwise create empty
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

    private AudioSource musicSource;
    private int currentTrackIndex = 0;
    private bool isPaused = false;

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

        // Restore saved volume
        if (PlayerPrefs.HasKey("MusicVolume"))
            volume = PlayerPrefs.GetFloat("MusicVolume");
        musicSource.volume = volume;

        // Auto-load playlist from Resources/Music if none assigned
        if (playlist == null || playlist.Length == 0)
        {
            AudioClip[] loaded = Resources.LoadAll<AudioClip>("Music");
            if (loaded != null && loaded.Length > 0)
                playlist = loaded;
        }
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
        if (musicSource != null && !isPaused && !musicSource.isPlaying && playlist != null && playlist.Length > 0)
            NextTrack();
    }

    public void PlayTrack(int index)
    {
        if (playlist == null || playlist.Length == 0) return;

        currentTrackIndex = Mathf.Clamp(index, 0, playlist.Length - 1);
        musicSource.clip = playlist[currentTrackIndex];
        musicSource.Play();
        isPaused = false;
    }

    public void NextTrack()
    {
        if (playlist == null || playlist.Length == 0) return;

        if (shuffle)
            currentTrackIndex = Random.Range(0, playlist.Length);
        else
            currentTrackIndex = (currentTrackIndex + 1) % playlist.Length;

        PlayTrack(currentTrackIndex);
    }

    public void PreviousTrack()
    {
        if (playlist == null || playlist.Length == 0) return;

        currentTrackIndex--;
        if (currentTrackIndex < 0)
            currentTrackIndex = playlist.Length - 1;

        PlayTrack(currentTrackIndex);
    }

    public void TogglePause()
    {
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
