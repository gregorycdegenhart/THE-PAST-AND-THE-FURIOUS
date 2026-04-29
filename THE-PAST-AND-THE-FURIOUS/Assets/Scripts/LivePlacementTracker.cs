using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Live placement display. Computes the player's current rank vs all AI cars each frame
/// and renders it as "1st / 8". Drop on any GameObject — auto-finds the player, the waypoint
/// path, and the placement text by tag/type/name. Inspector references override the auto-find.
/// </summary>
public class LivePlacementTracker : MonoBehaviour
{
    [Header("References (auto-found if left empty)")]
    [Tooltip("The player's car transform. Auto-find: GameObject tagged \"Player\".")]
    public Transform playerTransform;

    [Tooltip("The shared waypoint path that AI follows. Auto-find: first AIWaypointPath in the scene.")]
    public AIWaypointPath waypointPath;

    [Tooltip("Where to render the placement text. Auto-find: a TextMeshProUGUI named \"PlacementNumberTxt\" or \"PlacementText\" in the scene.")]
    public TextMeshProUGUI placementText;

    [Header("Display")]
    [Tooltip("If true, the text shows 'X / N' (e.g. '3rd / 8'). If false, just 'X' (e.g. '3rd').")]
    public bool showTotal = false;

    [Tooltip("How often (seconds) to recompute placement. 0.1 keeps it smooth without burning CPU.")]
    public float updateInterval = 0.1f;

    private AICarController[] aiCars;
    private float lastUpdate = -999f;

    static readonly string[] PlacementTextNames = { "PlacementNumberTxt", "PlacementText", "PositionText", "PlaceText" };

    // Auto-spawn a tracker into any race scene so the placement UI just works without Inspector wiring.
    // Race scene = one that contains an AIRaceGridSpawner. SubsystemRegistration runs once at game
    // start; the sceneLoaded callback then fires for every scene load (including manual ones).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void RegisterAutoSpawn()
    {
        SceneManager.sceneLoaded -= OnSceneLoadedAutoSpawn;
        SceneManager.sceneLoaded += OnSceneLoadedAutoSpawn;
    }

    static void OnSceneLoadedAutoSpawn(Scene scene, LoadSceneMode mode)
    {
        if (FindFirstObjectByType<LivePlacementTracker>() != null) return;
        if (!scene.name.StartsWith("Map")) return; // only race scenes; skip MainMenu / Garage / WinScene
        var go = new GameObject("LivePlacementTracker (auto)");
        go.AddComponent<LivePlacementTracker>();
    }

    void Start()
    {
        // Cache AI cars once. AIRaceGridSpawner spawns them in Awake, so they're alive by Start.
        aiCars = FindObjectsByType<AICarController>(FindObjectsSortMode.None);

        // Auto-find references when not wired in the inspector.
        if (playerTransform == null)
        {
            GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null) playerTransform = playerGO.transform;
        }
        if (waypointPath == null)
        {
            waypointPath = FindFirstObjectByType<AIWaypointPath>();
        }
        if (placementText == null)
        {
            foreach (string name in PlacementTextNames)
            {
                GameObject go = GameObject.Find(name);
                if (go == null) continue;
                placementText = go.GetComponent<TextMeshProUGUI>();
                if (placementText != null) break;
            }
            // Fallback: find any TextMeshProUGUI whose name contains "place" or "position".
            if (placementText == null)
            {
                foreach (var t in FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None))
                {
                    if (t == null) continue;
                    string n = t.name.ToLower();
                    if (n.Contains("place") || n.Contains("position") || n.Contains("rank"))
                    {
                        placementText = t;
                        break;
                    }
                }
            }
        }

        if (placementText == null)
            Debug.LogWarning("[LivePlacementTracker] No placement text found. Wire `placementText` in the inspector or rename the UI text to PlacementNumberTxt.");
    }

    void Update()
    {
        if (Time.unscaledTime - lastUpdate < updateInterval) return;
        lastUpdate = Time.unscaledTime;

        if (placementText == null) return;
        if (waypointPath == null || waypointPath.WaypointCount == 0) return;
        if (playerTransform == null) return;

        int total = 1 + (aiCars != null ? aiCars.Length : 0);
        int rank = ComputePlayerRank();

        placementText.text = showTotal
            ? $"{rank}{PositionSuffix(rank)} / {total}"
            : $"{rank}{PositionSuffix(rank)}";
    }

    /// <summary>
    /// Computes the player's current rank by sorting all racers (player + AI) on race progress
    /// and returning where the player landed (1-based).
    /// </summary>
    private int ComputePlayerRank()
    {
        int wpCount = waypointPath.WaypointCount;

        // Player progress = laps_completed * waypointCount + nearest_waypoint_index.
        // Approximate but good enough for live display — no segment-distance interpolation needed.
        int playerLap = RaceManager.Instance != null ? RaceManager.Instance.CurrentLap : 1;
        int playerWp = NearestWaypointIndex(playerTransform.position);
        long playerProgress = (long)(playerLap - 1) * wpCount + playerWp;

        int rank = 1;
        if (aiCars != null)
        {
            foreach (var ai in aiCars)
            {
                if (ai == null) continue;
                long aiProgress = (long)(ai.CurrentLap - 1) * wpCount + ai.CurrentWaypointIndex;
                if (aiProgress > playerProgress) rank++;
            }
        }
        return rank;
    }

    private int NearestWaypointIndex(Vector3 pos)
    {
        int nearest = 0;
        float nearestDist = float.MaxValue;
        for (int i = 0; i < waypointPath.WaypointCount; i++)
        {
            Transform wp = waypointPath.GetWaypoint(i);
            if (wp == null) continue;
            float d = (wp.position - pos).sqrMagnitude;
            if (d < nearestDist) { nearestDist = d; nearest = i; }
        }
        return nearest;
    }

    private static string PositionSuffix(int n)
    {
        int mod100 = n % 100;
        if (mod100 >= 11 && mod100 <= 13) return "th";
        switch (n % 10)
        {
            case 1: return "st";
            case 2: return "nd";
            case 3: return "rd";
            default: return "th";
        }
    }
}
