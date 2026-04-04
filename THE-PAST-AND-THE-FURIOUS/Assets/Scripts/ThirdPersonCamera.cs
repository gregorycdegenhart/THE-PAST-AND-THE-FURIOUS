using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Input")]
    public InputActionReference lookAction;

    [Header("Orbit")]
    public float distance = 6f;
    public float height = 2f;
    public float lookSensitivity = 0.12f;
    public float minPitch = -10f;
    public float maxPitch = 45f;

    [Header("Auto Return")]
    public float idleTimeBeforeReturn = 1.2f;
    public float returnSpeed = 4f;

    [Header("Cursor")]
    public bool lockCursorOnStart = true;

    [Header("Turbo FOV")]
    public CarController carController;
    public float normalFov = 60f;
    public float turboFov = 80f;
    public float fovLerpSpeed = 6f;

    float yaw;
    float pitch = 0f;
    float idleTimer;
    Camera cam;

    void OnEnable()
    {
        if (lookAction != null)
            lookAction.action.Enable();

        if (lockCursorOnStart)
            LockCursor();

        if (target != null)
        {
            yaw = target.eulerAngles.y;
            pitch = 0f;
        }

        cam = GetComponent<Camera>();
        if (cam == null)
            cam = Camera.main;

        if (cam != null)
            cam.fieldOfView = normalFov;
    }

    void OnDisable()
    {
        if (lookAction != null)
            lookAction.action.Disable();

        UnlockCursor();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            UnlockCursor();

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            LockCursor();
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        // FOV
        if (cam != null && carController != null)
        {
            float targetFov = carController.IsTurboActive() ? turboFov : normalFov;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, Time.deltaTime * fovLerpSpeed);
        }

        Vector2 lookInput = Vector2.zero;
        if (lookAction != null)
            lookInput = lookAction.action.ReadValue<Vector2>();

        bool isLooking = Cursor.lockState == CursorLockMode.Locked && lookInput.sqrMagnitude > 0.0001f;

        if (isLooking)
        {
            yaw += lookInput.x * lookSensitivity;
            pitch -= lookInput.y * lookSensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            idleTimer = 0f;
        }
        else
        {
            idleTimer += Time.deltaTime;

            if (idleTimer >= idleTimeBeforeReturn)
            {
                float targetYaw = target.eulerAngles.y;
                yaw = Mathf.LerpAngle(yaw, targetYaw, Time.deltaTime * returnSpeed);
                pitch = Mathf.Lerp(pitch, 0f, Time.deltaTime * returnSpeed);
            }
        }

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 offset = rotation * new Vector3(0f, height, -distance);

        transform.position = target.position + offset;
        transform.LookAt(target.position + Vector3.up * 1.5f);
    }

    void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}