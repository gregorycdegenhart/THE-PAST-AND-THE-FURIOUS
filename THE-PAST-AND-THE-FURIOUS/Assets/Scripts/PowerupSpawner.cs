using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drops power-up prefabs along the AI waypoint path at race start. Lets Arash
/// add a power-up to a map by dragging the spawner onto an empty GameObject
/// instead of placing every prefab by hand in every scene.
/// </summary>
public class PowerupSpawner : MonoBehaviour
{
    [Header("Path")]
    [Tooltip("Waypoint path the power-ups are placed along. Usually the same path the AI uses.")]
    public AIWaypointPath waypointPath;

    [Header("Power-up Prefabs")]
    [Tooltip("Prefabs to spawn. One is chosen at random per spawn slot. Add SlowMoPowerup, TurboPowerup, etc.")]
    public GameObject[] powerupPrefabs;

    [Header("Placement")]
    [Tooltip("How many power-ups to drop on the track. Spread evenly across the waypoints.")]
    public int spawnCount = 6;

    [Tooltip("Lateral offset from the waypoint (meters). 0 = on the racing line. Positive = right of path.")]
    public float lateralOffset = 0f;

    [Tooltip("Random lateral jitter applied per spawn (meters). 0 = exactly on lateralOffset.")]
    public float lateralJitter = 2f;

    [Tooltip("Vertical offset above the waypoint (meters). Power-ups float just above the track.")]
    public float heightOffset = 1f;

    [Tooltip("Skip this many waypoints from the start so the first power-up isn't on the grid.")]
    public int waypointStartOffset = 3;

    [Header("Debug")]
    public bool logSpawns = false;

    void Start()
    {
        if (waypointPath == null || waypointPath.WaypointCount < 2)
        {
            Debug.LogWarning("[PowerupSpawner] No waypoint path assigned or path has <2 waypoints. Nothing spawned.");
            return;
        }
        if (powerupPrefabs == null || powerupPrefabs.Length == 0)
        {
            Debug.LogWarning("[PowerupSpawner] powerupPrefabs is empty. Nothing spawned.");
            return;
        }
        if (spawnCount <= 0) return;

        SpawnAll();
    }

    void SpawnAll()
    {
        int wpCount = waypointPath.WaypointCount;
        int usable = Mathf.Max(1, wpCount - waypointStartOffset);
        int step = Mathf.Max(1, usable / spawnCount);

        var holder = new GameObject("_Powerups");
        holder.transform.SetParent(transform, false);

        int placed = 0;
        for (int i = 0; i < spawnCount; i++)
        {
            int wpIndex = (waypointStartOffset + i * step) % wpCount;
            Transform wp = waypointPath.GetWaypoint(wpIndex);
            if (wp == null) continue;

            // Direction along the path: use the next waypoint to compute "right" so the lateral
            // offset lays the power-up out beside the racing line, not floating in random space.
            Transform next = waypointPath.GetWaypoint((wpIndex + 1) % wpCount);
            Vector3 forward = next != null ? (next.position - wp.position) : wp.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            float jitter = Random.Range(-lateralJitter, lateralJitter);
            Vector3 pos = wp.position + right * (lateralOffset + jitter) + Vector3.up * heightOffset;

            GameObject prefab = powerupPrefabs[Random.Range(0, powerupPrefabs.Length)];
            if (prefab == null) continue;

            GameObject pu = Instantiate(prefab, pos, Quaternion.LookRotation(forward, Vector3.up), holder.transform);
            pu.name = $"{prefab.name}_{placed}";
            placed++;

            if (logSpawns)
                Debug.Log($"[PowerupSpawner] Placed {pu.name} at waypoint {wpIndex} pos={pos}");
        }
    }
}
