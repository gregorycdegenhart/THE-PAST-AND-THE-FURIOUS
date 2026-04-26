using UnityEngine;

/// <summary>
/// Anchors this GameObject to the world position of a target wheel transform every
/// LateUpdate, while leaving this GameObject's ROTATION untouched. Used by the tire
/// track TrailRenderer setup so the trail's local axes stay world-aligned (Z up,
/// trail lies flat on the ground) even as the wheel spins and steers.
///
/// Place on a GameObject (TrailAnchor) parented under Player_BMW root with rotation
/// (-90, 0, 0). Add a TrailRenderer to the same GameObject. Drag the wheel transform
/// into `wheel`. The trail will follow that wheel without inheriting its rotation.
///
/// Also disables the per-renderer culling/optimization paths Unity applies to
/// dynamic renderers — frustum culling, occlusion culling, shadow passes — which
/// otherwise pop the trail mesh on/off as the source moves.
/// </summary>
public class FollowWheel : MonoBehaviour
{
    [Tooltip("The wheel transform whose world position this object should track.")]
    public Transform wheel;

    [Tooltip("Vertical offset from the wheel center to ground level. Wheel radius is 0.35, so -0.30 puts the trail 5cm above ground (clears terrain bumps + avoids z-fighting).")]
    public float groundOffsetY = -0.30f;

    private TrailRenderer trail;

    void Awake()
    {
        trail = GetComponent<TrailRenderer>();

        if (trail != null)
        {
            trail.allowOcclusionWhenDynamic = false;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
        }
    }

    void LateUpdate()
    {
        if (wheel != null)
            transform.position = wheel.position + Vector3.up * groundOffsetY;

        // Re-assert a large bounds every frame — TrailRenderer's auto-computed
        // bounds can frustum-cull the visible trail mesh when the source moves.
        if (trail != null)
            trail.localBounds = new Bounds(Vector3.zero, Vector3.one * 400f);
    }
}
