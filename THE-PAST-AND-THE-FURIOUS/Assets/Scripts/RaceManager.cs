using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;

public class RaceManager : MonoBehaviour
{
    // race type
    public enum RaceType
    {
        Laps,
        Checkpoints
    }

    [Header("Race Settings")]
    public RaceType raceType = RaceType.Laps;

    [Tooltip("For laps mode only")]
    public int totalLaps = 3;

    [Tooltip("For checkpoints mode only: last checkpoint index triggers finish")]
    public int finalCheckpointIndex = 2;

    [Header("References")]
    public PlayerInput playerInput;
    public CarController carController;
    public Rigidbody carRigidbody;
    public Transform carTransform;
    public CanvasGroup fadeGroup;

    [Header("Finish Settings")]
    public float finishCruiseSpeed = 12f;
    public float delayBeforeFade = 1f;
    public float fadeDuration = 1f;
    public string nextSceneName;
    
    [Header("Game Over")]
    public GameOverScreen gameOverScreen;
    public float raceTimeLimit = 0f; // 0 = no limit

    [Tooltip("If this many AI finish before the player, the player loses (4th place or worse). Set to 0 to disable.")]
    public int loseIfBeatenByCount = 3;

    [Header("UI")]
    public TextMeshProUGUI lapText;

    // internal state
    private float raceElapsed = 0f;
    private int currentLap = 1;
    private int currentCheckpoint = 0;      // for checkpoints mode
    private bool raceFinished = false;
    private int aiFinishCount = 0;
    private int nonFinishCheckpointCount = -1; // computed once on first lap completion
    private System.Collections.Generic.HashSet<int> visitedCheckpoints = new System.Collections.Generic.HashSet<int>();

    void Awake()
    {
        // singleton pattern for easy access
        if (Instance != null && Instance != this)
            Destroy(gameObject);
        Instance = this;

        // make sure fade starts invisible
        if (fadeGroup != null)
            fadeGroup.alpha = 0f;

        // Auto-find lap text if not wired
        if (lapText == null)
        {
            var go = GameObject.Find("LapText");
            if (go != null) lapText = go.GetComponent<TMPro.TextMeshProUGUI>();
        }

        AutoFindPlayerRefs();
        UpdateLapUI();
    }

    void AutoFindPlayerRefs()
    {
        if (carController != null && carRigidbody != null && carTransform != null && playerInput != null) return;
        var player = GameObject.FindWithTag("Player");
        if (player == null) return;
        if (carController == null) carController = player.GetComponent<CarController>() ?? player.GetComponentInChildren<CarController>();
        if (carRigidbody == null) carRigidbody = player.GetComponent<Rigidbody>() ?? player.GetComponentInChildren<Rigidbody>();
        if (carTransform == null) carTransform = player.transform;
        if (playerInput == null) playerInput = player.GetComponent<PlayerInput>() ?? player.GetComponentInChildren<PlayerInput>();
    }

    public static RaceManager Instance { get; private set; }
    public bool IsRaceFinished => raceFinished;
    public int GetVisitedCount() => visitedCheckpoints.Count;
    public int GetAIFinishCount() => aiFinishCount;
    public int GetPlayerPosition() => aiFinishCount + 1; // 1-based; called when the player crosses the line
    public int CurrentLap => currentLap;

    /// <summary>
    /// Each AI calls this when it crosses the finish line. Used to compute the player's
    /// final position when they finish (player position = aiFinishCount + 1).
    /// </summary>
    public void RegisterAIFinish()
    {
        aiFinishCount++;
    }

    void Update()
    {
        if (raceFinished || !CountdownUI.RaceStarted) return;

        if (raceTimeLimit > 0f)
        {
            raceElapsed += Time.deltaTime;
            if (raceElapsed >= raceTimeLimit)
            {
                TriggerGameOver("Time's Up!");
            }
        }
    }

    public void TriggerGameOver(string reason = "Game Over!")
    {
        if (raceFinished) return;
        raceFinished = true;

        if (playerInput != null)
            playerInput.DeactivateInput();
        if (carController != null)
            carController.enabled = false;

        if (gameOverScreen != null)
            gameOverScreen.ShowGameOver(reason);
    }

