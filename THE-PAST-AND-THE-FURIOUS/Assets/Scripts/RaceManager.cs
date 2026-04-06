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
    
    [Header("UI")]
    public TextMeshProUGUI lapText;

    // internal state
    private int currentLap = 1;
    private int nextExpectedCheckpoint = 0; // for laps mode
    private int currentCheckpoint = 0;      // for checkpoints mode
    private bool raceFinished = false;

    void Awake()
    {
        // singleton pattern for easy access
        if (Instance != null && Instance != this)
            Destroy(gameObject);
        Instance = this;

        // make sure fade starts invisible
        if (fadeGroup != null)
            fadeGroup.alpha = 0f;
        
        UpdateLapUI();
    }

    public static RaceManager Instance { get; private set; }

    // called by LapCheckpoint triggers
    public void HitCheckpoint(int checkpointIndex, bool completesLap = false)
    {
        if (raceFinished) return;

        switch (raceType)
        {
            case RaceType.Laps:
            // enforce ordered checkpoints
                if (checkpointIndex != nextExpectedCheckpoint)
                    return;

                // if this checkpoint is the one that ends the lap
                if (completesLap)
                {
                    currentLap++;
                    UpdateLapUI();

                    Debug.Log("Lap completed: " + currentLap);

                    nextExpectedCheckpoint = 0;

                    if (currentLap > totalLaps)
                    {
                        StartCoroutine(FinishRace());
                    }
                }
                else
                {
                    nextExpectedCheckpoint++;
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

        // fade to black
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

        // load next scene if set
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }

    void UpdateLapUI() 
    { 
        if (lapText != null) lapText.text = "Lap " + currentLap + " / " + totalLaps; 
    }
}