using UnityEngine;

public class LapCheckpoint : MonoBehaviour
{
    public int checkpointIndex = 0;
    public bool completesLap = false; // only matters in Laps mode

    private float lastTriggerTime = -1f;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && RaceManager.Instance != null)
        {
            // Don't register checkpoints before the race starts
            if (!CountdownUI.RaceStarted) return;

            // Prevent double-firing from multiple colliders on the same car
            if (Time.time - lastTriggerTime < 0.5f) return;
            lastTriggerTime = Time.time;

            RaceManager.Instance.HitCheckpoint(checkpointIndex, completesLap);

            CarAudio carAudio = other.GetComponentInParent<CarAudio>();
            if (carAudio != null) carAudio.PlayCheckpointSound();
        }
    }
}