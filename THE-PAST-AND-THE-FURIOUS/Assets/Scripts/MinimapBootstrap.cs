using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Auto-wires the minimap on every race scene so the team doesn't have to manually configure
/// each map's MinimapCamera + RenderTexture + RawImage chain. Runs after every scene load and:
///   1. Finds (or creates) a MinimapCamera GameObject.
///   2. Loads the shared MinimapRT RenderTexture from Resources and assigns it as the camera's
///      targetTexture (and to the UI RawImage's texture).
///   3. Sets MinimapCamera.target to the player tagged "Player".
///   4. If no minimap UI element exists in the scene, creates a basic square RawImage in the
///      bottom-right corner so the map at least shows up — replace with MinimapMask.prefab in
///      the editor for the proper circular mask.
/// </summary>
public static class MinimapBootstrap
{
    const string MinimapRTPath = "MinimapRT";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Only race scenes need a minimap. Gate on scene name so we don't depend on Map 2 / Map 3
        // already having their AI grid wired (which is exactly what we're working around).
        if (!scene.name.StartsWith("Map")) return;

        RenderTexture rt = Resources.Load<RenderTexture>(MinimapRTPath);
        if (rt == null)
        {
            Debug.LogWarning("[MinimapBootstrap] Resources/MinimapRT.renderTexture not found — minimap won't render.");
            return;
        }

        MinimapCamera mc = EnsureMinimapCamera(rt);
        EnsureMinimapUI(rt);

        Debug.Log($"[MinimapBootstrap] Wired minimap on '{scene.name}'. Camera target={(mc != null && mc.target != null ? mc.target.name : "<none>")}, RT={rt.name}");
    }

    static MinimapCamera EnsureMinimapCamera(RenderTexture rt)
    {
        MinimapCamera mc = Object.FindFirstObjectByType<MinimapCamera>();

        if (mc == null)
        {
            GameObject go = new GameObject("MinimapCamera (auto)");
            Camera cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            mc = go.AddComponent<MinimapCamera>();
        }

        Camera mCam = mc.GetComponent<Camera>();
        if (mCam == null) mCam = mc.gameObject.AddComponent<Camera>();

        // Always force the targetTexture to our shared RT — fixes scenes where the Camera was
        // placed but never had targetTexture assigned (the "blank black circle" case).
        mCam.targetTexture = rt;

        // Wire the player target if it's missing.
        if (mc.target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) mc.target = player.transform;
        }

        return mc;
    }

    static void EnsureMinimapUI(RenderTexture rt)
    {
        // Look for an existing minimap RawImage by name. Map 1 has MinimapMask in the scene.
        RawImage existing = null;
        foreach (var ri in Object.FindObjectsByType<RawImage>(FindObjectsSortMode.None))
        {
            if (ri == null) continue;
            string n = ri.name.ToLower();
            string parentN = ri.transform.parent != null ? ri.transform.parent.name.ToLower() : "";
            if (n.Contains("minimap") || parentN.Contains("minimap"))
            {
                existing = ri;
                break;
            }
        }

        if (existing != null)
        {
            // Force the texture in case Map 2's RawImage was wired to a stale RT.
            existing.texture = rt;
            return;
        }

        // No minimap UI — create a basic square one as a fallback so Map 3 at least shows
        // the rendered map. The team can drop MinimapMask.prefab in later for the proper circle.
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) return; // nothing to parent to

        GameObject go = new GameObject("Minimap (auto)");
        go.transform.SetParent(canvas.transform, worldPositionStays: false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-30f, 30f);
        rect.sizeDelta = new Vector2(220f, 220f);

        RawImage img = go.AddComponent<RawImage>();
        img.texture = rt;
    }
}
