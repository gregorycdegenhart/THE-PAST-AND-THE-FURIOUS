using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// EventSystem-bypass UI driver for the pause menu and any sibling UI shown alongside
/// it (notably the music playlist widget that lives inside PausePanel in Map1/2/3).
///
/// In this project, the EventSystem stops dispatching pointer events while the game
/// is paused (Time.timeScale = 0). We've ruled out the obvious causes — input update
/// mode, CanvasGroup blocksRaycasts, GraphicRaycaster missing/disabled, canvas scale,
/// raycast targets, Button.interactable — and the underlying issue is still moving.
/// So instead of chasing it, this script bypasses the EventSystem entirely:
///
///   - Builds its own list of every Selectable (Buttons, Slider) under the visible
///     panel each time the panel is shown.
///   - In Update, hit-tests the mouse against each Selectable's rect and dispatches
///     the standard pointer events (Enter/Exit/Down/Up/Click) via ExecuteEvents.
///     This drives the Button's built-in Color Tint transitions (so hover highlights
///     properly) and triggers any onClick listeners that other scripts wired up
///     (MusicPlaylistUI's Prev/PlayPause/Next, etc.).
///   - Sliders also get drag handling so the volume slider works end-to-end.
///
/// As long as MonoBehaviour.Update keeps running (which it does at timeScale=0),
/// the menu and the music widget keep working regardless of what's broken with the
/// EventSystem.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject pausePanel;
    public GameObject howToPlayPanel;

    private bool isPaused = false;
    private CursorLockMode previousLockMode;
    private bool previousCursorVisible;
    private bool eventSystemWasEnabled = true;

    // Cached Selectables under whatever panel is currently visible. Rebuilt on show.
    private readonly List<Selectable> activeSelectables = new List<Selectable>();
    private Selectable hovered;
    private Selectable pressedDown;
    private Slider draggingSlider;
    private PointerEventData pointerData;
    private Camera uiCamera;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void EnsureInputSystemRunsWhilePaused()
    {
        if (InputSystem.settings != null &&
            InputSystem.settings.updateMode != InputSettings.UpdateMode.ProcessEventsInDynamicUpdate)
        {
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
        }
    }

    void Start()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (howToPlayPanel != null) howToPlayPanel.SetActive(false);
        WirePauseButtons();
    }

    void Update()
    {
        // Pause toggle.
        bool pausePressed = false;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame ||
                Keyboard.current.pKey.wasPressedThisFrame)
                pausePressed = true;
        }
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
            pausePressed = true;

        if (pausePressed)
        {
            if (howToPlayPanel != null && howToPlayPanel.activeSelf)
                CloseHowToPlay();
            else
                TogglePause();
        }

        bool anyPanelOpen = (pausePanel != null && pausePanel.activeSelf) ||
                            (howToPlayPanel != null && howToPlayPanel.activeSelf);
        if (anyPanelOpen)
            DriveDirectInput();
    }

    public void TogglePause()
    {
        if (RaceManager.Instance != null && RaceManager.Instance.IsRaceFinished)
            return;

        isPaused = !isPaused;

        if (isPaused)
        {
            previousLockMode = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = previousLockMode;
            Cursor.visible = previousCursorVisible;
        }

        if (pausePanel != null)
        {
            pausePanel.SetActive(isPaused);
            if (isPaused) RebuildSelectables(pausePanel);
            else ClearActiveState();
        }

        // Disable the EventSystem while paused so it doesn't dispatch clicks alongside
        // our direct-input handling. Otherwise every Next/Prev tap fires twice — once
        // via EventSystem, once via DriveDirectInput — causing the music widget to
        // skip multiple tracks per click.
        if (EventSystem.current != null)
        {
            if (isPaused)
            {
                eventSystemWasEnabled = EventSystem.current.enabled;
                EventSystem.current.enabled = false;
            }
            else
            {
                EventSystem.current.enabled = eventSystemWasEnabled;
            }
        }

        Time.timeScale = isPaused ? 0f : 1f;
        AudioListener.pause = isPaused;
    }

    public void OnResumeButton() { if (isPaused) TogglePause(); }

    public void OnHowToPlayButton()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (howToPlayPanel != null)
        {
            howToPlayPanel.SetActive(true);
            RebuildSelectables(howToPlayPanel);
        }
    }

    public void CloseHowToPlay()
    {
        if (howToPlayPanel != null) howToPlayPanel.SetActive(false);
        if (pausePanel != null && isPaused)
        {
            pausePanel.SetActive(true);
            RebuildSelectables(pausePanel);
        }
    }

    public void OnRestartButton()
    {
        Unpause();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnQuitToMenuButton()
    {
        Unpause();
        SceneManager.LoadScene("MainMenu");
    }

    void Unpause()
    {
        isPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        ClearActiveState();
        if (EventSystem.current != null)
            EventSystem.current.enabled = eventSystemWasEnabled;
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    /// <summary>
    /// Wires the pause panel's Buttons to our handlers if they don't already have
    /// onClick listeners. Run once at Start so the buttons stay wired across pauses.
    /// </summary>
    void WirePauseButtons()
    {
        if (pausePanel != null)
        {
            WireButton(pausePanel, "ResumeButton", OnResumeButton);
            WireButton(pausePanel, "HTPPauseButton", OnHowToPlayButton);
            WireButton(pausePanel, "RestartButton", OnRestartButton);
            WireButton(pausePanel, "QuitButton", OnQuitToMenuButton);
        }
        if (howToPlayPanel != null)
            WireButton(howToPlayPanel, "CloseHTPButton", CloseHowToPlay);
    }

    static void WireButton(GameObject parent, string name, UnityEngine.Events.UnityAction action)
    {
        Transform t = FindDeep(parent.transform, name);
        if (t == null) return;
        Button btn = t.GetComponent<Button>();
        if (btn == null) return;
        if (btn.onClick.GetPersistentEventCount() > 0) return;
        btn.onClick.RemoveListener(action);
        btn.onClick.AddListener(action);
    }

    /// <summary>
    /// Collect every active Selectable (Button, Slider, Toggle, etc.) under `panel`,
    /// PLUS the persistent music widget (which lives on its own DontDestroyOnLoad
    /// Canvas under MusicManager). We drive all of them so the pause-menu buttons
    /// and the music widget's prev/play/next/slider all respond while paused —
    /// EventSystem is disabled during pause, so without this our direct-input is
    /// the only path.
    /// </summary>
    void RebuildSelectables(GameObject panel)
    {
        ClearActiveState();
        activeSelectables.Clear();
        if (panel != null)
            panel.GetComponentsInChildren<Selectable>(true, activeSelectables);

        // Add persistent music widget's Selectables (separate Canvas, separate hierarchy).
        if (MusicManager.Instance != null)
        {
            var widgetUI = MusicManager.Instance.GetComponentInChildren<MusicPlaylistUI>(true);
            if (widgetUI != null)
            {
                var widgetSelectables = new List<Selectable>();
                widgetUI.GetComponentsInChildren<Selectable>(true, widgetSelectables);
                activeSelectables.AddRange(widgetSelectables);
            }
        }

        // Cache the camera the panel's canvas uses for hit-testing.
        // Both the pause panel canvas and the music widget canvas are Screen Space -
        // Overlay in this project, so a single null camera handles both correctly.
        var canvas = panel != null ? panel.GetComponentInParent<Canvas>() : null;
        uiCamera = (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            ? null
            : (canvas.worldCamera != null ? canvas.worldCamera : Camera.main);
    }

    void ClearActiveState()
    {
        if (hovered != null)
            ExecuteEvents.Execute(hovered.gameObject, GetPointerData(), ExecuteEvents.pointerExitHandler);
        hovered = null;
        pressedDown = null;
        draggingSlider = null;
    }

    PointerEventData GetPointerData()
    {
        if (pointerData == null)
            pointerData = new PointerEventData(EventSystem.current);
        pointerData.position = GetMouseScreenPosition();
        return pointerData;
    }

    /// <summary>
    /// Per-frame: hit-test the mouse, dispatch the standard pointer events to
    /// whichever Selectable is under it. Also drives slider drag if needed.
    /// </summary>
    void DriveDirectInput()
    {
        Vector2 mousePos = GetMouseScreenPosition();
        var data = GetPointerData();
        data.position = mousePos;

        // Find the topmost Selectable under the mouse (last one in iteration order
        // that contains the point — siblings rendered later are on top in Unity UI).
        Selectable underMouse = null;
        for (int i = 0; i < activeSelectables.Count; i++)
        {
            var s = activeSelectables[i];
            if (s == null || !s.gameObject.activeInHierarchy || !s.IsInteractable()) continue;
            var rt = s.transform as RectTransform;
            if (rt == null) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(rt, mousePos, uiCamera))
                underMouse = s;
        }

        // Hover transitions.
        if (underMouse != hovered)
        {
            if (hovered != null)
                ExecuteEvents.Execute(hovered.gameObject, data, ExecuteEvents.pointerExitHandler);
            if (underMouse != null)
                ExecuteEvents.Execute(underMouse.gameObject, data, ExecuteEvents.pointerEnterHandler);
            hovered = underMouse;
        }

        // Mouse-down: arm pressedDown / start slider drag.
        if (WasMouseDownThisFrame() && underMouse != null)
        {
            pressedDown = underMouse;
            ExecuteEvents.Execute(underMouse.gameObject, data, ExecuteEvents.pointerDownHandler);

            if (underMouse is Slider sl)
            {
                draggingSlider = sl;
                UpdateSliderFromMouse(sl, mousePos);
            }
        }

        // Slider drag while held.
        if (draggingSlider != null && IsMouseHeld())
        {
            UpdateSliderFromMouse(draggingSlider, mousePos);
        }

        // Mouse-up: fire click on whatever was pressed if we're still over it.
        if (WasMouseUpThisFrame())
        {
            if (pressedDown != null)
                ExecuteEvents.Execute(pressedDown.gameObject, data, ExecuteEvents.pointerUpHandler);

            if (pressedDown != null && pressedDown == underMouse)
                ExecuteEvents.Execute(pressedDown.gameObject, data, ExecuteEvents.pointerClickHandler);

            pressedDown = null;
            draggingSlider = null;
        }
    }

    static void UpdateSliderFromMouse(Slider slider, Vector2 mousePos)
    {
        if (slider == null) return;
        var rt = slider.transform as RectTransform;
        if (rt == null) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, mousePos, null, out Vector2 local))
            return;

        // Convert local point (relative to pivot) into a 0-1 fraction along the slider's axis.
        Rect rect = rt.rect;
        float frac;
        if (slider.direction == Slider.Direction.LeftToRight)
            frac = Mathf.InverseLerp(rect.xMin, rect.xMax, local.x);
        else if (slider.direction == Slider.Direction.RightToLeft)
            frac = Mathf.InverseLerp(rect.xMax, rect.xMin, local.x);
        else if (slider.direction == Slider.Direction.BottomToTop)
            frac = Mathf.InverseLerp(rect.yMin, rect.yMax, local.y);
        else
            frac = Mathf.InverseLerp(rect.yMax, rect.yMin, local.y);

        slider.value = Mathf.Lerp(slider.minValue, slider.maxValue, Mathf.Clamp01(frac));
    }

    static Vector2 GetMouseScreenPosition()
    {
        if (Mouse.current != null) return Mouse.current.position.ReadValue();
        return Input.mousePosition;
    }

    static bool WasMouseDownThisFrame()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
        return Input.GetMouseButtonDown(0);
    }

    static bool WasMouseUpThisFrame()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame) return true;
        return Input.GetMouseButtonUp(0);
    }

    static bool IsMouseHeld()
    {
        if (Mouse.current != null) return Mouse.current.leftButton.isPressed;
        return Input.GetMouseButton(0);
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
