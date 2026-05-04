using UnityEngine;
using UnityEngine.UI;

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
    }

    void Start()
    {
        if (waypointPath == null) waypointPath = FindFirstObjectByType<AIWaypointPath>();
        if (wrongWayPanel == null)
        {
            foreach (var canv in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                var found = FindDeep(canv.transform, "WrongWayPanel");
                if (found != null) { wrongWayPanel = found.gameObject; break; }
            }
        }
        if (wrongWayPanel != null) wrongWayPanel.SetActive(false);
    }

    static Transform FindDeep(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeep(parent.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
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

    /// <summary>
    /// Finds the waypoint segment closest to the player (by perpendicular distance to the
    /// segment line, not just to a waypoint), and returns that segment's direction.
    /// More robust than picking the nearest single waypoint — when the player is mid-segment,
    /// the nearest-waypoint approach can return a direction that points OFF the road and
    /// produces false wrong-way readings on twisty tracks.
    /// </summary>
    private Vector3 GetPathDirectionAtPlayer()
    {
        int count = waypointPath.WaypointCount;
        if (count < 2) return Vector3.zero;

        Vector3 pos = transform.position; pos.y = 0f;

        int bestSegStart = 0;
        float bestDistSq = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Transform a = waypointPath.GetWaypoint(i);
            Transform b = waypointPath.GetWaypoint(i + 1);
            if (a == null || b == null) continue;

            Vector3 ap = a.position; ap.y = 0f;
            Vector3 bp = b.position; bp.y = 0f;
            Vector3 ab = bp - ap;
            float abLenSq = ab.sqrMagnitude;
            if (abLenSq < 0.0001f) continue;

            float t = Mathf.Clamp01(Vector3.Dot(pos - ap, ab) / abLenSq);
            Vector3 projected = ap + ab * t;
            float dSq = (pos - projected).sqrMagnitude;
            if (dSq < bestDistSq) { bestDistSq = dSq; bestSegStart = i; }
        }

        Transform segA = waypointPath.GetWaypoint(bestSegStart);
        Transform segB = waypointPath.GetWaypoint(bestSegStart + 1);
        if (segA == null || segB == null) return Vector3.zero;

        Vector3 dir = segB.position - segA.position;
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
