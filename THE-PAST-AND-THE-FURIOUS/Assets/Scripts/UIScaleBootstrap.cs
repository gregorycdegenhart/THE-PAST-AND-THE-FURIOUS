using UnityEngine;
using UnityEngine.UI;

public static class UIScaleBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void NormalizeCanvasScalers()
    {
        var scalers = Object.FindObjectsByType<CanvasScaler>(FindObjectsSortMode.None);
        foreach (var s in scalers)
        {
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920f, 1080f);
            s.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            s.matchWidthOrHeight = 0.5f;
        }
    }
}