    // called by LapCheckpoint triggers
    public void HitCheckpoint(int checkpointIndex, bool completesLap = false)
    {
        if (raceFinished) return;

        switch (raceType)
        {
            case RaceType.Laps:
                if (completesLap)
                {
                    // Only complete a lap if all non-finish checkpoints have been visited
                    // (On the very first crossing, visitedCheckpoints may be incomplete —
                    //  that's fine, it just won't count as a lap yet)
                    int requiredCount = GetNonFinishCheckpointCount();
                    if (visitedCheckpoints.Count >= requiredCount)
                    {
                        currentLap++;
                        UpdateLapUI();
                        visitedCheckpoints.Clear();

                        if (currentLap > totalLaps)
                            StartCoroutine(FinishRace());
                    }
                }
                else
                {
                    visitedCheckpoints.Add(checkpointIndex);
                }
                break;

            case RaceType.Checkpoints:
                // linear checkpoints: enforce order
                if (checkpointIndex != currentCheckpoint)
                    return;

                currentCheckpoint++;
                UpdateLapUI();

                if (checkpointIndex == finalCheckpointIndex)
                    StartCoroutine(FinishRace());
                break;
        }
    }

    private IEnumerator FinishRace()
    {
        // Lock in the placement at the EXACT moment the player crosses the line. Any AI
        // that finishes during the coast-out / fade no longer affects the saved position.
        // (Setting raceFinished AFTER this — also at the same instant — locks in the time
        // for the same reason: RaceTimer reads IsRaceFinished and saves on the next frame.)
        int finalPos = aiFinishCount + 1;
        PlayerPrefs.SetInt("FinalPosition", finalPos);
        PlayerPrefs.SetInt("FinalPosition_" + SceneManager.GetActiveScene().name, finalPos);
        PlayerPrefs.Save();

        raceFinished = true;

        // disable player input
        if (playerInput != null)
            playerInput.DeactivateInput();

        // disable car controller
        if (carController != null)
            carController.enabled = false;

        // keep car moving forward at a steady speed
        if (carRigidbody != null && carTransform != null)
        {
            carRigidbody.linearVelocity = carTransform.forward * finishCruiseSpeed;
            carRigidbody.angularVelocity = Vector3.zero;
        }

        if (delayBeforeFade > 0f)
            yield return new WaitForSecondsRealtime(delayBeforeFade);

        // Fade to black before loading the next scene
        if (fadeGroup != null)
        {
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                fadeGroup.alpha = Mathf.Lerp(0f, 1f, t / fadeDuration);
                yield return null;
            }
            fadeGroup.alpha = 1f;
        }

        // Compute next scene by cycling through Map1/2/3 from the player's chosen starting map.
        // After 3 maps played, head to the final win cutscene.
        int played = PlayerPrefs.GetInt("MapsPlayed", 0) + 1;
        PlayerPrefs.SetInt("MapsPlayed", played);
        PlayerPrefs.Save();
        string nextScene;
        if (played >= 3)
        {
            nextScene = "AztecWinScene";
        }
        else
        {
            int startIdx = PlayerPrefs.GetInt("StartMapIdx", 0);
            int nextIdx = (startIdx + played) % 3;
            nextScene = "Map" + (nextIdx + 1);
        }
        SceneManager.LoadScene(nextScene);
    }

    static string PositionSuffix(int n)
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

    int GetNonFinishCheckpointCount()
    {
        // Cache the count after the first scan — checkpoints don't appear/disappear
        // mid-race, so re-scanning the scene on every lap completion was wasted work.
        if (nonFinishCheckpointCount < 0)
        {
            int count = 0;
            foreach (var cp in Object.FindObjectsByType<LapCheckpoint>(FindObjectsSortMode.None))
            {
                if (!cp.completesLap) count++;
            }
            nonFinishCheckpointCount = count;
        }
        return nonFinishCheckpointCount;
    }

    void UpdateLapUI()
    {
        if (lapText == null) return;
        if (raceType == RaceType.Checkpoints)
            lapText.text = "Checkpoint " + Mathf.Min(currentCheckpoint, finalCheckpointIndex + 1) + " / " + (finalCheckpointIndex + 1);
        else
            lapText.text = "Lap " + Mathf.Min(currentLap, totalLaps) + " / " + totalLaps;
    }
}