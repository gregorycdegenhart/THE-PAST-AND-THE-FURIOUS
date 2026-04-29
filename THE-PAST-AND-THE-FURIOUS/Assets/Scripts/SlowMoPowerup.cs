using System.Collections;
using UnityEngine;

public class SlowMoPowerup : MonoBehaviour
{
    [Header("Slow Motion Settings")]
    public float slowMultiplier = 0.3f;
    public float duration = 3f;

    private bool active = false;
    private Collider triggerCollider;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") && !other.CompareTag("Opponent"))
            return;

        if (active)
            return;

        active = true;

        GameObject picker = other.attachedRigidbody != null
            ? other.attachedRigidbody.gameObject
            : other.gameObject;

        if (triggerCollider != null)
            triggerCollider.enabled = false;

        StartCoroutine(ApplySlowMo(picker));
    }

    private IEnumerator ApplySlowMo(GameObject picker)
    {
        CarController[] players =
            Object.FindObjectsByType<CarController>(FindObjectsSortMode.None);

        AICarController[] aiCars =
            Object.FindObjectsByType<AICarController>(FindObjectsSortMode.None);

        float endTime = Time.realtimeSinceStartup + duration;

        // Keep forcing the slow effect for the full duration
        while (Time.realtimeSinceStartup < endTime)
        {
            foreach (CarController car in players)
            {
                if (car == null || car.gameObject == picker)
                    continue;

                car.SetSpeedMultiplier(slowMultiplier);
            }

            foreach (AICarController ai in aiCars)
            {
                if (ai == null || ai.gameObject == picker)
                    continue;

                ai.SetSpeedMultiplier(slowMultiplier);
            }

            yield return null;
        }

        // Restore everyone except the picker
        foreach (CarController car in players)
        {
            if (car == null || car.gameObject == picker)
                continue;

            car.SetSpeedMultiplier(1f);
        }

        foreach (AICarController ai in aiCars)
        {
            if (ai == null || ai.gameObject == picker)
                continue;

            ai.SetSpeedMultiplier(1f);
        }

        // Now it is safe to deactivate the powerup
        gameObject.SetActive(false);
    }
}