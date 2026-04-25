using UnityEngine;
using TMPro;

public class Speedometer : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI speedText;  // Drag your TMP text here in Inspector
    public Rigidbody carRigidbody;     // Drag your car here in Inspector

    [Header("Settings")]
    public string displayFormat = "{0} MPH";
    public float maxDisplayMPH = 200f;

    void Update()
    {
        // Get speed in Unity units/sec, convert to MPH
        float metersPerSecond = carRigidbody.linearVelocity.magnitude;
        float mph = Mathf.Clamp(metersPerSecond * 2.237f, 0f, maxDisplayMPH);

        // Round it and display it
        speedText.text = string.Format(displayFormat, Mathf.RoundToInt(mph));
    }
}