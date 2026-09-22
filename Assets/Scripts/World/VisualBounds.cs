using UnityEngine;

// Measures a model that was just instantiated. Renderer.bounds is still empty in that frame,
// and a Terrain has no renderer at all, so the size comes from the meshes and terrain data.
public static class VisualBounds
{
    public static bool TryMeasure(Transform space, GameObject target, out Bounds bounds)
    {
        bounds = default;
        bool measured = false;

        foreach (Terrain terrain in target.GetComponentsInChildren<Terrain>())
        {
            if (terrain.terrainData == null) continue;
            // A terrain grows from its transform toward positive X and Z.
            Encapsulate(space, terrain.transform, terrain.terrainData.bounds, ref bounds, ref measured);
        }

        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>())
        {
            Bounds local = renderer.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null
                ? filter.sharedMesh.bounds
                : renderer.localBounds;
            if (local.size == Vector3.zero) continue;
            Encapsulate(space, renderer.transform, local, ref bounds, ref measured);
        }

        return measured;
    }

    private static void Encapsulate(Transform space, Transform owner, Bounds local, ref Bounds bounds, ref bool measured)
    {
        Matrix4x4 toSpace = space.worldToLocalMatrix * owner.localToWorldMatrix;
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 direction = new((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
            Vector3 point = toSpace.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, direction));
            if (measured)
            {
                bounds.Encapsulate(point);
            }
            else
            {
                bounds = new Bounds(point, Vector3.zero);
                measured = true;
            }
        }
    }
}
