using UnityEngine;

public sealed class WallPassBlockShake : MonoBehaviour
{
    private const float Amplitude = 0.015f;
    private const float Frequency = 14f;

    private Transform visual;
    private Vector3 originalLocalPosition;
    private Bounds stableVisualBounds;
    private bool hasStableVisualBounds;
    private bool isShaking;

    public bool IsShaking => isShaking;

    public void Initialize(Transform targetVisual)
    {
        visual = targetVisual;
        if (visual == null) return;

        originalLocalPosition = visual.localPosition;
        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || !renderer.enabled) continue;
            if (!hasStableVisualBounds)
            {
                stableVisualBounds = renderer.bounds;
                hasStableVisualBounds = true;
            }
            else
            {
                stableVisualBounds.Encapsulate(renderer.bounds);
            }
        }
    }

    public bool TryGetStableVisualBounds(out Bounds bounds)
    {
        bounds = stableVisualBounds;
        return hasStableVisualBounds;
    }

    public void SetShaking(bool shaking)
    {
        if (isShaking == shaking) return;
        isShaking = shaking;
        if (!isShaking) RestoreVisualPosition();
    }

    private void LateUpdate()
    {
        if (!isShaking || visual == null) return;
        float phase = Time.time * Frequency * Mathf.PI * 2f;
        visual.localPosition = originalLocalPosition + new Vector3(
            Mathf.Sin(phase) * Amplitude,
            0f,
            Mathf.Sin(phase * 1.17f + 1.2f) * Amplitude);
    }

    private void OnDisable() => RestoreVisualPosition();

    private void RestoreVisualPosition()
    {
        if (visual != null) visual.localPosition = originalLocalPosition;
        isShaking = false;
    }
}
