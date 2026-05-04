using UnityEngine;

[DefaultExecutionOrder(-100)]
public class CarSpawner : MonoBehaviour
{
    [Tooltip("Car 1 — top-arrow index 0 in the garage. PlayerPrefs 'SelectedCarColor' = 0.")]
    public GameObject orangeCarPrefab;

    [Tooltip("Car 2 — top-arrow index 1 in the garage. PlayerPrefs 'SelectedCarColor' = 1.")]
    public GameObject pinkCarPrefab;

    [Tooltip("Car 3 — top-arrow index 2 in the garage. PlayerPrefs 'SelectedCarColor' = 2.")]
    public GameObject blueCarPrefab;

    [Tooltip("Where the chosen car spawns. If null, spawner's own transform is used.")]
    public Transform spawnPoint;

    [Tooltip("Fallback when PlayerPrefs has no 'SelectedCarColor' key (e.g. running a map directly without going through the garage).")]
    public int defaultIndex = 0;

    [Tooltip("If true, disable any Light components on objects named '*Headlight*' after spawning. Use for daytime maps.")]
    public bool disableHeadlights = false;

    [Tooltip("Optional VFX (rain, snow, etc.) instantiated as a child of the spawned player car. Local-space particle systems work best so the effect follows the car.")]
    public GameObject vfxPrefab;

    void Awake()
    {
        int idx = PlayerPrefs.GetInt("SelectedCarColor", defaultIndex);
        GameObject prefab = idx switch
        {
            1 => pinkCarPrefab,
            2 => blueCarPrefab,
            _ => orangeCarPrefab,
        };
        if (prefab == null)
        {
            Debug.LogError($"[CarSpawner] Selected car prefab (index {idx}) is not assigned.");
            return;
        }

        Transform sp = spawnPoint != null ? spawnPoint : transform;
        GameObject car = Instantiate(prefab, sp.position, sp.rotation);
        car.name = prefab.name;

        if (disableHeadlights)
            foreach (var lt in car.GetComponentsInChildren<Light>(true))
                if (lt.gameObject.name.IndexOf("Headlight", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    lt.enabled = false;

        if (vfxPrefab != null)
        {
            // Preserve the prefab's authored local position/rotation so e.g. a rain box positioned
            // 10m above stays above the car. InstantiatePrefab-with-parent uses the prefab's local
            // transform by default (worldPositionStays = false), which is what we want.
            Instantiate(vfxPrefab, car.transform);
        }

        var aiSpawner = FindFirstObjectByType<AIRaceGridSpawner>();
        if (aiSpawner != null)
        {
            if (aiSpawner.playerTransform == null) aiSpawner.playerTransform = car.transform;
            if (aiSpawner.playerRigidbody == null) aiSpawner.playerRigidbody = car.GetComponent<Rigidbody>() ?? car.GetComponentInChildren<Rigidbody>();
        }
    }
}
