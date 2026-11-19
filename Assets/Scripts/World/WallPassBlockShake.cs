using UnityEngine;

public sealed class WallPassBlockShake : MonoBehaviour
{
    private const float Amplitude = 0.015f;
    private const float Frequency = 14f;

    private Transform visual;
    private Vector3 originalLocalPosition;
    private Bounds stableVisualBounds;
    private Material debrisMaterial;
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
            if (debrisMaterial == null) debrisMaterial = renderer.sharedMaterial;
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
        if (isShaking)
            EmitDebris();
        else
            RestoreVisualPosition();
    }

    private void EmitDebris()
    {
        if (!hasStableVisualBounds || debrisMaterial == null) return;

        GameObject debrisRoot = new("Wall Pass Block Debris");
        debrisRoot.transform.position = stableVisualBounds.center
            + Vector3.up * (stableVisualBounds.extents.y + 0.06f);

        const int debrisCount = 8;
        for (int i = 0; i < debrisCount; i++)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = "Wall Pass Debris Piece";
            piece.transform.SetParent(debrisRoot.transform, false);
            piece.transform.localPosition = new Vector3(
                Random.Range(-stableVisualBounds.extents.x * 0.85f, stableVisualBounds.extents.x * 0.85f),
                Random.Range(0.02f, 0.2f),
                Random.Range(-stableVisualBounds.extents.z * 0.85f, stableVisualBounds.extents.z * 0.85f));

            float size = Random.Range(0.07f, 0.13f);
            piece.transform.localScale = new Vector3(size, size * Random.Range(0.7f, 1.3f), size);
            piece.transform.localRotation = Random.rotation;
            piece.GetComponent<Renderer>().sharedMaterial = debrisMaterial;
            Object.Destroy(piece.GetComponent<Collider>());

            Vector3 velocity = new(
                Random.Range(-0.35f, 0.35f),
                Random.Range(0.35f, 0.8f),
                Random.Range(-0.35f, 0.35f));
            Vector3 spin = Random.insideUnitSphere * Random.Range(120f, 260f);
            piece.AddComponent<ExplosionParticlePiece>().Initialize(
                velocity,
                spin,
                Random.Range(0.35f, 0.65f));
        }

        Object.Destroy(debrisRoot, 0.8f);
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
