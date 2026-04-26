using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    public HowToPlayScreen howToPlayScreen;

    void Start()
    {
        WireButton("PlayButton", () => SceneManager.LoadScene("Garage"));
        WireButton("QuitButton", () =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });

        // Wire How To Play button
        if (howToPlayScreen != null)
            WireButton("HowToPlayButton", () => howToPlayScreen.Show());
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
        Debug.LogWarning($"[MainMenuUI] Button '{name}' not found in any Canvas.");
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
