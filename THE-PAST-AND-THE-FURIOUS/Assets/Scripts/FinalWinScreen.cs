using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Reveals a results panel after the ending cutscene timeline finishes. Populates
/// per-map placement + time text from PlayerPrefs keys written by RaceManager / RaceTimer
/// (FinalPosition_<scene> and RaceTime_<scene>).
/// </summary>
public class FinalWinScreen : MonoBehaviour
{
    [Header("Cutscene")]
    public PlayableDirector director;
    [Tooltip("If true and director is set, the panel reveals when the timeline finishes. Otherwise reveal immediately.")]
    public bool waitForDirector = true;
    public float fadeInDuration = 1f;

    [Header("Canvas")]
    public CanvasGroup panelGroup;

    [Header("Per-map rows (label + placement + time)")]
    public TextMeshProUGUI map1RowText;
    public TextMeshProUGUI map2RowText;
    public TextMeshProUGUI map3RowText;

    [Header("Buttons")]
    public Button menuButton;
    public string menuSceneName = "MainMenu";

    void Awake()
    {
        if (panelGroup != null)
        {
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
        }
        if (menuButton != null)
            menuButton.onClick.AddListener(() => SceneManager.LoadScene(menuSceneName));
    }

    void Start()
    {
        if (waitForDirector && director != null)
            director.stopped += OnDirectorStopped;
        else
            StartCoroutine(RevealRoutine());
    }

    void OnDirectorStopped(PlayableDirector pd)
    {
        StartCoroutine(RevealRoutine());
    }

    IEnumerator RevealRoutine()
    {
        PopulateRows();
        if (panelGroup == null) yield break;
        panelGroup.interactable = true;
        panelGroup.blocksRaycasts = true;
        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.unscaledDeltaTime;
            panelGroup.alpha = Mathf.Clamp01(t / fadeInDuration);
            yield return null;
        }
        panelGroup.alpha = 1f;
    }

    void PopulateRows()
    {
        SetRow(map1RowText, "The Past: Aztec",     "Map1");
        SetRow(map2RowText, "The Present: I4",     "Map2");
        SetRow(map3RowText, "The Future: Neo Tokyo","Map3");
    }

    static void SetRow(TextMeshProUGUI text, string label, string mapKey)
    {
        if (text == null) return;
        bool hasPos = PlayerPrefs.HasKey("FinalPosition_" + mapKey);
        bool hasTime = PlayerPrefs.HasKey("RaceTime_" + mapKey);
        if (!hasPos && !hasTime)
        {
            text.text = $"{label}    —";
            return;
        }
        int pos = PlayerPrefs.GetInt("FinalPosition_" + mapKey, 0);
        float time = PlayerPrefs.GetFloat("RaceTime_" + mapKey, 0f);
        string posStr = pos > 0 ? $"{pos}{Suffix(pos)}" : "—";
        string timeStr = time > 0f ? FormatTime(time) : "—";
        text.text = $"{label}    {posStr}    {timeStr}";
    }

    static string Suffix(int n)
    {
        int mod100 = n % 100;
        if (mod100 >= 11 && mod100 <= 13) return "th";
        return (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
    }

    static string FormatTime(float t)
    {
        int m = (int)(t / 60f);
        int s = (int)(t % 60f);
        int cs = (int)((t * 100f) % 100f);
        return $"{m:00}:{s:00}.{cs:00}";
    }
}
