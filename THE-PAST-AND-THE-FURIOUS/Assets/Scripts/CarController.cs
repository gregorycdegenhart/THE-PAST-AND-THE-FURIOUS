using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("Movement")]
    public float acceleration = 30f;
    public float maxForwardSpeed = 25f;
    public float maxReverseSpeed = 10f;
    public float turnSpeed = 170f;
    public float deceleration = 10f;

    public Vector2 moveInput;

    [Header("Turbo")]
    public float turboSpeedMultiplier = 2f;
    public float turboDuration = 2f;
    public float turboCooldown = 5f;

    [Header("Stability")]
    public float sidewaysDrag = 0.2f;
    public Vector3 centerOfMassOffset = new Vector3(0f, -0.5f, 0f);

    [Header("Rigidbody References")]
    public Rigidbody rb;

    private bool isTurboActive = false;
    private float turboTimer = 0f;
    private float cooldownTimer = 0f;
    private float speedMultiplier = 1f;

    [Header("Drift")]
    public float driftGrip = 0.12f;
    public float driftTurnMultiplier = 1.2f;
    public float driftEntryKick = 5f;
    public float driftBoostPerSecond = 0.18f;
    public float driftBoostMax = 1.5f;
    public float driftBoostForce = 20f;

    private bool isDrifting = false;
    private bool wasDrifting = false;
    private float driftAmount = 0f;
    private float driftBoostCharge = 0f;
    private Vector3 preSlideVelocity;

    [Header("Ground Stick")]
    public LayerMask groundMask;
    public float groundCheckDistance = 0.8f;
    // Lighter than before — heavy stick force was stacking with gravity to ~70 m/s² of constant
    // downforce, which made the rigid sphere wheels chatter against the ground every fixed step.
    public float groundStickForce = 18f;
    // Only clamps when y-velocity is high enough to be a launch — normal ramp climb stays under this.
    public float maxUpVelocityWhenGrounded = 3f;

    void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        rb.mass = 120f;
        rb.linearDamping = 0f;
        rb.angularDamping = 5f;

        // Only freeze Y rotation (yaw is driven by MoveRotation, which bypasses the constraint
        // anyway). X/Z stay free so the body naturally pitches/rolls to follow ramp slopes.
        // Conflict-resolution note: main branch had switched this to RigidbodyConstraints.None,
        // but with the new physics (sphere wheels, slope assist, AI pass-through) we need the
        // Y-axis constrained or the car spins from glancing contacts and physics torque.
        rb.constraints = RigidbodyConstraints.FreezeRotationY;

        rb.centerOfMass = centerOfMassOffset;
        rb.useGravity = true;

        // If groundMask was left empty in the inspector, fall back to "everything except this car's layer".
        // Otherwise IsGrounded silently returns false forever and the down-stick / Y-clamp never apply.
        if (groundMask.value == 0)
            groundMask = ~(1 << gameObject.layer);
    }

    void LateUpdate()
    {
        // Safety net only — normal slope tilt (pitch up a ramp, roll on a banked turn) is left alone.
        // This kicks in only on extreme tilts that mean the car got flipped by terrain jank
        // (the "rotates to face up" bug). 60 degrees is well past anything you'd hit on a real ramp.
        Vector3 e = transform.eulerAngles;
        float xTilt = e.x > 180f ? e.x - 360f : e.x;
        float zTilt = e.z > 180f ? e.z - 360f : e.z;
        if (Mathf.Abs(xTilt) > 60f || Mathf.Abs(zTilt) > 60f)
        {
            transform.rotation = Quaternion.Euler(0f, e.y, 0f);
            if (rb != null) rb.angularVelocity = Vector3.zero;
        }
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
                speedMultiplier = 1f;
            }
        }
        else if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
        }

        isDrifting = false;
        if (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed)
            isDrifting = true;
        if (Gamepad.current != null && Gamepad.current.rightTrigger.ReadValue() > 0.3f)
            isDrifting = true;
    }

    void FixedUpdate()
    {
        rb.AddForce(Physics.gravity * 2f * rb.mass, ForceMode.Force);

        // Don't drive (or even update drift state) until the GO signal. AICarController already
        // gates on this — without the same gate here, Map 2/3 can let the player roll forward
        // before the countdown finishes if their CountdownUI's playerInput reference is unwired.
        if (!CountdownUI.RaceStarted)
        {
            moveInput = Vector2.zero;
            return;
        }

        // One raycast per fixed step — used for slope-aligned forward, airborne check, and ground stick.
        bool grounded = IsGrounded(out RaycastHit groundHit);
        Vector3 surfaceNormal = grounded ? groundHit.normal : Vector3.up;

        // SLOPE ASSIST: cancel the slope-tangent component of gravity when grounded. Without
        // this, even small terrain irregularities + the 3x effective gravity can create enough
        // tangent drag at low speed to halt the car partway up a ramp. With it, slope angle
        // doesn't degrade propulsion — feels like flat-ground driving on any incline.
        if (grounded)
        {
            Vector3 totalGravity = Physics.gravity * 3f;
            Vector3 gravityAlongSlope = Vector3.ProjectOnPlane(totalGravity, surfaceNormal);
            rb.AddForce(-gravityAlongSlope, ForceMode.Acceleration);
        }

        // Drive direction follows the surface plane so propulsion stays aligned with ramps
        // instead of being projected onto the world horizontal (which made the car stall on inclines).
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, surfaceNormal);
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = transform.forward;
            forward.y = 0f;
        }
        forward.Normalize();

        // Velocity projected onto the same plane as `forward`, so the propulsion delta lives
        // entirely in the drive plane (slope plane when grounded, world horizontal in air).
        Vector3 flatVel = Vector3.ProjectOnPlane(rb.linearVelocity, surfaceNormal);
        float speed = flatVel.magnitude;

        // =====================
        // DRIFT STATE
        // =====================
        // Builds up over 0.25s, bleeds out over 0.6s (momentum carry on release)
        if (isDrifting)
            driftAmount = Mathf.MoveTowards(driftAmount, 1f, Time.fixedDeltaTime * 4f);
        else
            driftAmount = Mathf.MoveTowards(driftAmount, 0f, Time.fixedDeltaTime * 1.7f);

        // Entry kick - bigger swing
        if (isDrifting && !wasDrifting && speed > 3f)
        {
            preSlideVelocity = rb.linearVelocity;
            float steerDir = Mathf.Abs(moveInput.x) > 0.1f ? Mathf.Sign(moveInput.x) : 1f;
            // Scale kick with speed - faster = bigger swing
            float kickScale = Mathf.Clamp01(speed / maxForwardSpeed);
            rb.AddForce(transform.right * steerDir * driftEntryKick * (0.5f + kickScale), ForceMode.VelocityChange);
            driftBoostCharge = 0f;
        }

        // Drift exit - release the boost
        if (!isDrifting && wasDrifting && driftBoostCharge > 0.2f)
        {
            float boostPower = driftBoostCharge * driftBoostForce;
            rb.AddForce(transform.forward * boostPower, ForceMode.VelocityChange);
            driftBoostCharge = 0f;
        }
        wasDrifting = isDrifting;

        // Charge boost while drifting - more sideways slide = faster charge
        if (isDrifting && speed > 3f)
        {
            Vector3 lv = transform.InverseTransformDirection(rb.linearVelocity);
            float slideAmount = Mathf.Abs(lv.x) / Mathf.Max(speed, 1f);
            driftBoostCharge = Mathf.Min(driftBoostCharge + slideAmount * driftBoostPerSecond * Time.fixedDeltaTime * 60f, driftBoostMax);
        }

        // =====================
        // THE KEY TO GOOD DRIFT:
        // Car rotates via steering, but velocity stays in the OLD direction.
        // This creates the angle between where the car faces and where it moves.
        // =====================

        // --- TURNING ---
        float turnMult = Mathf.Lerp(1f, driftTurnMultiplier, driftAmount);
        if (Mathf.Abs(moveInput.x) > 0.1f)
        {
            float turn = moveInput.x * turnSpeed * turnMult * Time.fixedDeltaTime;
            // During normal driving, only turn in direction of travel
            // During drift, always allow full rotation regardless of velocity
            if (driftAmount < 0.3f)
                turn *= Mathf.Sign(Vector3.Dot(rb.linearVelocity, forward));
            rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turn, 0f));
        }

        // --- FORWARD FORCE ---
        // During drift: push force along car's forward but velocity carries in slide direction
        float targetSpeed = moveInput.y * maxForwardSpeed * speedMultiplier;
        if (targetSpeed < 0f) targetSpeed = Mathf.Max(targetSpeed, -maxReverseSpeed * speedMultiplier);

        bool airborneCoasting = !grounded && Mathf.Abs(moveInput.y) < 0.01f;

        if (!airborneCoasting)
        {
            if (driftAmount > 0.3f && moveInput.y > 0.1f)
            {
                // During drift: add force along car's forward to maintain speed
                // but DON'T correct sideways velocity - that's the slide
                float forwardComponent = Vector3.Dot(rb.linearVelocity, forward);
                float desiredForward = maxForwardSpeed * speedMultiplier * 0.9f;
                if (forwardComponent < desiredForward)
                {
                    rb.AddForce(forward * acceleration * rb.mass * 0.7f, ForceMode.Force);
                }
            }
            else
            {
                // Normal driving
                Vector3 desiredVel = forward * targetSpeed;
                Vector3 force = (desiredVel - flatVel) * rb.mass / Time.fixedDeltaTime;
                force = Vector3.ClampMagnitude(force, acceleration * rb.mass);
                rb.AddForce(force, ForceMode.Force);
            }
        }

        // --- SIDEWAYS DRAG (speed-preserving) ---
        // Zeroing localVel.x while leaving the rest alone makes the car shed total speed every
        // turn — at a 30° angle, that's ~13% magnitude loss per redirect. We instead reduce
        // the lateral component and rescale the velocity to its original magnitude, so turning
        // REDIRECTS speed into forward instead of bleeding it. Drift mode (high grip lerp toward
        // driftGrip) reduces the rescale aggression so a slide still feels like a slide.
        float grip = Mathf.Lerp(sidewaysDrag, driftGrip, driftAmount);
        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
        float originalMag = localVel.magnitude;
        localVel.x *= grip;
        // Rescale to preserve total speed — but only when not deeply drifting (so big slides
        // don't get magic-rescaled into infinite forward speed).
        if (originalMag > 0.1f && driftAmount < 0.5f)
        {
            float newMag = localVel.magnitude;
            if (newMag > 0.001f) localVel *= (originalMag / newMag);
        }
        rb.linearVelocity = transform.TransformDirection(localVel);

        // --- GROUND STICK ---
        if (grounded)
        {
            // Push along the surface normal (not straight down) so on a ramp the stick force
            // doesn't fight propulsion up the slope.
            rb.AddForce(-surfaceNormal * groundStickForce, ForceMode.Acceleration);

            // Clamp upward Y velocity on flat ground OR small bumps. Threshold of 0.88 ≈ 28° of
            // surface tilt, which catches the kind of small bump that tips a sphere wheel into a
            // launch but stays out of the way for real ramps (35°+).
            bool flatishGround = Vector3.Dot(surfaceNormal, Vector3.up) > 0.88f;
            if (flatishGround && rb.linearVelocity.y > maxUpVelocityWhenGrounded)
            {
                Vector3 v = rb.linearVelocity;
                v.y = maxUpVelocityWhenGrounded;
                rb.linearVelocity = v;
            }
        }
    }

    // --- Input System ---
    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnDrift(InputValue value) { }

    public bool IsDrifting() => isDrifting || driftAmount > 0.1f;
    public float GetDriftBoostCharge() => driftBoostCharge / driftBoostMax;

    public void OnTurbo(InputValue value)
    {
        if (value.isPressed && !isTurboActive && cooldownTimer <= 0f)
        {
            isTurboActive = true;
            turboTimer = turboDuration;
            SetSpeedMultiplier(turboSpeedMultiplier);
        }
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = multiplier;
    }

    private bool IsGrounded(out RaycastHit hit)
    {
        Vector3 origin = transform.position + Vector3.up * 0.2f;
        return Physics.Raycast(origin, Vector3.down, out hit, groundCheckDistance, groundMask, QueryTriggerInteraction.Ignore);
    }

    public float GetCooldownProgress() => cooldownTimer > 0f ? cooldownTimer / turboCooldown : 0f;
    public bool IsTurboActive() => isTurboActive;
}
