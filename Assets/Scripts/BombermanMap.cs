using System.Collections.Generic;
using UnityEngine;

public sealed class BombermanMap
{
    private readonly int width;
    private readonly int height;
    private readonly float destructibleDensity;
    private readonly int randomSeed;
    private readonly BombermanMaterials materials;
    private readonly Dictionary<Vector2Int, CellKind> cells = new();

    public BombermanMap(int width, int height, float destructibleDensity, int randomSeed, BombermanMaterials materials)
    {
        this.width = width;
        this.height = height;
        this.destructibleDensity = destructibleDensity;
        this.randomSeed = randomSeed;
        this.materials = materials;
    }

    public float LeftWorldX => CellToWorld(new Vector2Int(0, 0)).x;
    public float RightWorldX => CellToWorld(new Vector2Int(width - 1, 0)).x;

    public void Generate()
    {
        Random.InitState(randomSeed == 0 ? System.Environment.TickCount : randomSeed);
        GameObject mapRoot = new("Generated Bomberman Map");
        cells.Clear();

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
                    CreateBlock(cell, "Solid Wall", materials.Solid, 1f, mapRoot.transform);
                }
                else if (kind == CellKind.Destructible)
                {
                    CreateBlock(cell, "Destructible Wall", materials.Destructible, 0.8f, mapRoot.transform);
                }
            }
        }

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
        CellObject[] cellObjects = UnityEngine.Object.FindObjectsByType<CellObject>(FindObjectsSortMode.None);
        foreach (CellObject cellObject in cellObjects)
        {
            if (cellObject.Cell == cell)
            {
                UnityEngine.Object.Destroy(cellObject.gameObject);
                return;
            }
        }
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
        bool border = cell.x == 0 || cell.y == 0 || cell.x == width - 1 || cell.y == height - 1;
        if (border || (cell.x % 2 == 0 && cell.y % 2 == 0))
        {
            return CellKind.Solid;
        }

        if (IsSafeSpawnCell(cell))
        {
            return CellKind.Empty;
        }

        return Random.value < destructibleDensity ? CellKind.Destructible : CellKind.Empty;
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

    private void CreateFloor(Vector2Int cell, Transform parent)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = $"Floor {cell.x},{cell.y}";
        floor.transform.SetParent(parent);
        floor.transform.position = CellToWorld(cell) + new Vector3(0f, -0.06f, 0f);
        floor.transform.localScale = new Vector3(0.96f, 0.08f, 0.96f);
        floor.GetComponent<Renderer>().material = materials.Floor;
        UnityEngine.Object.Destroy(floor.GetComponent<Collider>());
    }

    private GameObject CreateBlock(Vector2Int cell, string blockName, Material material, float heightScale, Transform parent)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = $"{blockName} {cell.x},{cell.y}";
        block.transform.SetParent(parent);
        block.transform.position = CellToWorld(cell) + new Vector3(0f, heightScale * 0.45f, 0f);
        block.transform.localScale = new Vector3(0.92f, heightScale, 0.92f);
        block.GetComponent<Renderer>().material = material;
        BoxCollider collider = block.GetComponent<BoxCollider>();
        collider.size = new Vector3(0.86f, 1f, 0.86f);
        block.AddComponent<CellObject>().Cell = cell;
        return block;
    }

    private void CreateArenaBorder(Transform parent)
    {
        Vector3 center = CellToWorld(new Vector2Int(width / 2, height / 2));
        CreateRail("Top Rail", center + new Vector3(0f, 0.12f, height / 2f + 0.55f), new Vector3(width, 0.25f, 0.25f), parent);
        CreateRail("Bottom Rail", center + new Vector3(0f, 0.12f, -height / 2f - 0.55f), new Vector3(width, 0.25f, 0.25f), parent);
        CreateRail("Left Rail", center + new Vector3(-width / 2f - 0.55f, 0.12f, 0f), new Vector3(0.25f, 0.25f, height), parent);
        CreateRail("Right Rail", center + new Vector3(width / 2f + 0.55f, 0.12f, 0f), new Vector3(0.25f, 0.25f, height), parent);
    }

    private void CreateRail(string railName, Vector3 position, Vector3 scale, Transform parent)
    {
        GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rail.name = railName;
        rail.transform.SetParent(parent);
        rail.transform.position = position;
        rail.transform.localScale = scale;
        rail.GetComponent<Renderer>().material = materials.Border;
    }
}
