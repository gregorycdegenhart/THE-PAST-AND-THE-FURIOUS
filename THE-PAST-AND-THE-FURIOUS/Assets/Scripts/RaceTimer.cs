using UnityEngine;
using TMPro;

public class RaceTimer : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI timerText;

    private float elapsedTime = 0f;
    private bool timing = false;
    private bool finished = false;

    void Update()
    {
        if (!timing && CountdownUI.RaceStarted)
            timing = true;

        if (timing && !finished)
        {
            if (RaceManager.Instance != null && RaceManager.Instance.IsRaceFinished)
            {
                finished = true;
                PlayerPrefs.SetFloat("RaceTime", elapsedTime);
                PlayerPrefs.SetFloat("RaceTime_" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, elapsedTime);
                PlayerPrefs.Save();
            }
            else
            {
                elapsedTime += Time.deltaTime;
            }
        }

        if (timerText != null)
            timerText.text = FormatTime(elapsedTime);
    }

    static string FormatTime(float t)
    {
        int minutes = (int)(t / 60f);
        int seconds = (int)(t % 60f);
        int centiseconds = (int)((t * 100f) % 100f);
        return string.Format("{0:00}:{1:00}.{2:00}", minutes, seconds, centiseconds);
    }
}
