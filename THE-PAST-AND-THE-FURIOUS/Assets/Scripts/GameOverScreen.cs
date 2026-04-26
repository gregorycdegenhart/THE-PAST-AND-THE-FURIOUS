using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Game over panel — same EventSystem-bypass approach as PauseMenu, since this
/// also runs while Time.timeScale = 0 and inherits the same dead-UI bug.
/// </summary>
public class GameOverScreen : MonoBehaviour
{
    [Header("UI")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI reasonText;

    private readonly List<Selectable> activeSelectables = new List<Selectable>();
    private Selectable hovered;
    private Selectable pressedDown;
    private PointerEventData pointerData;
    private Camera uiCamera;
    private bool eventSystemWasEnabled = true;

    void Start()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
            WireButton(gameOverPanel, "RetryButton", OnRetryButton);
            WireButton(gameOverPanel, "GOQuitButton", OnQuitToMenuButton);
        }
    }

    void Update()
    {
        if (gameOverPanel != null && gameOverPanel.activeSelf)
            DriveDirectInput();
    }

    public void ShowGameOver(string reason = "Race Over!")
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            WireButton(gameOverPanel, "RetryButton", OnRetryButton);
            WireButton(gameOverPanel, "GOQuitButton", OnQuitToMenuButton);
            RebuildSelectables(gameOverPanel);
        }
        if (reasonText != null)
            reasonText.text = reason;

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Same fix as PauseMenu — EventSystem dispatch is unreliable at timeScale = 0
        // in this project. Disable it so our direct-input drive is the sole source of
        // pointer events. Restored when the scene reloads via Retry/Quit.
        if (EventSystem.current != null)
        {
            eventSystemWasEnabled = EventSystem.current.enabled;
            EventSystem.current.enabled = false;
        }
    }

    public void OnRetryButton()
    {
        RestoreOnExit();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnQuitToMenuButton()
    {
        RestoreOnExit();
        SceneManager.LoadScene("MainMenu");
    }

    void RestoreOnExit()
    {
        Time.timeScale = 1f;
        if (EventSystem.current != null)
            EventSystem.current.enabled = eventSystemWasEnabled;
    }

    void RebuildSelectables(GameObject panel)
    {
        activeSelectables.Clear();
        hovered = null;
        pressedDown = null;
        if (panel == null) return;
        panel.GetComponentsInChildren<Selectable>(true, activeSelectables);

        var canvas = panel.GetComponentInParent<Canvas>();
        uiCamera = (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            ? null
            : (canvas.worldCamera != null ? canvas.worldCamera : Camera.main);
    }

    PointerEventData GetPointerData()
    {
        if (pointerData == null)
            pointerData = new PointerEventData(EventSystem.current);
        pointerData.position = GetMouseScreenPosition();
        return pointerData;
    }

    void DriveDirectInput()
    {
        Vector2 mousePos = GetMouseScreenPosition();
        var data = GetPointerData();
        data.position = mousePos;

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

        if (underMouse != hovered)
        {
            if (hovered != null)
                ExecuteEvents.Execute(hovered.gameObject, data, ExecuteEvents.pointerExitHandler);
            if (underMouse != null)
                ExecuteEvents.Execute(underMouse.gameObject, data, ExecuteEvents.pointerEnterHandler);
            hovered = underMouse;
        }

        if (WasMouseDownThisFrame() && underMouse != null)
        {
            pressedDown = underMouse;
            ExecuteEvents.Execute(underMouse.gameObject, data, ExecuteEvents.pointerDownHandler);
        }

        if (WasMouseUpThisFrame())
        {
            if (pressedDown != null)
                ExecuteEvents.Execute(pressedDown.gameObject, data, ExecuteEvents.pointerUpHandler);

            if (pressedDown != null && pressedDown == underMouse)
                ExecuteEvents.Execute(pressedDown.gameObject, data, ExecuteEvents.pointerClickHandler);

            pressedDown = null;
        }
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

    static void WireButton(GameObject parent, string name, UnityEngine.Events.UnityAction action)
    {
        Transform t = FindDeep(parent.transform, name);
        if (t == null)
        {
            Debug.LogWarning($"[GameOverScreen] Button '{name}' not found under '{parent.name}'.");
            return;
        }
        Button btn = t.GetComponent<Button>();
        if (btn == null) return;
        if (btn.onClick.GetPersistentEventCount() > 0) return;
        btn.onClick.RemoveListener(action);
        btn.onClick.AddListener(action);
    }
}
