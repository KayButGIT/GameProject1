using UnityEngine;

public sealed class Bomb : MonoBehaviour
{
    public Vector2Int Cell;

    private Collider bombCollider;
    private Transform owner;
    private float ownerReleaseDistance;

    public void Initialize(Vector2Int cell, Transform ownerTransform, float releaseDistance)
    {
        Cell = cell;
        owner = ownerTransform;
        ownerReleaseDistance = releaseDistance;
        bombCollider = GetComponent<Collider>();

        if (bombCollider != null && owner != null)
        {
            bombCollider.enabled = false;
        }
    }

    private void Update()
    {
        if (bombCollider == null || bombCollider.enabled)
        {
            return;
        }

        if (owner == null || PlanarDistance(transform.position, owner.position) >= ownerReleaseDistance)
        {
            bombCollider.enabled = true;
            owner = null;
        }
    }

    private static float PlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
