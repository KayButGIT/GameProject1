using UnityEngine;

public sealed class Bomb : MonoBehaviour
{
    public Vector2Int Cell;

    private Collider[] bombColliders;
    private Transform owner;
    private float ownerReleaseDistance;
    private bool blocksMovement;

    public bool BlocksMovement => blocksMovement;

    public void Initialize(Vector2Int cell, Transform ownerTransform, float releaseDistance)
    {
        Cell = cell;
        owner = ownerTransform;
        ownerReleaseDistance = releaseDistance;
        bombColliders = GetComponentsInChildren<Collider>();

        foreach (Collider bombCollider in bombColliders)
        {
            bombCollider.enabled = false;
        }
    }

    private void Update()
    {
        if (blocksMovement)
        {
            return;
        }

        if (owner == null || PlanarDistance(transform.position, owner.position) >= ownerReleaseDistance)
        {
            blocksMovement = true;
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
