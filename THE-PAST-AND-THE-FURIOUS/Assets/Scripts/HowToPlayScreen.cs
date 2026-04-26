using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class HowToPlayScreen : MonoBehaviour
{
    [Header("UI")]
    public GameObject howToPlayPanel;

    private bool hasPauseMenu;

    void Start()
    {
        // If a PauseMenu lives in this scene, IT owns the HTP open/close flow
        // (so closing properly restores the pause panel). Don't double-handle.
        hasPauseMenu = FindFirstObjectByType<PauseMenu>() != null;

        if (howToPlayPanel != null)
        {
            howToPlayPanel.SetActive(false);
            Transform closeBtn = FindDeep(howToPlayPanel.transform, "CloseHTPButton");
            if (closeBtn != null)
            {
                Button btn = closeBtn.GetComponent<Button>();
                if (btn != null && btn.onClick.GetPersistentEventCount() == 0)
                {
                    btn.onClick.RemoveListener(Close);
                    btn.onClick.AddListener(Close);
                }
            }
        }
    }

    void Update()
    {
        if (hasPauseMenu) return; // PauseMenu handles ESC for the in-game HTP

        if (howToPlayPanel != null && howToPlayPanel.activeSelf)
        {
            // Check both new InputSystem and legacy Input — same pattern as PauseMenu.
            // Either path closes the panel; we want this to work regardless of which
            // input mode is active in the project's settings.
            bool escPressed = false;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                escPressed = true;
            if (Input.GetKeyDown(KeyCode.Escape))
                escPressed = true;

            if (escPressed)
                Close();
        }
    }

    public void Show()
    {
        if (howToPlayPanel != null)
            howToPlayPanel.SetActive(true);
    }

    public void Close()
    {
        if (howToPlayPanel != null)
            howToPlayPanel.SetActive(false);
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
