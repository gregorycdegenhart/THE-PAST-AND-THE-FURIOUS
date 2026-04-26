using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class WinScreenUI : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI finalTimeText;

    void Start()
    {
        float raceTime = PlayerPrefs.GetFloat("RaceTime", 0f);

        int minutes = (int)(raceTime / 60f);
        int seconds = (int)(raceTime % 60f);
        int centiseconds = (int)((raceTime * 100f) % 100f);

        if (finalTimeText != null)
            finalTimeText.text = string.Format("Time: {0:00}:{1:00}.{2:00}", minutes, seconds, centiseconds);

        // Self-wire buttons
        WireButton("MenuButton", () => SceneManager.LoadScene("MainMenu"));
        WireButton("ReplayButton", () => SceneManager.LoadScene("Garage"));
    }

    void WireButton(string name, UnityEngine.Events.UnityAction action)
    {
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            Transform t = FindDeep(canvas.transform, name);
            if (t != null)
            {
                Button btn = t.GetComponent<Button>();
                if (btn != null && btn.onClick.GetPersistentEventCount() == 0)
                {
                    btn.onClick.RemoveListener(action);
                    btn.onClick.AddListener(action);
                }
                return;
            }
        }
        Debug.LogWarning($"[WinScreenUI] Button '{name}' not found in any Canvas.");
    }

    static Transform FindDeep(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeep(parent.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}
