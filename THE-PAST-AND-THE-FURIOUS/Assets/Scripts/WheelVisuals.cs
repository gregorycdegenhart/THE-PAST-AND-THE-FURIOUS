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

    private CarController carController;
    private float currentSteerAngle = 0f;

    void Awake()
    {
        carController = GetComponentInParent<CarController>();
    }

    void Update()
    {
        HandleSteering();
    }

    void HandleSteering()
    {
        float steerInput = carController.moveInput.x;
        float targetAngle = steerInput * maxSteerAngle;

        currentSteerAngle = Mathf.Lerp(currentSteerAngle, targetAngle, Time.deltaTime * steerSpeed);

        if (frontLeftWheel != null)
            frontLeftWheel.localRotation = Quaternion.Euler(0f, currentSteerAngle, 0f);

        if (frontRightWheel != null)
            frontRightWheel.localRotation = Quaternion.Euler(0f, currentSteerAngle, 0f);
    }
}