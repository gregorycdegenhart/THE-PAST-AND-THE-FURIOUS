using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class AICarController : MonoBehaviour
{
    [Header("Path")]
    public AIWaypointPath waypointPath;
    [Tooltip("Radius around a waypoint that counts as 'reached'. Increase if AI miss waypoints and try to backtrack.")]
    public float waypointReachDistance = 15f;
    [Tooltip("If true, also advance to the next waypoint when the AI has driven past it along the path direction, even if it's outside the reach radius. Prevents backtracking after overshoots.")]
    public bool advanceOnPassBy = true;

    [Header("Movement")]
    public float maxSpeed = 25f;
    [Tooltip("How quickly the car ramps up to max speed")]
    public float accelerationTime = 2f;
    public float turnSpeed = 140f;

    [Header("Braking")]
    [Tooltip("How many waypoints ahead to scan for corners. Scales with speed automatically.")]
    public int lookAheadCount = 6;
    [Tooltip("Total angle across lookahead waypoints that counts as a mild corner (starts gentle braking).")]
    public float gentleCornerAngle = 20f;
    [Tooltip("Total angle across lookahead waypoints that counts as a hairpin (full braking).")]
    public float hairpinCornerAngle = 90f;
    [Tooltip("Speed multiplier for a gentle corner.")]
    [Range(0.3f, 1f)] public float cornerSpeedMultiplier = 0.8f;
    [Tooltip("Speed multiplier for a hairpin — AI cannot go faster than this through the tightest turns.")]
    [Range(0.1f, 0.9f)] public float hairpinSpeedMultiplier = 0.55f;

    [Header("Rubber Banding")]
    public Transform playerTransform;
    public float rubberBandSlowDistance = 30f;
    public float rubberBandFastDistance = 15f;
    public float rubberBandSlowMultiplier = 0.9f;
    public float rubberBandFastMultiplier = 1.8f;

    [Header("Imperfect Steering")]
    public float steerNoiseAmount = 0.08f;
    public float steerNoiseSpeed = 0.6f;

    [Header("Racing Line")]
    public float racingLineOffset = 3f;
    public float racingLineRandomRange = 4f;

    [Header("Car Separation")]
    public float separationDistance = 4f;
    public float separationStrength = 25f;

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

    [Header("Finish Behavior")]
    [Tooltip("Deceleration applied after the race is finished (units/sec^2). Lower = longer coast-out past the finish line.")]
    public float finishDeceleration = 40f;
    [Tooltip("Max distance (meters) the AI will coast forward past the finish line before forcibly stopping.")]
    public float maxCoastDistance = 80f;

    [Header("Reaction")]
    [Tooltip("Seconds this AI waits after the countdown ends before starting to move. Gives the pack a natural staggered launch instead of all 7 taking off in lockstep.")]
    public float startReactionDelay = 0f;

    [Header("Ground Snap")]
    [Tooltip("If true, the AI's Y is snapped to the ground below it every FixedUpdate via a downward raycast. Required when the car is kinematic (which it is) and the track has elevation changes.")]
    public bool snapToGround = true;

    [Tooltip("Distance above the ground surface where the rigidbody origin should sit. 0.7 matches the Player_BMW's resting height.")]
    public float groundYOffset = 0.7f;

    [Tooltip("Layers that count as ground. Include Terrain and Default; exclude the car's own layer if it self-hits.")]
    public LayerMask groundMask = ~0;

    [Header("Solid Obstacle Avoidance")]
    [Tooltip("If true, the AI sweep-tests before moving and won't pass through terrain, walls, the player, or other static obstacles. AI-vs-AI is handled by the soft separation below.")]
    public bool blockAgainstSolids = true;

    [Tooltip("Layers counted as blocking obstacles. Should include Terrain, Default, and anything the car shouldn't phase through.")]
    public LayerMask obstacleMask = ~0;

    [Tooltip("How far the AI is pushed back from a solid on contact. Small values prevent jitter.")]
    public float obstacleSkin = 0.05f;

    private readonly RaycastHit[] groundHits = new RaycastHit[8];
    private readonly RaycastHit[] obstacleHits = new RaycastHit[16];
    private readonly Collider[] overlapBuffer = new Collider[16];

    private float raceStartTimestamp = -1f;
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
    private float coastDistance = 0f;
    private bool waitingForFinalFinishCross = false;

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
        if (waypointPath == null || waypointPath.WaypointCount == 0) return;

        // Wait for the 3-2-1-GO countdown to finish before taking off.
        if (!CountdownUI.RaceStarted) return;

        // Per-AI reaction delay: stagger the launch so the pack doesn't move as a rigid block.
        if (raceStartTimestamp < 0f) raceStartTimestamp = Time.time;
        if (Time.time - raceStartTimestamp < startReactionDelay) return;

        // Race-finished state: coast forward and decelerate smoothly instead of halting.
        // This lets the AI actually cross the finish line, then rolls them to a stop.
        if (raceFinished)
        {
            CoastOut();
            return;
        }

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

        // Severity-based braking: the sharper the upcoming corner, the slower we go.
        // gentleCornerAngle → cornerSpeedMultiplier (light brake)
        // hairpinCornerAngle → hairpinSpeedMultiplier (heavy brake)
        // Anywhere between: linear interp. Below gentle: full speed.
        float cornerSeverity = GetCornerSeverity();
        float cornerBrake = 1f;
        if (cornerSeverity > gentleCornerAngle)
        {
            float t = Mathf.InverseLerp(gentleCornerAngle, hairpinCornerAngle, cornerSeverity);
            cornerBrake = Mathf.Lerp(cornerSpeedMultiplier, hairpinSpeedMultiplier, t);
        }
        targetSpeed *= cornerBrake;

        targetSpeed *= GetRubberBandMultiplier();

        // Don't fire a new turbo while steering through a real corner — the extra speed
        // is exactly what sends the AI off the track. Finish the turn first.
        bool inCorner = cornerSeverity > gentleCornerAngle;

        if (isTurboActive)
            targetSpeed *= turboSpeedMultiplier;
        else if (cooldownTimer <= 0f && !inCorner)
        {
            isTurboActive = true;
            turboTimer = turboDuration;
        }

        // --- Smooth acceleration ---
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, (maxSpeed / accelerationTime) * Time.fixedDeltaTime);

        // --- Move ---
        Vector3 newPos = rb.position + transform.forward * currentSpeed * Time.fixedDeltaTime;

        // Ground snap: without this a kinematic AI just keeps its spawn Y forever, which
        // floats on any elevation change in the track.
        if (snapToGround)
            SnapYToGround(ref newPos);
        else
            newPos.y = rb.position.y;

        newPos = ApplySeparation(newPos);

        // Obstacle avoidance: AI is kinematic so Unity won't resolve collisions with terrain,
        // walls, or other AI. Sweep-test the intended move and clamp distance if anything
        // blocks the path. Hard-separates AI-vs-AI as a safety net over the soft separation.
        if (blockAgainstSolids)
            ClampAgainstObstacles(ref newPos);

        rb.MovePosition(newPos);

        PushPlayerIfClose();

        // Advance either by reach distance OR by having driven past the waypoint along the path.
        bool reached = dist < waypointReachDistance;
        bool passedBy = advanceOnPassBy && HasPassedCurrentWaypoint(targetWaypoint);
        if (reached || passedBy)
            AdvanceWaypoint();
    }

    /// <summary>
    /// Called every FixedUpdate while the race is finished. Decelerates the car from whatever
    /// speed it had when crossing the line and rolls it to a stop, so the AI actually appears
    /// to finish the race and coast out rather than stopping dead at the waypoint.
    /// </summary>
    private void CoastOut()
    {
        if (currentSpeed <= 0.01f) return; // fully stopped — leave it parked

        currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, finishDeceleration * Time.fixedDeltaTime);

        float stepDistance = currentSpeed * Time.fixedDeltaTime;
        coastDistance += stepDistance;

        // Safety: stop forcibly if we've coasted further than the cap (e.g., coasting down a hill).
        if (coastDistance >= maxCoastDistance)
        {
            currentSpeed = 0f;
            return;
        }

        Vector3 newPos = rb.position + transform.forward * currentSpeed * Time.fixedDeltaTime;

        if (snapToGround)
            SnapYToGround(ref newPos);
        else
            newPos.y = rb.position.y;

        if (blockAgainstSolids)
            ClampAgainstObstacles(ref newPos);

        rb.MovePosition(newPos);
    }

    /// <summary>
    /// Returns true if the car is already past the current waypoint along the direction toward
    /// the NEXT waypoint. This prevents the AI from circling back after overshooting a turn.
    /// </summary>
    private bool HasPassedCurrentWaypoint(Transform currentWaypoint)
    {
        if (waypointPath == null || waypointPath.WaypointCount < 2) return false;

        int nextIndex = (currentWaypointIndex + 1) % waypointPath.WaypointCount;
        Transform nextWaypoint = waypointPath.GetWaypoint(nextIndex);
        if (nextWaypoint == null || currentWaypoint == null) return false;

        Vector3 segmentDir = nextWaypoint.position - currentWaypoint.position;
        segmentDir.y = 0f;
        if (segmentDir.sqrMagnitude < 0.001f) return false;
        segmentDir.Normalize();

        Vector3 fromWp = transform.position - currentWaypoint.position;
        fromWp.y = 0f;

        // Positive projection = AI is on the "next" side of the waypoint along the path.
        return Vector3.Dot(fromWp, segmentDir) > 0f;
    }

    /// <summary>
    /// Returns the total corner angle (in degrees) across the next lookahead waypoints.
    /// Higher = sharper turn. Scales the lookahead range with current speed so fast AI
    /// see further and brake earlier.
    /// </summary>
    private float GetCornerSeverity()
    {
        if (waypointPath == null || waypointPath.WaypointCount == 0) return 0f;

        // Scale lookahead with speed: at low speed, 1x the configured count;
        // at max speed, 2x. Prevents the "I was going too fast to see the turn" problem.
        int speedAdjustedLookahead = Mathf.CeilToInt(
            lookAheadCount * Mathf.Lerp(1f, 2f, maxSpeed > 0f ? currentSpeed / maxSpeed : 0f));
        speedAdjustedLookahead = Mathf.Max(3, speedAdjustedLookahead);

        float totalAngle = 0f;
        int count = waypointPath.WaypointCount;

        for (int i = 0; i < speedAdjustedLookahead; i++)
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

        return totalAngle;
    }

    /// <summary>
    /// Box-cast the car's chassis in the direction of the intended move. If anything solid
    /// (terrain, another AI, the player, props) is in the way, shorten the move so the car
    /// stops against it instead of phasing through.
    /// </summary>
    private void ClampAgainstObstacles(ref Vector3 newPos)
    {
        Vector3 delta = newPos - rb.position;
        delta.y = 0f; // only horizontal movement — ground snap owns Y
        float distance = delta.magnitude;
        if (distance < 0.0001f) return;

        Vector3 direction = delta / distance;

        // Size of the Player_BMW chassis (width, height, length) from its 3 BoxColliders
        // combined: x=2.16, y=1.5, z=4.8. Half-extents below.
        Vector3 halfExtents = new Vector3(1.08f, 0.75f, 2.4f);
        Vector3 boxCenter = rb.position + rb.rotation * new Vector3(-0.09f, 0.28f, 0f);

        int hitCount = Physics.BoxCastNonAlloc(
            boxCenter, halfExtents, direction, obstacleHits, rb.rotation,
            distance + obstacleSkin, obstacleMask, QueryTriggerInteraction.Ignore);

        float allowed = distance;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit h = obstacleHits[i];
            if (h.collider == null) continue;

            // Skip our own colliders.
            if (h.collider.transform == transform || h.collider.transform.IsChildOf(transform)) continue;

            // BoxCast reports 0 distance when a collider is already overlapping at start; ignore those.
            if (h.distance <= 0f) continue;

            float d = Mathf.Max(0f, h.distance - obstacleSkin);
            if (d < allowed) allowed = d;
        }

        if (allowed < distance)
        {
            Vector3 clamped = rb.position + direction * allowed;
            // Preserve whatever ground-snap already did to Y.
            clamped.y = newPos.y;
            newPos = clamped;
        }
    }

    /// <summary>
    /// Snap the AI to the ground at its (X, Z) position, keeping a constant offset above the
    /// ground equal to this AI's groundYOffset. The offset is set once at spawn time by the
    /// spawner (computed from the car's visual bounds so the wheels sit exactly on the ground).
    /// This is independent of whatever Y the player happens to be at.
    /// </summary>
    private void SnapYToGround(ref Vector3 pos)
    {
        if (!TryFindGroundY(pos.x, pos.y + 5f, pos.z, this.transform, out float aiTerrainY))
        {
            pos.y = rb.position.y;
            return;
        }

        pos.y = aiTerrainY + groundYOffset;
    }

    /// <summary>
    /// Downward raycast that returns the Y of the nearest hit that is NOT attached to `selfRoot`
    /// or its descendants. Used to find the actual ground below a car while ignoring the car
    /// itself.
    /// </summary>
    private bool TryFindGroundY(float x, float y, float z, Transform selfRoot, out float groundY)
    {
        Vector3 rayStart = new Vector3(x, y, z);
        int hitCount = Physics.RaycastNonAlloc(
            rayStart, Vector3.down, groundHits, 60f, groundMask, QueryTriggerInteraction.Ignore);

        float nearestDist = float.MaxValue;
        float nearestY = 0f;
        bool found = false;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit h = groundHits[i];
            if (h.collider == null) continue;
            if (selfRoot != null)
            {
                if (h.collider.transform == selfRoot) continue;
                if (h.collider.transform.IsChildOf(selfRoot)) continue;
            }
            if (h.distance < nearestDist)
            {
                nearestDist = h.distance;
                nearestY = h.point.y;
                found = true;
            }
        }
        groundY = nearestY;
        return found;
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
                    // Lap 3 is DONE (we just wrapped past WP_34). But don't stop yet — the
                    // car is still heading back toward WP_00 (the finish line itself). Wait
                    // until it actually reaches WP_00 before triggering the coast-out.
                    waitingForFinalFinishCross = true;
                }
            }
            else if (waitingForFinalFinishCross && currentWaypointIndex == 1)
            {
                // We just reached WP_00 (the advance from WP_00 made the index become 1).
                // That's the real finish-line crossing — NOW start coasting to a stop.
                raceFinished = true;
                waitingForFinalFinishCross = false;
                Debug.Log(gameObject.name + " finished the race (crossed WP_00)!");
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
