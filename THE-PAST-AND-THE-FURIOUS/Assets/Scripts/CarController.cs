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

        // preserve your original constraints
        rb.constraints = RigidbodyConstraints.FreezeRotationX | 
                         RigidbodyConstraints.FreezeRotationZ | 
                         RigidbodyConstraints.FreezeRotationY;

        rb.centerOfMass = centerOfMassOffset;

        rb.useGravity = true; // make sure this is on
        rb.AddForce(Physics.gravity * 4f * rb.mass);
    }

    void Update()
    {
        // handle turbo timers
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
    }

    void FixedUpdate()
    {
        // At the start of FixedUpdate, apply extra gravity
        rb.AddForce(Physics.gravity * 2f * rb.mass, ForceMode.Force);
        
        Vector3 forward = transform.forward;
        forward.y = 0f; // flatten so we only move along ground
        forward.Normalize();

        // horizontal velocity
        Vector3 flatVel = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);

        // desired speed along forward
        float targetSpeed = moveInput.y * maxForwardSpeed * speedMultiplier;
        if (targetSpeed < 0f) targetSpeed = Mathf.Max(targetSpeed, -maxReverseSpeed * speedMultiplier);

        Vector3 desiredVel = forward * targetSpeed;

        // calculate force needed to reach desired velocity
        Vector3 force = (desiredVel - flatVel) * rb.mass / Time.fixedDeltaTime;

        // clamp force for stability
        force = Vector3.ClampMagnitude(force, acceleration * rb.mass);

        // apply force, leave gravity alone
        rb.AddForce(force, ForceMode.Force);

        // turning
        if (Mathf.Abs(moveInput.x) > 0.1f)
        {
            float turn = moveInput.x * turnSpeed * Time.fixedDeltaTime * Mathf.Sign(Vector3.Dot(rb.linearVelocity, forward));
            rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turn, 0f));
        }

        // sideways drag
        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
        localVel.x *= sidewaysDrag;
        rb.linearVelocity = transform.TransformDirection(localVel);

        if (IsGrounded(out RaycastHit hit))
        {
            rb.AddForce(Vector3.down * groundStickForce, ForceMode.Acceleration);

            // Also clamp upward velocity so bumps don't launch the car
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

    public void OnTurbo(InputValue value)
    {
        if (value.isPressed && !isTurboActive && cooldownTimer <= 0f)
        {
            isTurboActive = true;
            turboTimer = turboDuration;
            SetSpeedMultiplier(turboSpeedMultiplier);
        }
    }

    // --- Turbo / external control ---
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