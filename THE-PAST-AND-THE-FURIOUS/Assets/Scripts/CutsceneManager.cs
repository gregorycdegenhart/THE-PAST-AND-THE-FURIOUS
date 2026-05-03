using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public class CutsceneManager : MonoBehaviour
{
    [Header("References")]
    public PlayableDirector cutsceneDirector;
    public CanvasGroup fadeGroup;
    public GameObject garageUI;

    [Header("Settings")]
    public float fadeDuration = 1f;

    private string selectedScene;

    public void SetSelectedScene(string sceneName)
    {
        selectedScene = sceneName;
    }

    public void StartCutscene()
    {
        if (string.IsNullOrEmpty(selectedScene))
        {
            Debug.LogWarning("No scene selected!");
            return;
        }
        StartCoroutine(PlayCutsceneThenLoad());
    }

    private IEnumerator PlayCutsceneThenLoad()
    {
        // Hide the UI
        if (garageUI != null)
            garageUI.SetActive(false);

        cutsceneDirector.Play();

        yield return new WaitForSeconds((float)cutsceneDirector.duration);

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            fadeGroup.alpha = Mathf.Lerp(0f, 1f, t / fadeDuration);
            yield return null;
        }

        fadeGroup.alpha = 1f;

        SceneManager.LoadScene(selectedScene);
    }
}