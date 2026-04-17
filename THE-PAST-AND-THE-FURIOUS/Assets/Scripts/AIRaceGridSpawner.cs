using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Spawns a grid of AI opponents at race start, reusing the player's car prefab.
/// Strips out player-only components (camera, input, audio listener, player controller)
/// and wires up AICarController on each spawned copy.
/// </summary>
public class AIRaceGridSpawner : MonoBehaviour
{
    [Header("Spawn")]
    [Tooltip("Prefab to clone for each AI (typically Player_BMW). Player-only components are stripped automatically.")]
    public GameObject carSourcePrefab;

    [Tooltip("How many AI opponents to spawn.")]
    public int numberOfAI = 7;

    [Header("References")]
    [Tooltip("Transform of the player's car. Used as the anchor for the starting grid.")]
    public Transform playerTransform;

    [Tooltip("Rigidbody of the player's car. Used by AI pushback logic.")]
    public Rigidbody playerRigidbody;

    [Tooltip("Waypoint path the AI will follow.")]
    public AIWaypointPath waypointPath;

    [Header("Grid Formation")]
    [Tooltip("Cars per row in the grid. The player IS one of these slots — 7 AI + 1 player in a 4-wide grid = 2 rows.")]
    public int carsPerRow = 4;

    [Tooltip("Which column index the player occupies in row 0 (0-based). AI fill all other slots in the grid.")]
    public int playerGridColumn = 1;

    [Tooltip("Lateral distance between cars on the same row.")]
    public float lateralSpacing = 4f;

    [Tooltip("Distance between rows (front to back).")]
    public float rowSpacing = 6f;

    [Tooltip("Extra gap added to the distance between the player's row and row 1. Must be >= 0.")]
    public float gridStartGap = 2f;

    [Header("AI Tuning (forwarded to each spawned AI)")]
    [Tooltip("If true and the player has a CarController, AI max speed is derived from CarController.maxForwardSpeed so the AI can keep up with the player automatically.")]
    public bool matchPlayerMaxSpeed = true;

    [Tooltip("Multiplier applied when matching the player's max speed. Player gets turbo (2x) and drift boost (up to 1.5x) stacked, so the AI needs a headroom factor here just to keep up. 1.5-2.0 is usually fair.")]
    [Range(0.5f, 3f)] public float playerSpeedMatchFactor = 1.8f;

    [Tooltip("Fallback max speed if there's no player CarController to read from.")]
    public float aiMaxSpeed = 25f;

    public float aiSeparationDistance = 4f;
    public float aiSeparationStrength = 25f;

    [Header("AI Personality Variance (per-racer randomization)")]
    [Tooltip("Each AI's max speed is randomized within ±(this fraction). 0.08 = ±8%.")]
    [Range(0f, 0.3f)] public float speedVariance = 0.08f;

    [Tooltip("Each AI's corner braking aggression varies by ±(this fraction of the base value).")]
    [Range(0f, 0.5f)] public float cornerAggressionVariance = 0.25f;

    [Tooltip("Steering noise amount range. Some AI drive cleaner, some wobble more.")]
    public Vector2 steerNoiseRange = new Vector2(0.04f, 0.12f);

    [Tooltip("Turbo cooldown multiplier range. Smaller = turbos more often.")]
    public Vector2 turboCooldownMultiplierRange = new Vector2(0.4f, 0.9f);

    [Tooltip("Acceleration time range (seconds to reach max). Lower = snappier.")]
    public Vector2 accelerationTimeRange = new Vector2(1.2f, 2.4f);

    [Tooltip("Per-AI reaction delay range after the countdown ends. Small values (0-0.4s) look natural.")]
    public Vector2 reactionDelayRange = new Vector2(0f, 0.35f);

    [Header("Race Settings (forwarded to each spawned AI)")]
    public RaceManager.RaceType raceType = RaceManager.RaceType.Laps;
    public int totalLaps = 3;

    [Header("Debug")]
    public bool logSpawns = true;

