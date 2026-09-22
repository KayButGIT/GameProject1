using System.Collections.Generic;
using UnityEngine;

public sealed class BombermanMap
{
    private readonly int width;
    private readonly int height;
    private readonly float destructibleDensity;
    private readonly int randomSeed;
    private readonly BombermanMaterials materials;
    private readonly StageTheme theme;
    private readonly Transform parent;
    private System.Random layoutRandom;
    private System.Random visualRandom;
    private readonly Dictionary<Vector2Int, GameObject> blocks = new();
    private readonly Dictionary<Vector2Int, CellKind> cells = new();

    public BombermanMap(int width, int height, float destructibleDensity, int randomSeed, BombermanMaterials materials, StageTheme theme = null, Transform parent = null)
    {
        this.width = width;
        this.height = height;
        this.destructibleDensity = destructibleDensity;
        this.randomSeed = randomSeed;
        this.materials = materials;
        this.theme = theme;
        this.parent = parent;
    }

    public float LeftWorldX => CellToWorld(new Vector2Int(0, 0)).x;
    public float RightWorldX => CellToWorld(new Vector2Int(width - 1, 0)).x;
    public ExitDoor ExitDoor { get; private set; }

    public void Generate()
    {
        Random.InitState(randomSeed);
        layoutRandom = new System.Random(randomSeed);
        // Model variants use their own sequence, so they never change the layout or the exit.
        visualRandom = new System.Random(~randomSeed);
        GameObject mapRoot = new("Generated Bomberman Map");
        mapRoot.transform.SetParent(parent, false);
        cells.Clear();
        blocks.Clear();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2Int cell = new(x, y);
                CellKind kind = GetInitialCellKind(cell);
                cells[cell] = kind;

                CreateFloor(cell, mapRoot.transform);
                if (kind == CellKind.Solid)
                {
                    CreateBlock(cell, "Solid Wall", materials.Solid, 1f, mapRoot.transform, GetSolidVisual(cell));
                }
                else if (kind == CellKind.Destructible)
                {
                    CreateBlock(cell, "Destructible Wall", materials.Destructible, 0.8f, mapRoot.transform, theme != null ? theme.BreakableBlock : null);
                }
            }
        }

        ExitDoor = CreateExitDoor(mapRoot.transform);
        CreateArenaBorder(mapRoot.transform);
    }

    public CellKind GetCellKind(Vector2Int cell)
    {
        return cells.TryGetValue(cell, out CellKind kind) ? kind : CellKind.None;
    }

    public bool IsWalkable(Vector2Int cell)
    {
        return GetCellKind(cell) == CellKind.Empty;
    }

    public void DestroyDestructibleAt(Vector2Int cell)
    {
        if (GetCellKind(cell) != CellKind.Destructible)
        {
            return;
        }

        cells[cell] = CellKind.Empty;
        if (blocks.TryGetValue(cell, out GameObject block))
        {
            block.SetActive(false);
            UnityEngine.Object.Destroy(block);
            blocks.Remove(cell);
        }

        if (ExitDoor != null && ExitDoor.Cell == cell)
        {
            ExitDoor.Reveal();
        }
    }

    // The material a block is drawn with: its theme model's when one is attached.
    public Material GetBlockMaterial(Vector2Int cell)
    {
        if (!blocks.TryGetValue(cell, out GameObject block))
        {
            return null;
        }

        foreach (Renderer renderer in block.GetComponentsInChildren<Renderer>())
        {
            if (renderer.enabled && renderer.sharedMaterial != null)
            {
                return renderer.sharedMaterial;
            }
        }

        return null;
    }

    public Vector3 CellToWorld(Vector2Int cell)
    {
        return new Vector3(cell.x - width / 2f, 0f, cell.y - height / 2f);
    }

    public Vector2Int WorldToCell(Vector3 worldPosition)
    {
        int x = Mathf.RoundToInt(worldPosition.x + width / 2f);
        int y = Mathf.RoundToInt(worldPosition.z + height / 2f);
        return new Vector2Int(Mathf.Clamp(x, 0, width - 1), Mathf.Clamp(y, 0, height - 1));
    }

    private CellKind GetInitialCellKind(Vector2Int cell)
    {
        if (IsBorderCell(cell) || (cell.x % 2 == 0 && cell.y % 2 == 0))
        {
            return CellKind.Solid;
        }

        if (IsSafeSpawnCell(cell))
        {
            return CellKind.Empty;
        }

        return layoutRandom.NextDouble() < destructibleDensity ? CellKind.Destructible : CellKind.Empty;
    }

    private bool IsBorderCell(Vector2Int cell)
    {
        return cell.x == 0 || cell.y == 0 || cell.x == width - 1 || cell.y == height - 1;
    }

    // The outer ring can use its own models; without any it matches the inner pillars.
    private StageTheme.VisualSlot GetSolidVisual(Vector2Int cell)
    {
        if (theme == null)
        {
            return null;
        }

        return IsBorderCell(cell) && theme.BorderBlock != null && theme.BorderBlock.HasVariants ? theme.BorderBlock : theme.SolidBlock;
    }

    private bool IsSafeSpawnCell(Vector2Int cell)
    {
        return cell == new Vector2Int(1, 1)
            || cell == new Vector2Int(1, 2)
            || cell == new Vector2Int(2, 1)
            || cell == new Vector2Int(width - 2, height - 2)
            || cell == new Vector2Int(width - 3, height - 2)
            || cell == new Vector2Int(width - 2, height - 3);
    }

    private ExitDoor CreateExitDoor(Transform parent)
    {
        // Like the original game, the exit hides under a random breakable block.
        List<Vector2Int> hidingCells = new();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (cells[new Vector2Int(x, y)] == CellKind.Destructible)
                {
                    hidingCells.Add(new Vector2Int(x, y));
                }
            }
        }

        // Picked after the layout, so a seed still produces the same blocks. Open layouts show the exit in the far corner.
        bool hidden = hidingCells.Count > 0;
        Vector2Int cell = hidden ? hidingCells[layoutRandom.Next(hidingCells.Count)] : new Vector2Int(width - 2, height - 2);
        return ExitDoor.Create(cell, CellToWorld(cell), parent, materials, theme != null ? theme.ExitDoor : null, visualRandom, !hidden);
    }

    // Edit-mode stage previews cannot use deferred destruction.
    internal static void DestroyGenerated(UnityEngine.Object target)
    {
        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(target);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private void CreateFloor(Vector2Int cell, Transform parent)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = $"Floor {cell.x},{cell.y}";
        floor.transform.SetParent(parent);
        floor.transform.position = CellToWorld(cell) + new Vector3(0f, -0.06f, 0f);
        floor.transform.localScale = new Vector3(0.96f, 0.08f, 0.96f);
        floor.GetComponent<Renderer>().sharedMaterial = materials.Floor;
        if (theme != null && theme.Floor != null && theme.Floor.Attach(floor.transform, visualRandom))
            floor.GetComponent<Renderer>().enabled = false;
        DestroyGenerated(floor.GetComponent<Collider>());
    }

    private GameObject CreateBlock(Vector2Int cell, string blockName, Material material, float heightScale, Transform parent, StageTheme.VisualSlot visual)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = $"{blockName} {cell.x},{cell.y}";
        block.transform.SetParent(parent);
        block.transform.position = CellToWorld(cell) + new Vector3(0f, heightScale * 0.45f, 0f);
        block.transform.localScale = new Vector3(0.92f, heightScale, 0.92f);
        block.GetComponent<Renderer>().sharedMaterial = material;
        if (visual != null && visual.Attach(block.transform, visualRandom)) block.GetComponent<Renderer>().enabled = false;
        BoxCollider collider = block.GetComponent<BoxCollider>();
        collider.size = Vector3.one;
        block.AddComponent<CellObject>().Cell = cell;
        blocks[cell] = block;
        return block;
    }

    private void CreateArenaBorder(Transform parent)
    {
        if (theme != null && theme.Border != null && theme.Border.Prefab != null)
        {
            CreateBorderModel(parent);
            return;
        }

        Vector3 center = CellToWorld(new Vector2Int(width / 2, height / 2));
        CreateRail("Top Rail", center + new Vector3(0f, 0.12f, height / 2f + 0.55f), new Vector3(width, 0.25f, 0.25f), parent);
        CreateRail("Bottom Rail", center + new Vector3(0f, 0.12f, -height / 2f - 0.55f), new Vector3(width, 0.25f, 0.25f), parent);
        CreateRail("Left Rail", center + new Vector3(-width / 2f - 0.55f, 0.12f, 0f), new Vector3(0.25f, 0.25f, height), parent);
        CreateRail("Right Rail", center + new Vector3(width / 2f + 0.55f, 0.12f, 0f), new Vector3(0.25f, 0.25f, height), parent);
    }

    // A single model frames the arena instead of the rails, stretched to the current map size.
    private void CreateBorderModel(Transform parent)
    {
        StageTheme.BorderVisual border = theme.Border;
        GameObject holder = new("Arena Border");
        holder.transform.SetParent(parent);
        holder.transform.position = CellToWorld(new Vector2Int(width / 2, height / 2)) + border.Offset;

        // The prefab's own placement in the scene is ignored; Offset and Extra Scale replace it.
        GameObject visual = UnityEngine.Object.Instantiate(border.Prefab, holder.transform);
        visual.name = "Visual";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = border.Prefab.transform.localRotation;
        visual.transform.localScale = border.Prefab.transform.localScale;
        foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (Rigidbody body in visual.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }

        // Measured in the holder's own axes, before it turns or scales.
        Vector3 scale = border.ExtraScale;
        if (border.AutoFit && VisualBounds.TryMeasure(holder.transform, visual, out Bounds bounds))
        {
            if (bounds.size.x > 0.0001f) scale.x *= width / bounds.size.x;
            if (bounds.size.z > 0.0001f) scale.z *= height / bounds.size.z;
            // Models whose pivot sits off center still end up framing the arena.
            visual.transform.localPosition -= new Vector3(bounds.center.x, 0f, bounds.center.z);
        }

        holder.transform.localScale = scale;
        holder.transform.localRotation = Quaternion.Euler(border.Rotation);
    }

    private void CreateRail(string railName, Vector3 position, Vector3 scale, Transform parent)
    {
        GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rail.name = railName;
        rail.transform.SetParent(parent);
        rail.transform.position = position;
        rail.transform.localScale = scale;
        rail.GetComponent<Renderer>().sharedMaterial = materials.Border;
    }
}
