using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerWrongWayDetector : MonoBehaviour
{
    [Header("Path")]
    [Tooltip("The same AIWaypointPath the AI cars use. Drop the WaypointPath GameObject here.")]
    public AIWaypointPath waypointPath;

    [Header("Detection")]
    [Tooltip("Player must be moving faster than this (m/s) for wrong-way to register. Stops the warning from firing while parked.")]
    public float minSpeed = 4f;

    [Tooltip("Dot product threshold between player velocity and path direction. Below this counts as wrong-way (e.g. -0.3 means ~107 deg off).")]
    public float wrongWayDotThreshold = -0.3f;

    [Tooltip("Player must be wrong-way for this many seconds before the UI fires. Prevents flicker on legitimate spinouts.")]
    public float sustainSeconds = 1.0f;

    [Header("UI")]
    [Tooltip("Optional UI GameObject to enable/disable when wrong-way state changes. Wire this to your wrong-way popup.")]
    public GameObject wrongWayPanel;

    public bool IsWrongWay { get; private set; }

    private Rigidbody rb;
    private float wrongWayTimer = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (wrongWayPanel != null) wrongWayPanel.SetActive(false);
    }

    void FixedUpdate()
    {
        if (waypointPath == null || waypointPath.WaypointCount < 2)
        {
            SetWrongWay(false);
            return;
        }

        Vector3 vel = rb.linearVelocity;
        vel.y = 0f;
        float speed = vel.magnitude;

        if (speed < minSpeed)
        {
            wrongWayTimer = 0f;
            SetWrongWay(false);
            return;
        }

        Vector3 pathDir = GetPathDirectionAtPlayer();
        if (pathDir.sqrMagnitude < 0.001f)
        {
            SetWrongWay(false);
            return;
        }

        float dot = Vector3.Dot(vel.normalized, pathDir);

        if (dot < wrongWayDotThreshold)
        {
            wrongWayTimer += Time.fixedDeltaTime;
            if (wrongWayTimer >= sustainSeconds)
                SetWrongWay(true);
        }
        else
        {
            wrongWayTimer = 0f;
            SetWrongWay(false);
        }
    }

    private Vector3 GetPathDirectionAtPlayer()
    {
        int nearest = 0;
        float nearestDist = float.MaxValue;
        Vector3 pos = transform.position;

        for (int i = 0; i < waypointPath.WaypointCount; i++)
        {
            Transform wp = waypointPath.GetWaypoint(i);
            if (wp == null) continue;
            float d = (wp.position - pos).sqrMagnitude;
            if (d < nearestDist) { nearestDist = d; nearest = i; }
        }

        Transform a = waypointPath.GetWaypoint(nearest);
        Transform b = waypointPath.GetWaypoint(nearest + 1);
        if (a == null || b == null) return Vector3.zero;

        Vector3 dir = b.position - a.position;
        dir.y = 0f;
        return dir.normalized;
    }

    private void SetWrongWay(bool value)
    {
        if (IsWrongWay == value) return;
        IsWrongWay = value;
        if (wrongWayPanel != null) wrongWayPanel.SetActive(value);
    }
}
