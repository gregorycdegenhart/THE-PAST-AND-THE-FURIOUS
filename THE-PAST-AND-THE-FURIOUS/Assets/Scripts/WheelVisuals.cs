using UnityEngine;

public class WheelVisuals : MonoBehaviour
{
    [Header("Wheel Transforms")]
    public Transform frontLeftWheel;
    public Transform frontRightWheel;
    public Transform rearLeftWheel;
    public Transform rearRightWheel;

    [Header("Steering")]
    public float maxSteerAngle = 30f;
    public float steerSpeed = 5f;

    [Header("Spin")]
    public float wheelRadius = 0.35f;
    public Vector3 spinAxis = Vector3.right;

    private CarController carController;
    private float currentSteerAngle = 0f;
    private float spinAngle = 0f;

    void Awake()
    {
        carController = GetComponentInParent<CarController>();
    }

    void Update()
    {
        // AI cars have CarController stripped by AIRaceGridSpawner but keep WheelVisuals.
        // Without this guard, every AI car throws NullReferenceException every frame.
        if (carController == null) return;

        HandleSteering();
        HandleSpin();
    }

    void HandleSteering()
    {
        float steerInput = carController.moveInput.x;
        float targetAngle = steerInput * maxSteerAngle;

        currentSteerAngle = Mathf.Lerp(currentSteerAngle, targetAngle, Time.deltaTime * steerSpeed);

        Quaternion spin = Quaternion.AngleAxis(spinAngle, spinAxis);

        if (frontLeftWheel != null)
            frontLeftWheel.localRotation = Quaternion.Euler(0f, currentSteerAngle, 0f) * spin;

        if (frontRightWheel != null)
            frontRightWheel.localRotation = Quaternion.Euler(0f, currentSteerAngle, 0f) * spin;
    }

    void HandleSpin()
    {
        if (carController.rb == null) return;
        float forwardSpeed = Vector3.Dot(carController.rb.linearVelocity, carController.transform.forward);
        float rpm = forwardSpeed / (2f * Mathf.PI * wheelRadius) * 360f;
        spinAngle += rpm * Time.deltaTime;

        Quaternion spin = Quaternion.AngleAxis(spinAngle, spinAxis);

        if (rearLeftWheel != null)
            rearLeftWheel.localRotation = spin;

        if (rearRightWheel != null)
            rearRightWheel.localRotation = spin;
    }
}