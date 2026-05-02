using UnityEngine;

public class CarBrakelights : MonoBehaviour
{
    public Light[] brakelights;

    void Update()
    {
        bool braking = Input.GetKey(KeyCode.S);
        foreach (var light in brakelights)
            light.enabled = braking;
    }
}