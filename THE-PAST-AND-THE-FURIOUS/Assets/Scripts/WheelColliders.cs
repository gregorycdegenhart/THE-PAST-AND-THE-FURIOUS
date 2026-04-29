using UnityEngine;

public class WheelColliders : MonoBehaviour
{
    [Header("Collider Settings")]
    public float radius = 0.35f;

    static PhysicsMaterial sharedFrictionlessMat;

    void Awake()
    {
        // Wheel grip in this game is faked by CarController.sidewaysDrag, NOT by the physics
        // material on the wheel colliders. The default ~0.6 friction on a steep slope under
        // heavy normal force can exceed propulsion at low speed and bring the car to a halt
        // partway up a ramp. Make the wheel contacts frictionless so propulsion is the only
        // tangent-to-slope force.
        if (sharedFrictionlessMat == null)
        {
            sharedFrictionlessMat = new PhysicsMaterial("WheelFrictionless")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum,
            };
        }

        WheelVisuals wheelVisuals = GetComponent<WheelVisuals>();
        Transform[] wheels = null;

        if (wheelVisuals != null)
        {
            wheels = new Transform[] {
                wheelVisuals.frontLeftWheel,
                wheelVisuals.frontRightWheel,
                wheelVisuals.rearLeftWheel,
                wheelVisuals.rearRightWheel
            };
        }

        if (wheels == null || wheels.Length == 0)
        {
            var found = new System.Collections.Generic.List<Transform>();
            foreach (var child in GetComponentsInChildren<Transform>())
            {
                if (child.name.ToLower().Contains("tire") || child.name.ToLower().Contains("wheel"))
                    found.Add(child);
            }
            wheels = found.ToArray();
        }

        foreach (var wheel in wheels)
        {
            if (wheel == null) continue;

            // Capsules on a spinning transform produce shifting contact patches and catch on
            // terrain edges. Spheres are rotation-invariant and roll cleanly.
            var oldCapsule = wheel.GetComponent<CapsuleCollider>();
            if (oldCapsule != null) Destroy(oldCapsule);

            SphereCollider existing = wheel.GetComponent<SphereCollider>();
            if (existing != null)
            {
                existing.material = sharedFrictionlessMat;
                continue;
            }

            SphereCollider col = wheel.gameObject.AddComponent<SphereCollider>();
            col.radius = radius;
            col.center = Vector3.zero;
            col.material = sharedFrictionlessMat;
        }
    }
}
