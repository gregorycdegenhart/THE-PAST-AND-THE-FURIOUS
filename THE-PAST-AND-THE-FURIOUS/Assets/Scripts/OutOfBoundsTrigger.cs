using UnityEngine;

public class OutOfBoundsTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<CarController>() != null)
            RespawnManager.Instance?.TriggerRespawn();
    }
}