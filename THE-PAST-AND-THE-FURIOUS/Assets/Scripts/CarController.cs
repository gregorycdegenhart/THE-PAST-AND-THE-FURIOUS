using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("Movement")]
    public float acceleration = 30f;
    public float maxForwardSpeed = 25f;
    public float maxReverseSpeed = 10f;
    public float turnSpeed = 80f;
    public float deceleration = 10f;

    public Vector2 moveInput;

    [Header("Turbo")]
    public float turboSpeedMultiplier = 2f;
    public float turboDuration = 2f;
    public float turboCooldown = 5f;

    [Header("Stability")]
    public float sidewaysDrag = 0.8f;
    public Vector3 centerOfMassOffset = new Vector3(0f, -0.5f, 0f);

    [Header("Rigidbody References")]
    public Rigidbody rb;

    private bool isTurboActive = false;
    private float turboTimer = 0f;
    private float cooldownTimer = 0f;
    private float speedMultiplier = 1f;

    [Header("Drift")]
    public float driftGrip = 0.2f;
    public float driftTurnMultiplier = 3f;
    public float driftEntryKick = 9f;
    public float driftBoostPerSecond = 0.15f;
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
    public float groundStickForce = 40f;
    public float maxUpVelocityWhenGrounded = -2f;

    void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        rb.mass = 120f;
        rb.linearDamping = 0f;
        rb.angularDamping = 2f;

        rb.constraints = RigidbodyConstraints.None;
        /* rb.constraints = RigidbodyConstraints.FreezeRotationX |
                         RigidbodyConstraints.FreezeRotationZ |
                         RigidbodyConstraints.FreezeRotationY; */

        rb.centerOfMass = centerOfMassOffset;
        rb.useGravity = true;
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

        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 flatVel = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
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

        bool airborneCoasting = !IsGrounded(out RaycastHit hit) && Mathf.Abs(moveInput.y) < 0.01f;

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

        // --- SIDEWAYS DRAG ---
        // This is where the magic happens:
        // Normal: high drag (0.8) = car goes where it faces
        // Drift: low drag (0.35) = car slides, momentum carries sideways
        // Release: drag slowly returns, so the slide carries and gradually settles
        float grip = Mathf.Lerp(sidewaysDrag, driftGrip, driftAmount);
        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
        localVel.x *= grip;
        rb.linearVelocity = transform.TransformDirection(localVel);

        // --- GROUND STICK ---
        if (IsGrounded(out _))
        {
            rb.AddForce(Vector3.down * groundStickForce, ForceMode.Acceleration);

            if (rb.linearVelocity.y > maxUpVelocityWhenGrounded)
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
