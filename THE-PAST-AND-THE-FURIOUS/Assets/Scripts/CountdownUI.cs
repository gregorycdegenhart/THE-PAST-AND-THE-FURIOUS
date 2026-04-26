using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class CountdownUI : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI countdownText;
    public PlayerInput playerInput;

    public static bool RaceStarted { get; private set; }

    void Start()
    {
        RaceStarted = false;

        if (countdownText != null)
            countdownText.text = "";

        if (playerInput != null)
            playerInput.DeactivateInput();

        StartCoroutine(RunCountdown());
    }

    IEnumerator RunCountdown()
    {
        yield return new WaitForSecondsRealtime(0.5f);

        SetCountdownText("3");
        yield return new WaitForSecondsRealtime(1f);

        SetCountdownText("2");
        yield return new WaitForSecondsRealtime(1f);

        SetCountdownText("1");
        yield return new WaitForSecondsRealtime(1f);

        SetCountdownText("GO!");

        if (playerInput != null)
            playerInput.ActivateInput();

        RaceStarted = true;

        yield return new WaitForSecondsRealtime(0.5f);

        SetCountdownText("");
    }

    void SetCountdownText(string value)
    {
        if (countdownText != null)
            countdownText.text = value;
    }
}
