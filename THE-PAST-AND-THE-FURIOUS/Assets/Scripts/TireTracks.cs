using UnityEngine;

/// <summary>
/// Auto-spawns TrailRenderer-based tire tracks under each wheel of the car. Pair with FollowWheel,
/// which keeps the trails world-aligned and ground-pinned as the wheels spin and steer.
///
/// Drop this on the car (same GameObject as CarController/WheelVisuals). No Inspector wiring needed
/// — wheels are auto-discovered from WheelVisuals or by name.
/// </summary>
public class TireTracks : MonoBehaviour
{
    [Header("Trail Settings")]
    [Tooltip("How wide each tire track is.")]
    public float trailWidth = 0.18f;

    [Tooltip("How long the track persists (seconds).")]
    public float trailTime = 4f;

    [Tooltip("Color of the track (alpha controls opacity).")]
    public Color trailColor = new Color(0.05f, 0.05f, 0.05f, 0.85f);

    [Tooltip("Distance above wheel center where the trail sits. Negative pushes trail toward ground.")]
    public float groundOffsetY = -0.30f;

    [Tooltip("Min sideways slip (m/s) needed for tracks to render. 0 = always render. ~1.5 = only when sliding/drifting.")]
    public float sidewaysSlipThreshold = 1.5f;

    [Tooltip("If true, tracks also render when the player is using turbo even without slipping.")]
    public bool renderOnTurbo = true;

    [Tooltip("Shader to use for the trail material. Falls back to URP/Unlit, then Sprites/Default.")]
    public Material trailMaterial;

    private CarController carController;
    private TrailRenderer[] trails;

    void Start()
    {
        carController = GetComponent<CarController>();
        Transform[] wheels = FindWheels();
        if (wheels == null || wheels.Length == 0) return;

        Material mat = trailMaterial != null ? trailMaterial : MakeFallbackMaterial();

        trails = new TrailRenderer[wheels.Length];
        for (int i = 0; i < wheels.Length; i++)
        {
            if (wheels[i] == null) continue;

            GameObject anchor = new GameObject("TrailAnchor_" + wheels[i].name);
            anchor.transform.SetParent(transform, worldPositionStays: false);
            anchor.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            TrailRenderer t = anchor.AddComponent<TrailRenderer>();
            t.time = trailTime;
            t.startWidth = trailWidth;
            t.endWidth = trailWidth;
            t.minVertexDistance = 0.05f;
            t.material = mat;
            t.startColor = trailColor;
            t.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
            t.emitting = false;

            FollowWheel fw = anchor.AddComponent<FollowWheel>();
            fw.wheel = wheels[i];
            fw.groundOffsetY = groundOffsetY;

            trails[i] = t;
        }
    }

    void Update()
    {
        if (trails == null || carController == null || carController.rb == null) return;

        Rigidbody rb = carController.rb;
        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
        bool sliding = Mathf.Abs(localVel.x) > sidewaysSlipThreshold;
        bool turbo = renderOnTurbo && carController.IsTurboActive();
        bool drifting = carController.IsDrifting();
        bool emit = sliding || turbo || drifting;

        foreach (var t in trails)
            if (t != null) t.emitting = emit;
    }

    Transform[] FindWheels()
    {
        WheelVisuals wv = GetComponent<WheelVisuals>();
        if (wv != null)
        {
            return new Transform[] { wv.frontLeftWheel, wv.frontRightWheel, wv.rearLeftWheel, wv.rearRightWheel };
        }

        var found = new System.Collections.Generic.List<Transform>();
        foreach (var child in GetComponentsInChildren<Transform>(true))
        {
            if (child == transform) continue;
            string n = child.name.ToLower();
            if (n.Contains("wheel") || n.Contains("tire")) found.Add(child);
        }
        return found.ToArray();
    }

    static Material MakeFallbackMaterial()
    {
        Shader s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s == null) s = Shader.Find("Sprites/Default");
        if (s == null) s = Shader.Find("Unlit/Color");
        return new Material(s);
    }
}
