using UnityEngine;

public class TireSmoke : MonoBehaviour
{
    [Header("References")]
    public CarController carController;
    public Transform[] tirePositions;

    [Header("Smoke Prefab")]
    [Tooltip("Drag Exhaust Smoke prefab here, or leave empty to auto-load from Assets/Particles")]
    public ParticleSystem smokePrefab;

    [Header("Thresholds")]
    public float sidewaysSlipThreshold = 2f;
    public float brakingThreshold = 0.3f;
    public float turboSmokeMultiplier = 2f;
    public float baseEmissionRate = 30f;

    private ParticleSystem[] smokeParticles;

    void Start()
    {
        if (carController == null)
            carController = GetComponent<CarController>();

        // Auto-find wheel positions if not assigned
        if (tirePositions == null || tirePositions.Length == 0)
            tirePositions = AutoFindWheels();

        if (tirePositions == null || tirePositions.Length == 0)
            tirePositions = CreateDefaultWheelPositions();

        // Auto-load the Exhaust Smoke prefab from Resources if not assigned
        if (smokePrefab == null)
        {
            // Try loading as ParticleSystem
            smokePrefab = Resources.Load<ParticleSystem>("Exhaust Smoke");

            // If prefab root isn't a ParticleSystem, try loading as GameObject
            if (smokePrefab == null)
            {
                var go = Resources.Load<GameObject>("Exhaust Smoke");
                if (go != null)
                    smokePrefab = go.GetComponent<ParticleSystem>();
            }
        }

        // Create smoke instances at each wheel
        smokeParticles = new ParticleSystem[tirePositions.Length];
        for (int i = 0; i < tirePositions.Length; i++)
        {
            if (smokePrefab != null)
            {
                // Use the artist's prefab
                var instance = Instantiate(smokePrefab, tirePositions[i]);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.Stop();
                var emission = instance.emission;
                emission.rateOverTime = 0f;
                smokeParticles[i] = instance;
            }
            else
            {
                // Fallback: create procedural smoke
                smokeParticles[i] = CreateProceduralSmoke(tirePositions[i]);
            }
        }

    }

    void Update()
    {
        if (carController == null || smokeParticles == null) return;

        Rigidbody rb = carController.rb;
        if (rb == null) return;

        float speed = rb.linearVelocity.magnitude;
        float speedNormalized = Mathf.Clamp01(speed / carController.maxForwardSpeed);

        // Always emit smoke, scaled by velocity
        float rate = Mathf.Lerp(15f, 150f, speedNormalized);

        Vector3 localVel = carController.transform.InverseTransformDirection(rb.linearVelocity);
        float sidewaysSpeed = Mathf.Abs(localVel.x);

        bool isSliding = sidewaysSpeed > sidewaysSlipThreshold;
        bool isTurbo = carController.IsTurboActive();
        bool isDrifting = carController.IsDrifting();

        if (isSliding) rate *= Mathf.Clamp(sidewaysSpeed / sidewaysSlipThreshold, 1f, 3f);
        if (isDrifting) rate *= 2.5f;
        if (isTurbo) rate *= turboSmokeMultiplier;

        for (int i = 0; i < smokeParticles.Length; i++)
        {
            if (smokeParticles[i] == null) continue;

            var emission = smokeParticles[i].emission;
            emission.rateOverTime = rate;

            var main = smokeParticles[i].main;
            main.startSize = Mathf.Lerp(3f, 10f, speedNormalized);
            main.startLifetime = Mathf.Lerp(2f, 5f, speedNormalized);
            main.startSpeed = Mathf.Lerp(1f, 4f, speedNormalized);

            if (!smokeParticles[i].isPlaying)
                smokeParticles[i].Play();
        }
    }

    // --- Procedural fallback ---
    ParticleSystem CreateProceduralSmoke(Transform parent)
    {
        GameObject smokeObj = new GameObject("TireSmoke_Procedural");
        smokeObj.transform.SetParent(parent);
        smokeObj.transform.localPosition = Vector3.zero;

        ParticleSystem ps = smokeObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 4f;
        main.startSize = 6f;
        main.startSpeed = 2f;
        main.startColor = new Color(0.95f, 0.95f, 0.95f, 0.8f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 2000;
        main.gravityModifier = -0.25f;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(1f, 5f)));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(0.9f, 0.9f, 0.9f), 0f), new GradientColorKey(new Color(0.7f, 0.7f, 0.7f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = grad;

        ps.Stop();
        return ps;
    }

    // --- Wheel auto-detection ---
    Transform[] AutoFindWheels()
    {
        // Only use rear wheels for smoke
        WheelVisuals wv = GetComponent<WheelVisuals>();
        if (wv != null && wv.rearLeftWheel != null && wv.rearRightWheel != null)
            return new Transform[] { wv.rearLeftWheel, wv.rearRightWheel };

        // Fallback: search for rear-named transforms
        string[] rearPatterns = { "rear", "back", "rl", "rr", "tire3", "tire4" };
        var found = new System.Collections.Generic.List<Transform>();

        foreach (var child in GetComponentsInChildren<Transform>())
        {
            if (child == transform) continue;
            string name = child.name.ToLower();
            foreach (string pattern in rearPatterns)
            {
                if (name.Contains(pattern))
                {
                    found.Add(child);
                    break;
                }
            }
        }

        return found.Count > 0 ? found.ToArray() : null;
    }

    Transform[] CreateDefaultWheelPositions()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        Bounds bounds = new Bounds(transform.position, Vector3.one);
        foreach (var r in renderers)
            bounds.Encapsulate(r.bounds);

        float halfWidth = bounds.extents.x * 0.8f;
        float halfLength = bounds.extents.z * 0.7f;
        float bottom = bounds.min.y + 0.1f;

        // Only rear wheels
        Vector3[] offsets = {
            new Vector3(-halfWidth, bottom - transform.position.y, -halfLength),  // RL
            new Vector3(halfWidth, bottom - transform.position.y, -halfLength),   // RR
        };

        Transform[] wheels = new Transform[2];
        for (int i = 0; i < 2; i++)
        {
            var go = new GameObject("SmokePoint_" + i);
            go.transform.SetParent(transform);
            go.transform.localPosition = offsets[i];
            wheels[i] = go.transform;
        }

        return wheels;
    }
}