    void Awake()
    {
        if (carSourcePrefab == null)
        {
            Debug.LogError("[AIRaceGridSpawner] carSourcePrefab not assigned.");
            return;
        }
        if (waypointPath == null)
        {
            Debug.LogError("[AIRaceGridSpawner] waypointPath not assigned.");
            return;
        }
        if (playerTransform == null)
        {
            Debug.LogWarning("[AIRaceGridSpawner] playerTransform not assigned; AI will still drive but won't rubber-band to the player.");
        }

        SpawnGrid();
    }

    void SpawnGrid()
    {
        // Anchor the grid at the player's position and facing, or at this spawner if no player.
        Vector3 anchorPos = playerTransform != null ? playerTransform.position : transform.position;
        Quaternion anchorRot = playerTransform != null ? playerTransform.rotation : transform.rotation;

        Vector3 fwd = anchorRot * Vector3.forward;
        Vector3 right = anchorRot * Vector3.right;

        int cols = Mathf.Max(1, carsPerRow);
        int playerCol = Mathf.Clamp(playerGridColumn, 0, cols - 1);
        float gap = Mathf.Max(0f, gridStartGap);

        // Instantiate under an inactive holder so Awake/OnEnable is deferred until we've
        // stripped the player-only components. Otherwise CarAudio would spawn 2D AudioSources,
        // CarController would reconfigure the Rigidbody, an AudioListener would try to register, etc.
        GameObject holder = new GameObject("_AISpawnHolder");
        holder.SetActive(false);

        // Walk the grid (row, col), skipping the player's slot, until we've placed numberOfAI cars.
        int placed = 0;
        for (int row = 0; placed < numberOfAI; row++)
        {
            for (int col = 0; col < cols && placed < numberOfAI; col++)
            {
                // Row 0, player's column: that's where the player already is.
                if (row == 0 && col == playerCol) continue;

                // Row 0 = same forward line as player (AI beside you).
                // Row 1+ = each row further back by rowSpacing; first back row adds `gap`.
                float rowForward = 0f;
                if (row > 0) rowForward = -1f * (row * rowSpacing + gap);

                // Column lateral offset relative to the player's column.
                float colLateral = (col - playerCol) * lateralSpacing;

                Vector3 spawnPos = anchorPos + fwd * rowForward + right * colLateral;
                spawnPos.y = anchorPos.y;

                // Instantiating under an inactive parent means Awake on the clone's components
                // is deferred until we reparent and activate.
                GameObject ai = Instantiate(carSourcePrefab, holder.transform);
                ai.name = $"AI_Racer_{placed + 1}";

                ConfigureAsAI(ai);

                // Move out of the inactive holder and into world space at the intended spot.
                ai.transform.SetParent(null, false);
                ai.transform.position = spawnPos;
                ai.transform.rotation = anchorRot;

                // Surviving components' Awake fires here.
                ai.SetActive(true);

                // Only after activation are renderer bounds populated. Now snap the AI so its
                // visible wheels/chassis rest on the ground directly below the spawn point.
                PlaceAIOnGround(ai);

                if (logSpawns)
                {
                    AICarController ctrlForLog = ai.GetComponent<AICarController>();
                    float offsetForLog = ctrlForLog != null ? ctrlForLog.groundYOffset : 0f;
                    Debug.Log($"[AIRaceGridSpawner] Spawned {ai.name} at row={row} col={col} pos={ai.transform.position} groundYOffset={offsetForLog:F2}");
                }

                placed++;
            }

            // Safety: cap at a reasonable number of rows so a misconfiguration can't infinite-loop.
            if (row > 20) break;
        }

        Destroy(holder);
    }

