using UnityEngine;

public sealed class ExitDoor : MonoBehaviour
{
    public Vector2Int Cell { get; private set; }
    public bool IsUnlocked { get; private set; }
    public bool IsRevealed { get; private set; }

    private Renderer doorRenderer;
    private Material unlockedMaterial;

    public static ExitDoor Create(Vector2Int cell, BombermanMap map, Material lockedMaterial, Material unlockedMaterial)
    {
        GameObject doorObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        doorObject.name = "Exit Door";
        doorObject.transform.position = map.CellToWorld(cell) + new Vector3(0f, 0.5f, 0f);
        doorObject.transform.localScale = new Vector3(0.85f, 1f, 0.85f);
        Object.Destroy(doorObject.GetComponent<Collider>());

        ExitDoor door = doorObject.AddComponent<ExitDoor>();
        door.Cell = cell;
        door.doorRenderer = doorObject.GetComponent<Renderer>();
        door.unlockedMaterial = unlockedMaterial;
        door.doorRenderer.material = lockedMaterial;

        // Hidden by default: stays under its destructible wall until Reveal() is called.
        doorObject.SetActive(false);
        return door;
    }

    public void Reveal()
    {
        if (IsRevealed)
        {
            return;
        }

        IsRevealed = true;
        gameObject.SetActive(true);
    }

    public void Unlock()
    {
        if (IsUnlocked)
        {
            return;
        }

        IsUnlocked = true;
        if (doorRenderer != null && unlockedMaterial != null)
        {
            doorRenderer.material = unlockedMaterial;
        }
    }
}