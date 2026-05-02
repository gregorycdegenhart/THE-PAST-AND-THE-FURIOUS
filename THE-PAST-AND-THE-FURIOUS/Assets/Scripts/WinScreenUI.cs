using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class WinScreenUI : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI finalTimeText;

    [Tooltip("Optional: shows the player's finishing position (1st, 2nd, ...). Wire this up if you want podium-style text.")]
    public TextMeshProUGUI placementText;

    [Header("Podium Colors")]
    public Color goldColor = new Color(1f, 0.84f, 0.2f);
    public Color silverColor = new Color(0.85f, 0.85f, 0.9f);
    public Color bronzeColor = new Color(0.8f, 0.5f, 0.25f);
    public Color defaultPlacementColor = Color.white;

    void Start()
    {
        float raceTime = PlayerPrefs.GetFloat("RaceTime", 0f);

        int minutes = (int)(raceTime / 60f);
        int seconds = (int)(raceTime % 60f);
        int centiseconds = (int)((raceTime * 100f) % 100f);

        if (finalTimeText != null)
            finalTimeText.text = string.Format("Time: {0:00}:{1:00}.{2:00}", minutes, seconds, centiseconds);

        if (placementText != null)
        {
            int pos = PlayerPrefs.GetInt("FinalPosition", 1);
            placementText.text = pos + PositionSuffix(pos);
            placementText.color = pos switch
            {
                1 => goldColor,
                2 => silverColor,
                3 => bronzeColor,
                _ => defaultPlacementColor,
            };
        }

        // Self-wire buttons
        WireButton("MenuButton", () => SceneManager.LoadScene("MainMenu"));
        WireButton("ReplayButton", () => SceneManager.LoadScene("Garage"));
    }

    static string PositionSuffix(int n)
    {
        int mod100 = n % 100;
        if (mod100 >= 11 && mod100 <= 13) return "th";
        switch (n % 10)
        {
            case 1: return "st";
            case 2: return "nd";
            case 3: return "rd";
            default: return "th";
        }
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
                    btn.onClick.AddListener(action);
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
