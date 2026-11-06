using UnityEngine;

public sealed class EnemyVisualScale : MonoBehaviour
{
    [Min(0.1f)] public float VisualHeight = 0.75f;
    [Min(0.1f)] public float VisualWidth = 0.65f;
    [SerializeField, HideInInspector] private Transform modelRoot;
    [SerializeField, HideInInspector] private Vector3 sourceBoundsSize;
    [SerializeField, HideInInspector] private Vector3 sourceBoundsCenter;
    public Transform ModelRoot => modelRoot;
    public float OriginalHeight => sourceBoundsSize.y;
    public float OriginalWidth => Mathf.Max(sourceBoundsSize.x, sourceBoundsSize.z);

    public void Configure(Transform target, Bounds sourceBounds, float height, float width)
    {
        modelRoot = target;
        sourceBoundsSize = sourceBounds.size;
        sourceBoundsCenter = sourceBounds.center;
        VisualHeight = height;
        VisualWidth = width;
        ApplyScale();
    }

    public void ApplyScale()
    {
        if (Application.isPlaying || modelRoot == null || sourceBoundsSize.y <= 0.001f) return;
        float horizontalSize = Mathf.Max(sourceBoundsSize.x, sourceBoundsSize.z);
        if (horizontalSize <= 0.001f) return;

        float horizontalScale = VisualWidth / horizontalSize;
        float verticalScale = VisualHeight / sourceBoundsSize.y;
        modelRoot.localScale = new Vector3(horizontalScale, verticalScale, horizontalScale);
        modelRoot.localPosition = new Vector3(
            -sourceBoundsCenter.x * horizontalScale,
            -(sourceBoundsCenter.y - sourceBoundsSize.y * 0.5f) * verticalScale,
            -sourceBoundsCenter.z * horizontalScale);

        BoxCollider collider = GetComponent<BoxCollider>();
        if (collider != null)
        {
            collider.center = new Vector3(0f, VisualHeight * 0.5f, 0f);
            collider.size = new Vector3(VisualWidth, VisualHeight, VisualWidth);
        }
    }

    public void ResetToOriginalModelSize()
    {
        if (OriginalHeight <= 0.001f || OriginalWidth <= 0.001f) return;
        VisualHeight = OriginalHeight;
        VisualWidth = OriginalWidth;
        ApplyScale();
    }

    private void OnValidate()
    {
        VisualHeight = Mathf.Max(0.1f, VisualHeight);
        VisualWidth = Mathf.Max(0.1f, VisualWidth);
        ApplyScale();
    }
}
