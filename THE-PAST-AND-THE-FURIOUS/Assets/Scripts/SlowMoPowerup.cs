using System.Collections;
using UnityEngine;

public class SlowMoPowerup : MonoBehaviour
{
    public float slowMultiplier = 0.3f;
    public float duration = 3f;

    private bool active = false;

    private void OnTriggerEnter(Collider other)
    {
        // detect any car (player OR AI)
        if (!other.CompareTag("Player") && !other.CompareTag("Opponent"))
            return;

        if (active) return;
        active = true;

        GameObject picker = other.gameObject;

        StartCoroutine(ApplySlowMo(picker));

        // hide or disable pickup
        gameObject.SetActive(false);
    }

    IEnumerator ApplySlowMo(GameObject picker)
    {
        // find all racers
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        GameObject[] opponents = GameObject.FindGameObjectsWithTag("Opponent");

        // combine both into one list
        foreach (GameObject obj in players)
        {
            if (obj == picker) continue;

            // PLAYER CONTROLLER (if affected)
            CarController car = obj.GetComponent<CarController>();
            if (car != null)
                car.SetSpeedMultiplier(slowMultiplier);
        }

        foreach (GameObject obj in opponents)
        {
            if (obj == picker) continue;

            // AI CONTROLLER
            // AICarController ai = obj.GetComponent<AICarController>();
            // if (ai != null)
            //     ai.SetSpeedMultiplier(slowMultiplier);
        }

        yield return new WaitForSeconds(duration);

        // reset everyone back to normal
        foreach (GameObject obj in players)
        {
            if (obj == picker) continue;

            // CarController car = obj.GetComponent<CarController>();
            // if (car != null)
            //     car.SetSpeedMultiplier(1f);
        }

        foreach (GameObject obj in opponents)
        {
            if (obj == picker) continue;

            // AICarController ai = obj.GetComponent<AICarController>();
            // if (ai != null)
            //     ai.SetSpeedMultiplier(1f);
        }
    }
}