using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    public float acceleration = 30f;
    public float maxForwardSpeed = 25f;
    public float maxReverseSpeed = 10f;
    public float turnSpeed = 80f;
    public float deceleration = 10f;
    private float speedMultiplier = 1f;

    [Header("Turbo")]
    public float turboSpeedMultiplier = 2f;
    public float turboDuration = 2f;
    public float turboCooldown = 5f;

    private Rigidbody rb;
    private Vector2 moveInput;

    private bool isTurboActive = false;
    private float turboTimer = 0f;
    private float cooldownTimer = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.linearDamping = 0f;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.centerOfMass = new Vector3(0, -0.5f, 0);
    }

    void Update()
    {
        // Tick timers in Update (time-based, not physics-based)
        if (isTurboActive)
        {
            turboTimer -= Time.deltaTime;
            if (turboTimer <= 0f)
            {
                isTurboActive = false;
                cooldownTimer = turboCooldown;
                SetSpeedMultiplier(1f);
            }
        }
        else if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
        }
    }

    void FixedUpdate()
    {
        Vector3 forward = transform.forward;

        Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        float speedAlongForward = Vector3.Dot(flatVel, forward);

        if (moveInput.y > 0)
        {
            speedAlongForward += moveInput.y * acceleration * speedMultiplier * Time.fixedDeltaTime;
            speedAlongForward = Mathf.Clamp(speedAlongForward, -maxReverseSpeed * speedMultiplier, maxForwardSpeed * speedMultiplier);
        }
        else if (moveInput.y < 0)
        {
            speedAlongForward += moveInput.y * acceleration * speedMultiplier * Time.fixedDeltaTime;
            speedAlongForward = Mathf.Clamp(speedAlongForward, -maxReverseSpeed * speedMultiplier, maxForwardSpeed * speedMultiplier);
        }
        else
        {
            speedAlongForward = Mathf.MoveTowards(speedAlongForward, 0f, deceleration * Time.fixedDeltaTime);
        }

        rb.linearVelocity = forward * speedAlongForward + Vector3.up * rb.linearVelocity.y;

        if (Mathf.Abs(speedAlongForward) > 0.1f)
        {
            float turn = moveInput.x * turnSpeed * Time.fixedDeltaTime * Mathf.Sign(speedAlongForward);
            rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turn, 0f));
        }

        rb.angularVelocity = new Vector3(0f, rb.angularVelocity.y, 0f);

        flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 correctedVel = Vector3.Project(flatVel, transform.forward);
        rb.linearVelocity = correctedVel + Vector3.up * rb.linearVelocity.y;
        rb.angularVelocity = new Vector3(0f, rb.angularVelocity.y, 0f);
    }

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

    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = multiplier;
    }

    // expose turbo state for UI (cooldown 0-1, active state)
    public float GetCooldownProgress() => cooldownTimer > 0f ? cooldownTimer / turboCooldown : 0f;
    public bool IsTurboActive() => isTurboActive;
}