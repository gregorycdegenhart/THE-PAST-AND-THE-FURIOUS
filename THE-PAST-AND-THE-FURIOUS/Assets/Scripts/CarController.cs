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

    private Rigidbody rb;
    private Vector2 moveInput;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.linearDamping = 0f;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    void FixedUpdate()
    {
        // acceleration and steering
        Vector3 forward = transform.forward;

        Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        float speedAlongForward = Vector3.Dot(flatVel, forward);

        if (moveInput.y > 0)
        {
            speedAlongForward += moveInput.y * acceleration * Time.fixedDeltaTime;
            speedAlongForward = Mathf.Clamp(speedAlongForward, -maxReverseSpeed, maxForwardSpeed);
        }
        else if (moveInput.y < 0)
        {
            speedAlongForward += moveInput.y * acceleration * Time.fixedDeltaTime;
            speedAlongForward = Mathf.Clamp(speedAlongForward, -maxReverseSpeed, maxForwardSpeed);
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

        // debugging, stopping any unwanted spinning
        rb.angularVelocity = new Vector3(0f, rb.angularVelocity.y, 0f);

        // debugging, fixing velocity to be only forward/back
        flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 correctedVel = Vector3.Project(flatVel, transform.forward);
        rb.linearVelocity = correctedVel + Vector3.up * rb.linearVelocity.y;
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }
}