    /// <summary>
    /// Remove all components that only make sense for the player, then attach and configure AICarController.
    /// </summary>
    void ConfigureAsAI(GameObject ai)
    {
        // --- Strip player-only behaviours from the root and all children ---
        // Use GetComponentsInChildren(true) so we also strip from disabled/inactive children.
        StripAll<CarController>(ai);
        StripAll<PlayerInput>(ai);
        StripAll<ThirdPersonCamera>(ai);
        StripAll<CarAudio>(ai);
        StripAll<TireSmoke>(ai);
        StripAll<MinimapCamera>(ai);
        StripAll<AudioListener>(ai);

        // Cameras on the clone would fight with the player camera (double render). Disable them.
        foreach (var cam in ai.GetComponentsInChildren<Camera>(true))
        {
            cam.gameObject.SetActive(false);
        }

        // Change the tag so LapCheckpoint ("Player" tag) doesn't count AI as the player.
        // Untagged is safe; we don't need checkpoint handling for AI (AICarController tracks its own waypoint index).
        ai.tag = "Untagged";
        foreach (var t in ai.GetComponentsInChildren<Transform>(true))
        {
            if (t.CompareTag("Player"))
                t.tag = "Untagged";
        }

        // --- Ensure the Rigidbody is present (AICarController requires it) ---
        Rigidbody rb = ai.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = ai.AddComponent<Rigidbody>();
        }
        // AICarController sets this to kinematic in Awake. That means AI-AI physics collisions
        // are handled via the built-in separation pass; AI-vs-player collisions still apply
        // forces to the player's (non-kinematic) rigidbody.
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // The Player_BMW prefab has two BoxColliders offset at z=+1.5 and z=-1.5 with size 1.8
        // deep each, leaving a 1.2-unit unprotected gap in the middle of the car. Without this
        // patch, the player can ram an AI at the right angle and the player's front box slides
        // straight into the AI's empty middle — i.e. clipping. Add a solid middle box to close it.
        PatchMiddleColliderGap(ai);

        // --- Attach and configure the AI controller ---
        AICarController ctrl = ai.GetComponent<AICarController>();
        if (ctrl == null)
            ctrl = ai.AddComponent<AICarController>();

        ctrl.waypointPath = waypointPath;
        ctrl.playerTransform = playerTransform;
        ctrl.playerRigidbody = playerRigidbody;
        ctrl.separationDistance = aiSeparationDistance;
        ctrl.separationStrength = aiSeparationStrength;
        ctrl.raceType = raceType;
        ctrl.totalLaps = totalLaps;

        // --- Speed: match the player's CarController.maxForwardSpeed when possible ---
        float baseSpeed = aiMaxSpeed;
        if (matchPlayerMaxSpeed && playerTransform != null)
        {
            CarController playerCar = playerTransform.GetComponentInParent<CarController>();
            if (playerCar == null) playerCar = playerTransform.GetComponentInChildren<CarController>();
            if (playerCar != null)
            {
                // The player can stack turbo (turboSpeedMultiplier) and drift boost to far
                // exceed their base maxForwardSpeed. Copying maxForwardSpeed raw leaves the AI
                // hopelessly behind whenever the player drifts or turbos. The match factor
                // gives the AI enough baseline headroom to actually keep up.
                baseSpeed = playerCar.maxForwardSpeed * Mathf.Max(0.5f, playerSpeedMatchFactor);
            }
        }

        // --- Per-AI personality variance ---
        // Each AI gets slightly different tuning so the pack spreads out and feels like real drivers.
        // Bias the spread upward: a small minority of AI runs slightly below baseline, the rest
        // run at or above it. Keeps most of the field competitive with the player without making
        // every car identical.
        float below = speedVariance * 0.3f; // only -30% of the variance on the slow side
        float above = speedVariance * 1.3f; // +130% of the variance on the fast side
        float speedMultiplier = 1f + Random.Range(-below, above);
        ctrl.maxSpeed = baseSpeed * speedMultiplier;

        // Some AI brake harder in corners, some barely slow down.
        float baseCornerMult = ctrl.cornerSpeedMultiplier; // keep controller's default as the center
        ctrl.cornerSpeedMultiplier = Mathf.Clamp(
            baseCornerMult * (1f + Random.Range(-cornerAggressionVariance, cornerAggressionVariance)),
            0.2f, 0.95f);

        ctrl.steerNoiseAmount = Random.Range(steerNoiseRange.x, steerNoiseRange.y);
        ctrl.turboCooldown *= Random.Range(turboCooldownMultiplierRange.x, turboCooldownMultiplierRange.y);
        ctrl.accelerationTime = Random.Range(accelerationTimeRange.x, accelerationTimeRange.y);
        ctrl.startReactionDelay = Random.Range(reactionDelayRange.x, reactionDelayRange.y);

