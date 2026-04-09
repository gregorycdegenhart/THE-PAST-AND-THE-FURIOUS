using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class AICarController : MonoBehaviour
{
    [Header("Path")]
    public AIWaypointPath waypointPath;
    public float waypointReachDistance = 8f;

    [Header("Movement")]
    public float maxSpeed = 25f;
    [Tooltip("How quickly the car ramps up to max speed")]
    public float accelerationTime = 2f;
    public float turnSpeed = 90f;

    [Header("Braking")]
    [Tooltip("How many waypoints ahead to scan for corners")]
    public int lookAheadCount = 5;
    [Tooltip("Total angle across lookahead waypoints that triggers braking")]
    public float brakeAngleThreshold = 25f;
    [Tooltip("Speed multiplier when a corner is detected ahead")]
    public float cornerSpeedMultiplier = 0.5f;

    [Header("Rubber Banding")]
    public Transform playerTransform;
    public float rubberBandSlowDistance = 30f;
    public float rubberBandFastDistance = 30f;
    public float rubberBandSlowMultiplier = 0.75f;
    public float rubberBandFastMultiplier = 1.3f;

    [Header("Imperfect Steering")]
    public float steerNoiseAmount = 0.08f;
    public float steerNoiseSpeed = 0.6f;

    [Header("Racing Line")]
    public float racingLineOffset = 3f;
    public float racingLineRandomRange = 4f;

    [Header("Car Separation")]
    public float separationDistance = 2.5f;
    public float separationStrength = 8f;

    [Header("Player Pushback")]
    public Rigidbody playerRigidbody;
    public float pushDistance = 3f;
    public float pushForce = 12f;

    [Header("Turbo")]
    public float turboSpeedMultiplier = 2f;
    public float turboDuration = 2f;
    public float turboCooldown = 5f;
    public float turboMaxAngle = 20f;

    [Header("Race Settings")]
    public RaceManager.RaceType raceType = RaceManager.RaceType.Laps;
    public int totalLaps = 3;

    private Rigidbody rb;
    private int currentWaypointIndex = 0;
    private int currentLap = 1;
    private bool raceFinished = false;

    private bool isTurboActive = false;
    private float turboTimer = 0f;
    private float cooldownTimer = 0f;

    private float noiseOffsetX;
    private float noiseOffsetZ;
    private float currentLapLineOffset = 0f;
    private float currentSpeed = 0f;

    private static AICarController[] allAICars;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;

        noiseOffsetX = Random.Range(0f, 100f);
        noiseOffsetZ = Random.Range(0f, 100f);
        RandomizeLapLine();
    }

    void Start()
    {
        allAICars = FindObjectsByType<AICarController>(FindObjectsSortMode.None);
    }

    void Update()
    {
        if (isTurboActive)
        {
            turboTimer -= Time.deltaTime;
            if (turboTimer <= 0f)
            {
                isTurboActive = false;
                cooldownTimer = turboCooldown;
            }
        }
        else if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
        }
    }

    void FixedUpdate()
    {
        if (raceFinished || waypointPath == null || waypointPath.WaypointCount == 0) return;

        Transform targetWaypoint = waypointPath.GetWaypoint(currentWaypointIndex);
        if (targetWaypoint == null) return;

        Vector3 targetPos = GetRacingLineTarget(targetWaypoint);

        Vector3 dirToTarget = targetPos - transform.position;
        dirToTarget.y = 0f;
        float dist = dirToTarget.magnitude;
        dirToTarget.Normalize();

        float angleToTarget = Vector3.SignedAngle(transform.forward, dirToTarget, Vector3.up);

        // --- Steering ---
        float noiseTime = Time.time * steerNoiseSpeed;
        float steerNoise = (Mathf.PerlinNoise(noiseTime + noiseOffsetX, noiseTime + noiseOffsetZ) - 0.5f) * 2f * steerNoiseAmount;
        float steerInput = Mathf.Clamp(angleToTarget / 45f + steerNoise, -1f, 1f);

        float turnAmount = steerInput * turnSpeed * Time.fixedDeltaTime;
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turnAmount, 0f));

        // --- Target speed ---
        float targetSpeed = maxSpeed;

        // look ahead for corners
        if (IsCornerAhead())
            targetSpeed *= cornerSpeedMultiplier;

        targetSpeed *= GetRubberBandMultiplier();

        if (isTurboActive)
            targetSpeed *= turboSpeedMultiplier;
        else if (cooldownTimer <= 0f && !IsCornerAhead())
        {
            // only turbo on straights
            isTurboActive = true;
            turboTimer = turboDuration;
        }

        // --- Smooth acceleration ---
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, (maxSpeed / accelerationTime) * Time.fixedDeltaTime);

        // --- Move ---
        Vector3 newPos = rb.position + transform.forward * currentSpeed * Time.fixedDeltaTime;
        newPos.y = rb.position.y;

        newPos = ApplySeparation(newPos);

        rb.MovePosition(newPos);

        PushPlayerIfClose();

        if (dist < waypointReachDistance)
            AdvanceWaypoint();
    }

    /// <summary>
    /// Scans the next lookAheadCount waypoints and accumulates the total
    /// direction change. If it exceeds brakeAngleThreshold, a corner is ahead.
    /// </summary>
    private bool IsCornerAhead()
    {
        if (waypointPath == null || waypointPath.WaypointCount == 0) return false;

        float totalAngle = 0f;
        int count = waypointPath.WaypointCount;

        for (int i = 0; i < lookAheadCount; i++)
        {
            int idxA = (currentWaypointIndex + i) % count;
            int idxB = (currentWaypointIndex + i + 1) % count;
            int idxC = (currentWaypointIndex + i + 2) % count;

            Transform wpA = waypointPath.GetWaypoint(idxA);
            Transform wpB = waypointPath.GetWaypoint(idxB);
            Transform wpC = waypointPath.GetWaypoint(idxC);

            if (wpA == null || wpB == null || wpC == null) continue;

            Vector3 dirAB = (wpB.position - wpA.position);
            dirAB.y = 0f;
            dirAB.Normalize();

            Vector3 dirBC = (wpC.position - wpB.position);
            dirBC.y = 0f;
            dirBC.Normalize();

            totalAngle += Mathf.Abs(Vector3.SignedAngle(dirAB, dirBC, Vector3.up));
        }

        return totalAngle > brakeAngleThreshold;
    }

    private Vector3 ApplySeparation(Vector3 proposedPos)
    {
        if (allAICars == null) return proposedPos;

        foreach (AICarController other in allAICars)
        {
            if (other == null || other == this) continue;

            Vector3 toOther = other.rb.position - proposedPos;
            toOther.y = 0f;
            float dist = toOther.magnitude;

            if (dist < separationDistance && dist > 0.001f)
            {
                Vector3 pushDir = toOther.normalized;
                float overlap = separationDistance - dist;
                proposedPos -= pushDir * overlap * separationStrength * Time.fixedDeltaTime;
                proposedPos.y = rb.position.y;
            }
        }

        return proposedPos;
    }

    private void PushPlayerIfClose()
    {
        if (playerRigidbody == null) return;

        Vector3 toPlayer = playerRigidbody.position - rb.position;
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;

        if (dist < pushDistance && dist > 0.01f)
        {
            Vector3 pushDir = toPlayer.normalized;
            pushDir.y = 0f;

            Vector3 currentPlayerVel = playerRigidbody.linearVelocity;
            playerRigidbody.AddForce(pushDir * pushForce, ForceMode.Impulse);

            Vector3 newVel = playerRigidbody.linearVelocity;
            newVel.y = currentPlayerVel.y;
            playerRigidbody.linearVelocity = newVel;
        }
    }

    private Vector3 GetRacingLineTarget(Transform currentWaypoint)
    {
        int nextIndex = (currentWaypointIndex + 1) % waypointPath.WaypointCount;
        Transform nextWaypoint = waypointPath.GetWaypoint(nextIndex);

        if (nextWaypoint == null) return currentWaypoint.position;

        Vector3 pathDir = (nextWaypoint.position - currentWaypoint.position);
        pathDir.y = 0f;
        pathDir.Normalize();

        float turnDir = Vector3.SignedAngle(
            (currentWaypoint.position - transform.position).normalized,
            pathDir, Vector3.up);

        Vector3 insideDir = turnDir > 0f
            ? -Vector3.Cross(pathDir, Vector3.up)
            : Vector3.Cross(pathDir, Vector3.up);

        float totalOffset = racingLineOffset + currentLapLineOffset;
        return currentWaypoint.position + insideDir * totalOffset;
    }

    private float GetRubberBandMultiplier()
    {
        if (playerTransform == null) return 1f;

        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        int playerWaypoint = GetNearestWaypointIndex(playerTransform.position);
        int waypointDiff = currentWaypointIndex - playerWaypoint;

        if (waypointDiff > 2 || (waypointDiff >= 0 && distToPlayer > rubberBandSlowDistance))
            return rubberBandSlowMultiplier;

        if (waypointDiff < -2 || (waypointDiff <= 0 && distToPlayer > rubberBandFastDistance))
            return rubberBandFastMultiplier;

        return 1f;
    }

    private int GetNearestWaypointIndex(Vector3 pos)
    {
        int nearest = 0;
        float nearestDist = float.MaxValue;
        for (int i = 0; i < waypointPath.WaypointCount; i++)
        {
            Transform wp = waypointPath.GetWaypoint(i);
            if (wp == null) continue;
            float d = Vector3.Distance(pos, wp.position);
            if (d < nearestDist) { nearestDist = d; nearest = i; }
        }
        return nearest;
    }

    private void RandomizeLapLine()
    {
        currentLapLineOffset = Random.Range(-racingLineRandomRange, racingLineRandomRange);
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
                RandomizeLapLine();
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

    public bool IsTurboActive() => isTurboActive;
    public float GetCooldownProgress() => cooldownTimer > 0f ? cooldownTimer / turboCooldown : 0f;
}
