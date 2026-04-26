using System.Collections;
using UnityEngine;

public class SlowMoPowerup : MonoBehaviour
{
    public float slowMultiplier = 0.3f;
    public float duration = 3f;

    private bool active = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") && !other.CompareTag("Opponent"))
            return;

        if (active) return;
        active = true;

        GameObject picker = other.attachedRigidbody != null
            ? other.attachedRigidbody.gameObject
            : other.gameObject;

        StartCoroutine(ApplySlowMo(picker));

        gameObject.SetActive(false);
    }

    IEnumerator ApplySlowMo(GameObject picker)
    {
        // Slow EVERY car except the picker (player + AI). The picker's tag is
        // "Player" or "Opponent"; the AI cars are spawned as "Untagged" by the
        // grid spawner, so we sweep every active controller of each kind.
        var players = Object.FindObjectsByType<CarController>(FindObjectsSortMode.None);
        var aiCars = Object.FindObjectsByType<AICarController>(FindObjectsSortMode.None);

        foreach (var car in players)
        {
            if (car == null || car.gameObject == picker) continue;
            car.SetSpeedMultiplier(slowMultiplier);
        }

        foreach (var ai in aiCars)
        {
            if (ai == null || ai.gameObject == picker) continue;
            ai.SetSpeedMultiplier(slowMultiplier);
        }

        yield return new WaitForSeconds(duration);

        foreach (var car in players)
        {
            if (car == null || car.gameObject == picker) continue;
            car.SetSpeedMultiplier(1f);
        }

        foreach (var ai in aiCars)
        {
            if (ai == null || ai.gameObject == picker) continue;
            ai.SetSpeedMultiplier(1f);
        }
    }
}
