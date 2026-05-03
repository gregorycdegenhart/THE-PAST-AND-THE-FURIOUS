using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class RespawnManager : MonoBehaviour
{
    public static RespawnManager Instance { get; private set; }

    [Header("References")]
    public PlayerInput playerInput;
    public CarController carController;
    public Rigidbody carRigidbody;
    public Transform carTransform;
    public CanvasGroup fadeGroup;

    [Header("Respawn Settings")]
    [Tooltip("How fast the screen fades to black and back.")]
    public float fadeDuration = 0.4f;

    [Tooltip("Small height offset so the car doesn't spawn inside the ground.")]
    public float spawnHeightOffset = 0.5f;

    private bool isRespawning = false;
    private bool hasCheckpoint = false;
    private Vector3 lastCheckpointPosition;
    private Quaternion lastCheckpointRotation;
    private Vector3 defaultSpawnPosition;
    private Quaternion defaultSpawnRotation;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (fadeGroup != null)
            fadeGroup.alpha = 0f;

        if (carController == null || carRigidbody == null || carTransform == null || playerInput == null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                if (carController == null) carController = player.GetComponent<CarController>() ?? player.GetComponentInChildren<CarController>();
                if (carRigidbody == null) carRigidbody = player.GetComponent<Rigidbody>() ?? player.GetComponentInChildren<Rigidbody>();
                if (carTransform == null) carTransform = player.transform;
                if (playerInput == null) playerInput = player.GetComponent<PlayerInput>() ?? player.GetComponentInChildren<PlayerInput>();
            }
        }
    }

    void Start()
    {
        if (carTransform == null)
        {
            Debug.LogError("[RespawnManager] carTransform still null in Start — no Player-tagged GameObject in scene.");
            return;
        }
        defaultSpawnPosition = carTransform.position;
        defaultSpawnRotation = carTransform.rotation;
    }

    public void UpdateLastCheckpoint(Transform checkpointTransform, Quaternion carRotation)
    {
        lastCheckpointPosition = checkpointTransform.position;
        lastCheckpointRotation = carRotation;
        hasCheckpoint = true;
    }

    public void TriggerRespawn()
    {
        if (isRespawning || RaceManager.Instance == null || RaceManager.Instance.IsRaceFinished) return;
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        isRespawning = true;

        if (playerInput != null) playerInput.DeactivateInput();
        if (carController != null) carController.enabled = false;

        if (carRigidbody != null)
        {
            carRigidbody.linearVelocity = Vector3.zero;
            carRigidbody.angularVelocity = Vector3.zero;
        }

        yield return StartCoroutine(Fade(0f, 1f, fadeDuration));

        if (carRigidbody != null)
        {
            carRigidbody.linearVelocity = Vector3.zero;
            carRigidbody.angularVelocity = Vector3.zero;
        }

        if (hasCheckpoint)
        {
            carTransform.SetPositionAndRotation(
                lastCheckpointPosition + Vector3.up * spawnHeightOffset,
                lastCheckpointRotation);
        }
        else
        {
            carTransform.SetPositionAndRotation(
                defaultSpawnPosition + Vector3.up * spawnHeightOffset,
                defaultSpawnRotation);
        }

        yield return new WaitForSecondsRealtime(0.1f);

        if (carController != null) carController.enabled = true;
        if (playerInput != null) playerInput.ActivateInput();

        yield return StartCoroutine(Fade(1f, 0f, fadeDuration));

        isRespawning = false;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (fadeGroup == null) yield break;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            fadeGroup.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        fadeGroup.alpha = to;
    }
}