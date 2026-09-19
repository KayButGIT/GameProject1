using UnityEngine;

public sealed class ExitDoor : MonoBehaviour
{
    private const float PulseSpeed = 6f;
    private const float PulseAmount = 0.1f;

    private Renderer placeholder;
    private Material lockedMaterial;
    private Material openMaterial;
    private Transform visual;
    private Vector3 visualScale;
    private float pulseTime;

    public Vector2Int Cell { get; private set; }
    public bool IsRevealed { get; private set; }
    public bool IsOpen { get; private set; }

    public static ExitDoor Create(Vector2Int cell, Vector3 position, Transform parent, BombermanMaterials materials, StageTheme.VisualSlot themeVisual, System.Random random, bool revealed)
    {
        GameObject root = new($"Exit Door {cell.x},{cell.y}");
        root.transform.SetParent(parent);
        root.transform.position = position;

        GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        slab.name = "Door";
        slab.transform.SetParent(root.transform, false);
        slab.transform.localPosition = new Vector3(0f, 0.01f, 0f);
        slab.transform.localScale = new Vector3(0.78f, 0.06f, 0.78f);
        BombermanMap.DestroyGenerated(slab.GetComponent<Collider>());

        ExitDoor door = root.AddComponent<ExitDoor>();
        door.Cell = cell;
        door.lockedMaterial = materials.ExitDoorLocked;
        door.openMaterial = materials.ExitDoorOpen;
        door.placeholder = slab.GetComponent<Renderer>();
        door.placeholder.sharedMaterial = door.lockedMaterial;
        door.visual = slab.transform;
        if (themeVisual != null && themeVisual.Attach(root.transform, random))
        {
            door.placeholder.enabled = false;
            door.visual = root.transform.Find("Visual");
        }

        door.visualScale = door.visual.localScale;
        door.IsRevealed = revealed;
        root.SetActive(revealed);
        return door;
    }

    public void Reveal()
    {
        IsRevealed = true;
        gameObject.SetActive(true);
    }

    public void SetOpen(bool open)
    {
        if (IsOpen == open)
        {
            return;
        }

        IsOpen = open;
        placeholder.sharedMaterial = open ? openMaterial : lockedMaterial;
        pulseTime = 0f;
        visual.localScale = visualScale;
    }

    private void Update()
    {
        if (!IsOpen)
        {
            return;
        }

        // Scaled time, so the pulse freezes while the game is paused.
        pulseTime += Time.deltaTime;
        visual.localScale = visualScale * (1f + Mathf.Sin(pulseTime * PulseSpeed) * PulseAmount);
    }
}
