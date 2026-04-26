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

        UpdateLapUI();
    }

    public static RaceManager Instance { get; private set; }
    public bool IsRaceFinished => raceFinished;
    public int GetVisitedCount() => visitedCheckpoints.Count;
    public int GetAIFinishCount() => aiFinishCount;
    public int GetPlayerPosition() => aiFinishCount + 1; // 1-based; called when the player crosses the line

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
                        Debug.Log("Lap completed: " + currentLap);
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
                Debug.Log("Checkpoint reached: " + checkpointIndex);

                if (checkpointIndex == finalCheckpointIndex)
                    StartCoroutine(FinishRace());
                break;
        }
    }

    private IEnumerator FinishRace()
    {
        raceFinished = true;

        // Lose if too many AI finished ahead of us. We check here (after raceFinished is set)
        // so the player still gets the coast-out animation, then game over takes over.
        bool playerLost = loseIfBeatenByCount > 0 && aiFinishCount >= loseIfBeatenByCount;

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

        // Loss path: show GameOverScreen with the player's position. Skip the fade — the
        // game-over panel takes over the screen and a black overlay would just hide it.
        if (playerLost)
        {
            int pos = aiFinishCount + 1;
            string suffix = PositionSuffix(pos);
            if (gameOverScreen != null)
                gameOverScreen.ShowGameOver($"You finished {pos}{suffix}");
            yield break;
        }

        // Win path: fade to black, then load the next scene.
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

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
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
        int count = 0;
        foreach (var cp in Object.FindObjectsByType<LapCheckpoint>(FindObjectsSortMode.None))
        {
            if (!cp.completesLap) count++;
        }
        return count;
    }

    void UpdateLapUI() 
    { 
        if (lapText != null) lapText.text = "Lap " + currentLap + " / " + totalLaps; 
    }
}