        // Ground offset is applied later (in PlaceAIOnGround) after the car is activated,
        // because it needs renderer bounds which aren't valid until the GameObject is live.
    }

    /// <summary>
    /// Moves `ai` so that the LOWEST visible point of the car (bottom of the wheel mesh / chassis)
    /// rests exactly on the ground directly below its spawn position, and computes the
    /// groundYOffset the AICarController needs to maintain that pose as the car moves.
    /// Must be called AFTER the AI has been activated so renderer bounds are populated.
    /// </summary>
    void PlaceAIOnGround(GameObject ai)
    {
        AICarController ctrl = ai.GetComponent<AICarController>();
        if (ctrl == null) return;

        // 1. Find ground Y directly under the AI, ignoring the AI's own colliders.
        if (!RaycastGroundBelow(ai.transform.position, ai.transform, out float groundY))
            return; // no ground found; leave AI at its current Y

        // 2. Find the AI's visible lowest point from combined renderer bounds.
        Bounds? visual = ComputeVisualBounds(ai);
        if (!visual.HasValue) return;

        float lowestVisualY = visual.Value.min.y;

        // 3. Delta = how far to move the AI so its lowest visual Y sits on the ground.
        float delta = groundY - lowestVisualY;
        Vector3 p = ai.transform.position;
        p.y += delta;
        ai.transform.position = p;

        // 4. Record the offset from ground the controller should maintain going forward:
        // offset = transform.y - groundY. That keeps the wheels-on-ground invariant as the AI
        // drives over terrain of varying height.
        ctrl.groundYOffset = p.y - groundY;
    }

    static Bounds? ComputeVisualBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return null;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            b.Encapsulate(renderers[i].bounds);
        }
        return b;
    }

    static bool RaycastGroundBelow(Vector3 origin, Transform ignoreRoot, out float groundY)
    {
        Vector3 rayStart = origin + Vector3.up * 20f;
        RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) { groundY = 0f; return false; }
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit h in hits)
        {
            if (h.collider == null) continue;
            if (ignoreRoot != null)
            {
                if (h.collider.transform == ignoreRoot) continue;
                if (h.collider.transform.IsChildOf(ignoreRoot)) continue;
            }
            groundY = h.point.y;
            return true;
        }
        groundY = 0f;
        return false;
    }

    static void StripAll<T>(GameObject root) where T : Component
    {
        // DestroyImmediate is used intentionally: the clone is still inactive at this point,
        // and we need the component GONE before we activate the GameObject. Plain Destroy only
        // queues destruction for end-of-frame, which means Awake would still fire on activation.
        foreach (var c in root.GetComponentsInChildren<T>(true))
        {
            if (c != null) DestroyImmediate(c);
        }
    }

    /// <summary>
    /// Finds the GameObject that holds Player_BMW's two solid BoxColliders and adds a third one
    /// that covers the empty z-range between them, so nothing can pass through the middle.
    /// Falls back to the root if we can't find a specific host.
    /// </summary>
    static void PatchMiddleColliderGap(GameObject ai)
    {
        BoxCollider[] existing = ai.GetComponentsInChildren<BoxCollider>(true);

        // Look for the two solid chassis boxes (z-center ~ +/-1.5, not triggers).
        GameObject host = null;
        foreach (var bc in existing)
        {
            if (bc == null || bc.isTrigger) continue;
            if (Mathf.Abs(Mathf.Abs(bc.center.z) - 1.5f) < 0.5f)
            {
                host = bc.gameObject;
                break;
            }
        }
        if (host == null) host = ai;

        // Don't double-add if we already patched this clone.
        foreach (var bc in host.GetComponents<BoxCollider>())
        {
            if (!bc.isTrigger && Mathf.Abs(bc.center.z) < 0.2f && bc.size.z > 1f) return;
        }

        BoxCollider mid = host.AddComponent<BoxCollider>();
        mid.isTrigger = false;
        mid.center = new Vector3(-0.09f, 0.28f, 0f);
        // 1.5 deep covers the full 1.2 gap with a bit of overlap into the front/rear boxes.
        mid.size = new Vector3(2.16f, 1.5f, 1.5f);
    }
}
