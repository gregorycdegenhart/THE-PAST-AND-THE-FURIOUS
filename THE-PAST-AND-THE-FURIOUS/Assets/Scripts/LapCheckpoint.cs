using UnityEngine;

public class LapCheckpoint : MonoBehaviour
{
    public int checkpointIndex = 0;
    public bool completesLap = false;

    private float lastTriggerTime = -1f;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && RaceManager.Instance != null)
        {
            if (!CountdownUI.RaceStarted) return;

            if (Time.time - lastTriggerTime < 0.5f) return;
            lastTriggerTime = Time.time;

            RaceManager.Instance.HitCheckpoint(checkpointIndex, completesLap);
            RespawnManager.Instance?.UpdateLastCheckpoint(transform, other.transform.root.rotation);
        }
    }
}