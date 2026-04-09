using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class AICarController : MonoBehaviour
{
    [Header("Path")]
    public AIWaypointPath waypointPath;
    public float waypointReachDistance = 8f;

    [Header("Movement")]
    public float moveSpeed = 15f;
    public float turnSpeed = 90f;

    [Header("Braking")]
    public float brakeAngleThreshold = 30f;
    public float cornerSpeedMultiplier = 0.5f;

    [Header("Race Settings")]
    public RaceManager.RaceType raceType = RaceManager.RaceType.Laps;
    public int totalLaps = 3;

    private Rigidbody rb;
    private int currentWaypointIndex = 0;
    private int currentLap = 1;
    private bool raceFinished = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true; // just move it directly, no physics fighting
    }

    void FixedUpdate()
    {
        if (raceFinished || waypointPath == null || waypointPath.WaypointCount == 0) return;

        Transform targetWaypoint = waypointPath.GetWaypoint(currentWaypointIndex);
        if (targetWaypoint == null) return;

        // Direction to waypoint (flat)
        Vector3 dirToWaypoint = targetWaypoint.position - transform.position;
        dirToWaypoint.y = 0f;
        float dist = dirToWaypoint.magnitude;
        dirToWaypoint.Normalize();

        // Angle between forward and waypoint
        float angleToTarget = Vector3.SignedAngle(transform.forward, dirToWaypoint, Vector3.up);

        // Rotate toward waypoint
        float turnAmount = Mathf.Clamp(angleToTarget / 45f, -1f, 1f) * turnSpeed * Time.fixedDeltaTime;
        Quaternion newRot = rb.rotation * Quaternion.Euler(0f, turnAmount, 0f);
        rb.MoveRotation(newRot);

        // Slow down on sharp turns
        float speed = moveSpeed;
        if (Mathf.Abs(angleToTarget) > brakeAngleThreshold)
            speed *= cornerSpeedMultiplier;

        // Move forward
        Vector3 newPos = rb.position + transform.forward * speed * Time.fixedDeltaTime;
        newPos.y = rb.position.y; // keep current Y so it doesn't sink into ground
        rb.MovePosition(newPos);

        // Advance waypoint when close enough
        if (dist < waypointReachDistance)
            AdvanceWaypoint();
    }

    private void AdvanceWaypoint()
    {
        currentWaypointIndex++;

        if (raceType == RaceManager.RaceType.Laps)
        {
            if (currentWaypointIndex >= waypointPath.WaypointCount)
            {
                currentWaypointIndex = 0;
                currentLap++;
                if (currentLap > totalLaps)
                {
                    raceFinished = true;
                    Debug.Log(gameObject.name + " finished the race!");
                }
            }
        }
        else if (raceType == RaceManager.RaceType.Checkpoints)
        {
            if (currentWaypointIndex >= waypointPath.WaypointCount)
            {
                raceFinished = true;
                Debug.Log(gameObject.name + " finished the race!");
            }
        }
    }
}
