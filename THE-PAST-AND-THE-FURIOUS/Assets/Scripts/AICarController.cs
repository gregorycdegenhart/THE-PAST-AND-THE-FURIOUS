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
    public int lookAheadCount = 8;
    [Tooltip("Total angle across lookahead waypoints that counts as a mild corner (starts gentle braking).")]
    public float gentleCornerAngle = 12f;
    [Tooltip("Total angle across lookahead waypoints that counts as a hairpin (full braking).")]
    public float hairpinCornerAngle = 80f;
    [Tooltip("Speed multiplier for a gentle corner.")]
    [Range(0.3f, 1f)] public float cornerSpeedMultiplier = 0.85f;
    [Tooltip("Speed multiplier for a hairpin — AI cannot go faster than this through the tightest turns.")]
    [Range(0.1f, 0.9f)] public float hairpinSpeedMultiplier = 0.6f;

    [Header("Rubber Banding")]
    public Transform playerTransform;
    public float rubberBandSlowDistance = 30f;
    public float rubberBandFastDistance = 15f;
    public float rubberBandSlowMultiplier = 0.9f;
    public float rubberBandFastMultiplier = 1.5f;

    [Header("Imperfect Steering")]
    public float steerNoiseAmount = 0.08f;
    public float steerNoiseSpeed = 0.6f;

    [Header("Racing Line")]
    public float racingLineOffset = 3f;
    public float racingLineRandomRange = 4f;

    [Header("Car Separation")]
    public float separationDistance = 12f;
    public float separationStrength = 35f;

    [Tooltip("Multiplier on the lateral (sideways) component of the separation push. >1 spreads cars across the road instead of stacking on the racing line. ~2.5 keeps them in distinct lanes.")]
    public float lateralSeparationBias = 2.5f;

    // Spawner sets this for rubber-band distance computation; AI no longer applies any force
    // to the player (we want zero physical interaction — IgnoreCollision in spawner enforces that).
    [HideInInspector] public Rigidbody playerRigidbody;

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

    [Header("Dynamic Body Propulsion")]
    [Tooltip("Max horizontal acceleration (m/s^2) the propulsion force can apply. Higher = snappier response to target speed; lower = more sluggish/realistic.")]
    public float accelerationForceMax = 30f;

    [Tooltip("Per-frame multiplier on the lateral (sideways) component of the rigidbody's velocity. <1 = sideways drift bleeds off (1 = no lateral damping, 0 = instant lateral kill). 0.5 is a reasonable middle.")]
    [Range(0f, 1f)] public float lateralDampFactor = 0.6f;

    [Tooltip("If true, applies an upward force on slopes equal to the gravity component pulling the car down the slope, so the car doesn't lose speed climbing ramps. Uses a downward raycast to detect grounding.")]
    public bool slopeAssist = true;

    [Tooltip("Layers that count as ground for slope-assist + grounding checks. Should include Terrain and Default.")]
    public LayerMask groundMask = ~0;

    [Header("Obstacle Avoidance")]
    [Tooltip("Base distance ahead the AI casts for obstacles. Scaled by speed at runtime.")]
    public float obstacleAvoidanceDistance = 10f;

    [Tooltip("Sideways angle (degrees) of the left/right avoidance rays.")]
    public float obstacleAvoidanceSideAngle = 22f;

    [Tooltip("How strongly to bias steering away from obstacles (added to base steer input). 0 = off.")]
    public float obstacleAvoidanceStrength = 1.2f;

    [Tooltip("Speed multiplier applied when something is dead ahead. Prevents plowing at full speed into a barrier.")]
    [Range(0.1f, 1f)] public float obstacleSlowMultiplier = 0.55f;

    [Tooltip("Surfaces with a normal dot-up greater than this are treated as the road/ground and ignored by avoidance. 0.7 ≈ ramps up to ~45° still count as ground.")]
    [Range(0f, 1f)] public float groundNormalThreshold = 0.7f;

    [Header("Out-of-Bounds Respawn")]
    [Tooltip("If true, the AI auto-teleports back onto the racing line when it falls off the map (Y too far below the next waypoint, or horizontal distance too far from it).")]
    public bool autoRespawnIfFallenOff = true;

    [Tooltip("Meters below the current target waypoint after which the AI is considered to have fallen off and is respawned. Should be higher than the deepest legitimate dip on the track.")]
    public float fallenBelowThreshold = 40f;

    [Tooltip("Meters of horizontal distance from the current target waypoint after which the AI is considered off-course and respawned. Set high (or 0 to disable) — long straights between waypoints can legitimately put the AI hundreds of meters from the next waypoint.")]
    public float maxDistanceFromTrack = 0f;

    [Tooltip("Cooldown after a respawn before the AI can be respawned again — prevents ping-ponging if the respawn point is itself on a sketchy surface.")]
    public float respawnCooldown = 2f;

    [Tooltip("Height (meters) above the waypoint to spawn at, so the car drops onto the road instead of clipping into it.")]
    public float respawnHeightOffset = 1.5f;

    [Tooltip("Grace period (seconds) after the race starts before stuck/fall-off detection runs. Gives the AI time to accelerate from 0 and clear the start grid. If teleports are firing right after countdown, raise this.")]
    public float stuckGraceAfterRaceStart = 4f;

    [Tooltip("Seconds airborne (no ground beneath) before the AI gets teleported to the next waypoint. Catches cars that launch off ramps or edges.")]
    public float airborneTimeout = 0.5f;

    [Tooltip("Downward raycast distance for the airborne check. Should be larger than the tallest legitimate jump landing.")]
    public float airborneRaycastDistance = 4f;

    [Tooltip("Seconds of barely-moving (less than ~1 m/s) after grace before the AI is declared stuck and teleported to the next waypoint. Lower = more aggressive recovery. Raise if AI is teleporting when they're actually just slow.")]
    public float stuckTimeout = 1.5f;

    [Tooltip("If true, log a console message every time an AI teleports — useful for figuring out WHY they're teleporting (stuck vs fallen off).")]
    public bool logTeleports = true;

    private float raceStartTimestamp = -1f;
    private Rigidbody rb;
    private int currentWaypointIndex = 0;
    private int currentLap = 1;
    private bool raceFinished = false;

    // Stuck recovery: tracks how long the AI has been making basically no forward progress.
    // After stuckTimeout, advance to the next waypoint and teleport directly onto it so the
    // AI can't get pinned against a wall, an upside-down landing, or another AI for long.
    private Vector3 lastProgressPosition;
    private float stuckTimer = 0f;
    private float airborneTimer = 0f;
    private const float STUCK_MIN_DISTANCE_PER_SECOND = 1.0f;

    private bool isTurboActive = false;
    private float turboTimer = 0f;
    private float cooldownTimer = 0f;

    private float noiseOffsetX;
    private float noiseOffsetZ;
    private float currentLapLineOffset = 0f;
    private float currentSpeed = 0f;
    private float coastDistance = 0f;
    private bool waitingForFinalFinishCross = false;
    private float lastRespawnTime = -10f;
    private float externalSpeedMultiplier = 1f;

    private static AICarController[] allAICars;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Fully dynamic: gravity holds the car on the ground, physics handles ramps and falls,
        // jumps off ramps just work because gravity does. Car-vs-car collisions (player and
        // other AI) are filtered off in the spawner via Physics.IgnoreCollision.
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearDamping = 0f;
        rb.angularDamping = 5f;
        // Match the player's mass (CarController sets 120) so propulsion forces have the same
        // feel and so collision response with terrain is consistent.
        rb.mass = 120f;

        // Only freeze yaw — steering uses MoveRotation around Y. Pitch (X) and roll (Z) stay
        // free so the car body naturally tilts to follow ramps and banked turns. LateUpdate
        // below catches extreme tilts (terrain glitches) and re-levels the car.
        rb.constraints = RigidbodyConstraints.FreezeRotationY;

        noiseOffsetX = Random.Range(0f, 100f);
        noiseOffsetZ = Random.Range(0f, 100f);
        RandomizeLapLine();
    }

    /// <summary>
    /// Safety net: if terrain or a collision flips the car past 60° of pitch or roll, snap it
    /// back to flat (preserving heading). Mirrors the same guard in CarController.
    /// </summary>
    void LateUpdate()
    {
        Vector3 e = transform.eulerAngles;
        float xTilt = e.x > 180f ? e.x - 360f : e.x;
        float zTilt = e.z > 180f ? e.z - 360f : e.z;
        if (Mathf.Abs(xTilt) > 60f || Mathf.Abs(zTilt) > 60f)
        {
            transform.rotation = Quaternion.Euler(0f, e.y, 0f);
            if (rb != null) rb.angularVelocity = Vector3.zero;
        }
    }

    void Start()
    {
        // Static cache shared across every AI in the scene. Only the first AI's Start
        // populates it; the rest see the cached array and skip the scene scan.
        // Stale entries (AIs destroyed mid-race) are tolerated via the null check
        // inside ApplySeparationVelocity.
        if (allAICars == null || allAICars.Length == 0)
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

        // Out-of-bounds: if we've fallen off the map, snap back to the waypoint we were
        // heading toward so the AI doesn't fall forever or get stranded under the world.
        // Skipped during the same race-start grace period as stuck detection — at spawn the
        // AI might legitimately be far from waypoint 0 vertically (street level vs elevated
        // section) which would otherwise trigger an immediate teleport on race start.
        if (autoRespawnIfFallenOff
            && Time.time - raceStartTimestamp >= stuckGraceAfterRaceStart
            && Time.time - lastRespawnTime > respawnCooldown
            && IsFallenOffMap())
        {
            if (logTeleports) Debug.Log($"[AI {name}] Teleporting (FALLEN OFF) — y={rb.position.y:F1}, target wp Y={waypointPath.GetWaypoint(currentWaypointIndex)?.position.y:F1}");
            RespawnAtCurrentWaypoint();
            return;
        }

        // Airborne check: if no ground is beneath us for `airborneTimeout` seconds, advance to
        // the next waypoint and teleport there. Catches cars that launched off a ramp or edge.
        if (Time.time - raceStartTimestamp >= stuckGraceAfterRaceStart
            && Time.time - lastRespawnTime > respawnCooldown)
        {
            bool grounded = Physics.Raycast(rb.position + Vector3.up * 0.5f, Vector3.down,
                airborneRaycastDistance + 0.5f, groundMask, QueryTriggerInteraction.Ignore);
            if (!grounded)
            {
                airborneTimer += Time.fixedDeltaTime;
                if (airborneTimer >= airborneTimeout)
                {
                    if (logTeleports) Debug.Log($"[AI {name}] Teleporting (AIRBORNE) — no ground for {airborneTimer:F2}s");
                    airborneTimer = 0f;
                    AdvanceWaypoint();
                    RespawnAtCurrentWaypoint();
                    return;
                }
            }
            else
            {
                airborneTimer = 0f;
            }
        }

        Transform targetWaypoint = waypointPath.GetWaypoint(currentWaypointIndex);
        if (targetWaypoint == null) return;

        Vector3 targetPos = GetRacingLineTarget(targetWaypoint);

        Vector3 dirToTarget = targetPos - transform.position;
        dirToTarget.y = 0f;
        float dist = dirToTarget.magnitude;
        dirToTarget.Normalize();

        float angleToTarget = Vector3.SignedAngle(transform.forward, dirToTarget, Vector3.up);

        // --- Obstacle avoidance (raycasts ahead) ---
        ComputeObstacleAvoidance(out float avoidSteer, out float avoidSpeedMul);

        // --- Steering ---
        float noiseTime = Time.time * steerNoiseSpeed;
        float steerNoise = (Mathf.PerlinNoise(noiseTime + noiseOffsetX, noiseTime + noiseOffsetZ) - 0.5f) * 2f * steerNoiseAmount;
        float steerInput = Mathf.Clamp(angleToTarget / 45f + steerNoise + avoidSteer, -1f, 1f);

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

        float rubber = GetRubberBandMultiplier();
        targetSpeed *= rubber;
        targetSpeed *= externalSpeedMultiplier;
        targetSpeed *= avoidSpeedMul;

        // Don't fire a new turbo while steering through a HAIRPIN — gentle corners are fine.
        // (gentleCornerAngle is now low enough that almost every stretch of track counts as a
        // "corner" for braking purposes, which would lock the AI out of turbo entirely.)
        bool inHairpinForTurbo = cornerSeverity > Mathf.Max(40f, hairpinCornerAngle * 0.6f);

        // Don't fire turbo when already heavily rubber-banding — without this, an AI that fell
        // behind catches up at base * 2.0 (rubber band) * 2.0 (turbo) = 4x speed, which feels
        // like a supersonic last-second pass at the finish line.
        bool alreadyBoosted = rubber > 1.3f;

        if (isTurboActive)
            targetSpeed *= turboSpeedMultiplier;
        else if (cooldownTimer <= 0f && !inHairpinForTurbo && !alreadyBoosted)
        {
            isTurboActive = true;
            turboTimer = turboDuration;
        }

        // Hard cap so no combination of boosts (rubber band, turbo, drift kick, slow-mo inverse)
        // ever pushes the AI past 2.5x their base maxSpeed. Belt-and-suspenders against
        // multiplicative stacking I might add later.
        targetSpeed = Mathf.Min(targetSpeed, maxSpeed * 2.5f);

        // --- Smooth acceleration of the speed target ---
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, (maxSpeed / accelerationTime) * Time.fixedDeltaTime);

        // --- Propulsion (force-based, dynamic body) ---
        // Drive horizontal velocity toward (forward * currentSpeed) via clamped impulse force.
        // Y velocity is left to gravity / collision so ramps + jumps "just work".
        ApplyPropulsion(currentSpeed);

        // Lateral velocity damping: bleed off sideways drift so the car doesn't slide on turns.
        // Without this the dynamic body skids outward through every corner because we're not
        // using wheel colliders to provide grip.
        DampLateralVelocity();

        // Soft separation pushes us away from nearby AI without using physics collision (which
        // is filtered off via IgnoreCollision). Applied as an additive velocity tweak.
        ApplySeparationVelocity();

        // Slope assist: cancel the slope-tangent component of gravity when grounded so the AI
        // doesn't lose speed climbing ramps. Player CarController does the same.
        if (slopeAssist) ApplySlopeAssist();

        UpdateStuckRecovery(rb.position);

        // Advance either by reach distance OR by having driven past the waypoint along the path.
        bool reached = dist < waypointReachDistance;
        bool passedBy = advanceOnPassBy && HasPassedCurrentWaypoint(targetWaypoint);
        if (reached || passedBy)
            AdvanceWaypoint();
    }

    /// <summary>
    /// Applies a clamped force that drives the rigidbody's HORIZONTAL velocity toward
    /// (forward * targetSpeed). Vertical velocity is preserved so gravity owns falls and jumps.
    /// </summary>
    private void ApplyPropulsion(float targetSpeed)
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) return;
        forward.Normalize();

        Vector3 desiredHorizontal = forward * targetSpeed;
        Vector3 currentHorizontal = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 velError = desiredHorizontal - currentHorizontal;

        // Convert velocity error into a force this fixed step would resolve at unit mass; clamp
        // by accelerationForceMax * mass so we don't snap-change velocity unrealistically.
        Vector3 force = velError * rb.mass / Time.fixedDeltaTime;
        force = Vector3.ClampMagnitude(force, accelerationForceMax * rb.mass);
        rb.AddForce(force, ForceMode.Force);
    }

    /// <summary>
    /// Casts three rays — center, left at -sideAngle, right at +sideAngle — to detect static
    /// obstacles (barricades, walls, signs). Outputs a steer adjustment biasing away from
    /// hits, and a speed multiplier that slows the car if an obstacle is dead ahead.
    /// Skips other Rigidbodies (cars), triggers (checkpoints, OOB volumes), and the road
    /// surface itself (any hit whose normal mostly points up).
    /// </summary>
    private void ComputeObstacleAvoidance(out float steerAdjust, out float speedMul)
    {
        steerAdjust = 0f;
        speedMul = 1f;
        if (obstacleAvoidanceStrength <= 0f) return;

        float castDist = obstacleAvoidanceDistance + Mathf.Max(0f, currentSpeed) * 0.3f;
        Vector3 origin = rb.position + Vector3.up * 0.5f;
        Vector3 fwd = transform.forward;
        Vector3 leftDir = Quaternion.Euler(0f, -obstacleAvoidanceSideAngle, 0f) * fwd;
        Vector3 rightDir = Quaternion.Euler(0f, obstacleAvoidanceSideAngle, 0f) * fwd;

        bool centerHit = ObstacleHit(origin, fwd, castDist);
        bool leftHit = ObstacleHit(origin, leftDir, castDist);
        bool rightHit = ObstacleHit(origin, rightDir, castDist);

        if (centerHit)
        {
            speedMul = obstacleSlowMultiplier;
            if (leftHit && !rightHit) steerAdjust = obstacleAvoidanceStrength;       // turn right
            else if (rightHit && !leftHit) steerAdjust = -obstacleAvoidanceStrength; // turn left
            else steerAdjust = obstacleAvoidanceStrength * 0.7f;                     // both/neither: bias right
        }
        else if (leftHit)
        {
            steerAdjust = obstacleAvoidanceStrength * 0.5f;
        }
        else if (rightHit)
        {
            steerAdjust = -obstacleAvoidanceStrength * 0.5f;
        }
    }

    private bool ObstacleHit(Vector3 origin, Vector3 dir, float dist)
    {
        var hits = Physics.RaycastAll(origin, dir, dist, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h.collider == null) continue;
            if (h.collider.attachedRigidbody != null) continue;          // skip cars / dynamic bodies
            if (Vector3.Dot(h.normal, Vector3.up) > groundNormalThreshold) continue; // skip the road
            return true;
        }
        return false;
    }

    /// <summary>
    /// Reduces the lateral (car-local-X) component of velocity each frame, preserving forward
    /// and vertical. Replaces wheel-collider grip — without it the dynamic body skids in turns.
    /// </summary>
    private void DampLateralVelocity()
    {
        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
        localVel.x *= lateralDampFactor;
        rb.linearVelocity = transform.TransformDirection(localVel);
    }

    /// <summary>
    /// On slopes, gravity pulls the car down the slope tangent — that drag would slow ramp
    /// climbs noticeably. Cancel just that tangent component when grounded.
    /// </summary>
    private void ApplySlopeAssist()
    {
        if (!Physics.Raycast(rb.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit,
            2f, groundMask, QueryTriggerInteraction.Ignore)) return;
        // Skip anything attached to a Rigidbody (other cars) — only true terrain/walls assist.
        if (hit.collider.attachedRigidbody != null) return;
        if (Vector3.Dot(hit.normal, Vector3.up) > 0.99f) return; // flat ground — no assist needed

        Vector3 slopeTangentGravity = Vector3.ProjectOnPlane(Physics.gravity, hit.normal);
        rb.AddForce(-slopeTangentGravity, ForceMode.Acceleration);
    }

    /// <summary>
    /// Tracks forward progress. After STUCK_TIMEOUT seconds of basically no horizontal movement,
    /// advance to the next waypoint and teleport the AI directly there. Necessary because the
    /// dynamic body can get pinned against a wall, an upside-down landing, or another AI's
    /// pushed-aside debris — and unlike the kinematic flow, there's no obstacle-clamp to bypass.
    /// </summary>
    private void UpdateStuckRecovery(Vector3 currentPos)
    {
        // Race-start grace period: the AI accelerates from 0 m/s after countdown ends, and the
        // first ~2 seconds of that ramp look like "no progress" to the distance check. Without
        // this skip, every AI gets force-teleported the moment the race starts.
        // Also covers any cooldown after a recent respawn.
        if (Time.time - raceStartTimestamp < stuckGraceAfterRaceStart
            || Time.time - lastRespawnTime < respawnCooldown
            || lastProgressPosition == Vector3.zero)
        {
            lastProgressPosition = currentPos;
            stuckTimer = 0f;
            return;
        }

        Vector3 delta = currentPos - lastProgressPosition;
        delta.y = 0f;
        float distMoved = delta.magnitude;
        float requiredPerStep = STUCK_MIN_DISTANCE_PER_SECOND * Time.fixedDeltaTime;

        if (distMoved < requiredPerStep)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer >= stuckTimeout)
            {
                if (logTeleports) Debug.Log($"[AI {name}] Teleporting (STUCK) — wasn't moving > 1 m/s for {stuckTimeout:F1}s. Was at {currentPos:F1}, target wp at {waypointPath.GetWaypoint(currentWaypointIndex)?.position:F1}");
                stuckTimer = 0f;
                AdvanceWaypoint();
                RespawnAtCurrentWaypoint();
            }
        }
        else
        {
            stuckTimer = 0f;
        }
        lastProgressPosition = currentPos;
    }

    /// <summary>
    /// True if the AI has fallen off the world: too far below its current target waypoint (Y),
    /// or (if enabled) too far horizontally away from it. The horizontal check is OFF by default
    /// because long straights between waypoints can legitimately put the AI hundreds of meters
    /// from the next waypoint, and dynamic-body AI doesn't really fly off-track laterally
    /// anymore (terrain collision keeps them on the road).
    /// </summary>
    private bool IsFallenOffMap()
    {
        if (waypointPath == null || waypointPath.WaypointCount == 0) return false;
        Transform target = waypointPath.GetWaypoint(currentWaypointIndex);
        if (target == null) return false;

        // Y-fall check: AI is well below where the next checkpoint sits.
        if (rb.position.y < target.position.y - fallenBelowThreshold) return true;

        // Horizontal-distance check (opt-in only — set maxDistanceFromTrack > 0).
        if (maxDistanceFromTrack > 0f)
        {
            Vector3 horizontal = rb.position - target.position;
            horizontal.y = 0f;
            if (horizontal.magnitude > maxDistanceFromTrack) return true;
        }

        return false;
    }

    /// <summary>
    /// Teleport the AI to the waypoint it was heading toward, reset velocity, and orient it
    /// along the path. Used when the AI has fallen off the map. Brief cooldown afterward
    /// stops repeated triggers if the respawn point itself is on shaky ground.
    /// </summary>
    private void RespawnAtCurrentWaypoint()
    {
        Transform target = waypointPath.GetWaypoint(currentWaypointIndex);
        if (target == null) return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.position = target.position + Vector3.up * respawnHeightOffset;

        // Face along the path toward the next waypoint after this one (so we land oriented
        // forward, not sideways).
        Transform next = waypointPath.GetWaypoint(currentWaypointIndex + 1);
        if (next != null)
        {
            Vector3 dir = next.position - target.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                rb.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        currentSpeed = 0f;
        lastRespawnTime = Time.time;
    }

    /// <summary>
    /// Called every FixedUpdate while the race is finished. Decelerates the car from whatever
    /// speed it had when crossing the line and rolls it to a stop, so the AI actually appears
    /// to finish the race and coast out rather than stopping dead at the waypoint.
    /// </summary>
    private void CoastOut()
    {
        currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, finishDeceleration * Time.fixedDeltaTime);
        coastDistance += currentSpeed * Time.fixedDeltaTime;
        if (coastDistance >= maxCoastDistance) currentSpeed = 0f;

        ApplyPropulsion(currentSpeed);
        DampLateralVelocity();
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
    /// Soft separation: nudge our horizontal velocity AWAY from any nearby AI so the pack
    /// spreads across the road instead of stacking on the racing line. Replaces the position-
    /// based separation that worked when the AI was kinematic — we now write to rb.linearVelocity.
    /// Physics collision between AIs is filtered off (IgnoreCollision in spawner).
    /// </summary>
    private void ApplySeparationVelocity()
    {
        if (allAICars == null) return;

        // Brief grace after teleport: skip separation so a freshly respawned AI doesn't get
        // catapulted by a sibling sitting near the same waypoint. Without this, overlap × strength
        // produces hundreds of m/s of sideways velocity and the car flies off in a random direction.
        if (Time.time - lastRespawnTime < respawnCooldown) return;

        Vector3 forwardFlat = transform.forward;
        forwardFlat.y = 0f;
        forwardFlat.Normalize();
        Vector3 rightFlat = Vector3.Cross(Vector3.up, forwardFlat);

        Vector3 separationVel = Vector3.zero;

        foreach (AICarController other in allAICars)
        {
            if (other == null || other == this) continue;

            Vector3 toOther = other.rb.position - rb.position;
            toOther.y = 0f;
            float dist = toOther.magnitude;
            if (dist >= separationDistance || dist <= 0.001f) continue;

            Vector3 pushDir = -toOther.normalized;
            float lateral = Vector3.Dot(pushDir, rightFlat);
            float longitudinal = Vector3.Dot(pushDir, forwardFlat);
            Vector3 weightedDir = (rightFlat * lateral * lateralSeparationBias) + (forwardFlat * longitudinal);
            if (weightedDir.sqrMagnitude > 0.0001f) weightedDir.Normalize();
            else weightedDir = pushDir;

            float overlap = separationDistance - dist;
            separationVel += weightedDir * overlap * separationStrength;
        }

        if (separationVel.sqrMagnitude < 0.0001f) return;

        // Cap the per-frame separation push so two cars stacking on the same waypoint can't
        // launch each other across the map. 8 m/s is a strong nudge, well shy of slingshot.
        const float MAX_SEP_VEL = 8f;
        if (separationVel.sqrMagnitude > MAX_SEP_VEL * MAX_SEP_VEL)
            separationVel = separationVel.normalized * MAX_SEP_VEL;

        Vector3 v = rb.linearVelocity;
        v.x += separationVel.x;
        v.z += separationVel.z;
        rb.linearVelocity = v;
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
        {
            // Scale the boost with how far behind. Base multiplier at -2, then add 0.05 per
            // additional waypoint behind, capped at 2.0. Without scaling, an AI 12 waypoints
            // back has the same boost as one 2 waypoints back and just camps the back of the pack.
            int waypointsBehind = Mathf.Max(0, -waypointDiff - 2);
            float boost = rubberBandFastMultiplier + waypointsBehind * 0.05f;
            return Mathf.Min(boost, 2.0f);
        }

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
                if (RaceManager.Instance != null) RaceManager.Instance.RegisterAIFinish();
            }
        }
        else if (raceType == RaceManager.RaceType.Checkpoints)
        {
            if (currentWaypointIndex >= waypointPath.WaypointCount)
            {
                raceFinished = true;
                if (RaceManager.Instance != null) RaceManager.Instance.RegisterAIFinish();
            }
        }
    }

    public bool IsTurboActive() => isTurboActive;
    public float GetCooldownProgress() => cooldownTimer > 0f ? cooldownTimer / turboCooldown : 0f;

    public int CurrentLap => currentLap;
    public int CurrentWaypointIndex => currentWaypointIndex;
    public bool RaceFinished => raceFinished;

    // Mirrors CarController.SetSpeedMultiplier so power-ups (slow-mo, future buffs)
    // can affect AI the same way they affect the player.
    public void SetSpeedMultiplier(float multiplier) => externalSpeedMultiplier = multiplier;
}
