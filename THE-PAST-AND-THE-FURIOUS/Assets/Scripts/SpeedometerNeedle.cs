using UnityEngine;
using UnityEngine.UI;

public class SpeedometerNeedle : MonoBehaviour
{
    [Header("References")]
    public Rigidbody carRigidbody;

    [Header("Settings")]
    public float maxMPH = 200f;
    public float minAngle = 120f;   // Angle at 0 MPH
    public float maxAngle = -120f;  // Angle at max MPH

    void Update()
    {
        if (carRigidbody == null) return;

        float metersPerSecond = carRigidbody.linearVelocity.magnitude;
        float mph = Mathf.Clamp(metersPerSecond * 2.237f, 0f, maxMPH);

        // Map speed to angle
        float t = mph / maxMPH;
        float angle = Mathf.Lerp(minAngle, maxAngle, t);

        transform.localEulerAngles = new Vector3(0f, 0f, angle);
    }
}