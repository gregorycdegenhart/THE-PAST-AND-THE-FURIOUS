using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuUIManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject settingsPanel;
    public GameObject controlsPanel;

    [Header("Audio Canvas")]
    public GameObject audioCanvas; // <-- renamed for clarity

    void Start()
    {
        // Main menu only at start
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (controlsPanel != null) controlsPanel.SetActive(false);
        if (audioCanvas != null) audioCanvas.SetActive(false);
    }

    // -------- SETTINGS NAV --------
    public void OpenSettings()
    {
        mainMenuPanel.SetActive(false);
        settingsPanel.SetActive(true);

        controlsPanel.SetActive(false);

        if (audioCanvas != null)
            audioCanvas.SetActive(false);
    }

    public void BackToMainMenu()
    {
        mainMenuPanel.SetActive(true);

        settingsPanel.SetActive(false);
        controlsPanel.SetActive(false);

        if (audioCanvas != null)
            audioCanvas.SetActive(false);
    }

    // -------- SUB MENUS --------
    public void OpenControls()
    {
        settingsPanel.SetActive(false);
        controlsPanel.SetActive(true);

        if (audioCanvas != null)
            audioCanvas.SetActive(false);
    }

    public void OpenAudio()
    {
        // Hide everything else in settings flow
        settingsPanel.SetActive(false);
        controlsPanel.SetActive(false);

        // Show audio canvas
        if (audioCanvas != null)
            audioCanvas.SetActive(true);
    }

    public void BackToSettings()
    {
        settingsPanel.SetActive(true);

        controlsPanel.SetActive(false);

        if (audioCanvas != null)
            audioCanvas.SetActive(false);
    }

    // -------- PLAY --------
    public void PlayGame()
    {
        SceneManager.LoadScene("Garage");
    }

    // -------- QUIT --------
    public void QuitGame()
    {
        Debug.Log("Quit Game");
        Application.Quit();
    }
}