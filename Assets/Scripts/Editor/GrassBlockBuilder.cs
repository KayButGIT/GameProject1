using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class GrassBlockBuilder
{
    public const string Folder = "Assets/Models/Blocks/Grass";
    public const string PrefabPath = Folder + "/GrassBlock.prefab";
    public const string FloorPrefabPath = Folder + "/GrassFloor.prefab";

    [MenuItem("Tools/Bomberman/Create Missing Grass Floor")]
    public static void BuildFloor()
    {
        Build();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(FloorPrefabPath) != null) return;
        GameObject floor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        try
        {
            floor.name = "GrassFloor";
            // BombermanMap uses 0.08-high floor roots. Compensate only the blades,
            // preserving the thin soil and keeping their roots on the grass cap.
            const float floorHeight = 0.08f;
            const float bladeRootHeight = 0.502f;
            Transform blades = floor.GetComponent<GrassBlock>().GrassMesh.transform;
            blades.localScale = new Vector3(1f, 1f / floorHeight, 1f);
            blades.localPosition = new Vector3(0, bladeRootHeight * (1f - 1f / floorHeight), 0);
            PrefabUtility.SaveAsPrefabAsset(floor, FloorPrefabPath);
            AssetDatabase.SaveAssets();
        }
        finally { Object.DestroyImmediate(floor); }
        Debug.Log("GRASS_FLOOR_BUILD_PASS: " + FloorPrefabPath);
    }

    [MenuItem("Tools/Bomberman/Create Missing Grass Block")]
    public static void Build()
    {
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
        Shader windShader = Shader.Find("Bomberman/Grass Wind");
        if (windShader == null) throw new InvalidOperationException("Grass Wind shader is missing.");
        Material soil = Material("Soil", Shader.Find("Universal Render Pipeline/Simple Lit"), new Color(0.30f, 0.155f, 0.065f));
        Material turf = Material("GrassCap", Shader.Find("Universal Render Pipeline/Simple Lit"), new Color(0.36f, 0.58f, 0.025f));
        Material grass = Material("GrassWind", windShader, new Color(0.22f, 0.43f, 0.015f));
        grass.enableInstancing = true;
        EditorUtility.SetDirty(grass);
        GameObject root = new("GrassBlock");
        try
        {
            GrassBlock settings = root.AddComponent<GrassBlock>();
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh soilMesh = Object.Instantiate(cube.GetComponent<MeshFilter>().sharedMesh);
            Object.DestroyImmediate(cube);
            soilMesh.name = "SoilCube";
            Child(root, "Soil", SaveMesh(soilMesh, Folder + "/SoilCube.asset"), soil);
            Child(root, "Grass Cap", SaveMesh(BuildCap(), Folder + "/GrassCap.asset"), turf);
            settings.GrassMesh = Child(root, "Grass Blades", SaveMesh(BuildGrass(settings), Folder + "/GrassBlock_Blades.asset"), grass);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
        }
        finally { Object.DestroyImmediate(root); }
        Debug.Log("GRASS_BUILD_PASS: " + PrefabPath);
    }

    private static Material Material(string name, Shader shader, Color color)
    {
        string path = $"{Folder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(shader) { name = name };
        material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0);
        if (material.HasProperty("_SpecColor")) material.SetColor("_SpecColor", Color.black);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static MeshFilter Child(GameObject root, string name, Mesh mesh, Material material)
    {
        GameObject child = new(name);
        child.transform.SetParent(root.transform, false);
        MeshFilter filter = child.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        MeshRenderer renderer = child.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        return filter;
    }

    public static Mesh SaveMesh(Mesh mesh, string path)
    {
        Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (saved == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
        Undo.RecordObject(saved, "Rebuild grass mesh");
        EditorUtility.CopySerialized(mesh, saved);
        Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(saved);
        return saved;
    }

    public static Mesh BuildGrass(GrassBlock settings)
    {
        int count = Mathf.Clamp(settings.BladeCount, 1, 2000);
        float minHeight = Mathf.Clamp(settings.MinimumHeight, 0.01f, 3f);
        float maxHeight = Mathf.Clamp(settings.MaximumHeight, minHeight, 3f);
        float width = Mathf.Clamp(settings.BladeWidth, 0.005f, 0.2f);
        System.Random random = new(settings.RandomSeed);
        float Next(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
        List<Vector3> vertices = new(count * 7);
        List<Vector3> normals = new(count * 7);
        List<Vector2> uv = new(count * 7);
        List<Vector2> roots = new(count * 7);
        List<int> triangles = new(count * 15);
        // Shuffle a stratified grid to spread roots evenly without introducing rows.
        int side = Mathf.CeilToInt(Mathf.Sqrt(count));
        int[] cells = new int[side * side];
        for (int i = 0; i < cells.Length; i++) cells[i] = i;
        for (int i = cells.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (cells[i], cells[j]) = (cells[j], cells[i]);
        }
        for (int blade = 0; blade < count; blade++)
        {
            float x = ((cells[blade] % side + Next(0.12f, 0.88f)) / side - 0.5f) * 0.94f;
            float z = ((cells[blade] / side + Next(0.12f, 0.88f)) / side - 0.5f) * 0.94f;
            Vector3 root = new(x, 0.502f, z);
            float angle = Next(0, Mathf.PI * 2f);
            Vector3 across = new(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            Vector3 forward = new(-across.z, 0, across.x);
            Vector3 bend = forward * Next(0.015f, 0.075f);
            Vector3 normal = (forward + Vector3.up * 0.45f).normalized;
            float height = Next(minHeight, maxHeight);
            float halfWidth = width * Next(0.65f, 1.2f) * 0.5f;
            float variation = Next(0, 1);
            int first = vertices.Count;
            void Vertex(float t, float edge)
            {
                vertices.Add(root + Vector3.up * (height * t) + bend * (t * t) + across * (halfWidth * edge));
                normals.Add(normal);
                uv.Add(new Vector2(variation, t));
                roots.Add(new Vector2(x, z));
            }
            Vertex(0, -1); Vertex(0, 1);
            Vertex(0.4f, -0.75f); Vertex(0.4f, 0.75f);
            Vertex(0.75f, -0.35f); Vertex(0.75f, 0.35f);
            Vertex(1, 0);
            int[] face = { 0, 2, 1, 1, 2, 3, 2, 4, 3, 3, 4, 5, 4, 6, 5 };
            foreach (int index in face) triangles.Add(first + index);
        }
        Mesh mesh = new() { name = "Grass Blades" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uv);
        mesh.SetUVs(1, roots);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        Bounds bounds = mesh.bounds;
        bounds.Expand(0.26f); // Maximum shader displacement is 0.12 local units in any direction.
        mesh.bounds = bounds;
        return mesh;
    }

    private static Mesh BuildCap()
    {
        List<Vector3> vertices = new();
        List<int> triangles = new();
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = vertices.Count;
            vertices.AddRange(new[] { a, b, c, d });
            triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
        const float edge = 0.501f;
        Quad(new(-edge, 0.502f, -edge), new(-edge, 0.502f, edge), new(edge, 0.502f, edge), new(edge, 0.502f, -edge));
        Vector3[] corners = { new(-edge, 0, -edge), new(edge, 0, -edge), new(edge, 0, edge), new(-edge, 0, edge) };
        for (int side = 0; side < 4; side++)
            for (int strip = 0; strip < 8; strip++)
            {
                Vector3 a = Vector3.Lerp(corners[side], corners[(side + 1) % 4], strip / 8f);
                Vector3 b = Vector3.Lerp(corners[side], corners[(side + 1) % 4], (strip + 1) / 8f);
                float lowerA = 0.415f + Mathf.Sin((side * 8 + strip) * 2.3f) * 0.022f;
                float lowerB = 0.415f + Mathf.Sin((side * 8 + strip + 1) * 2.3f) * 0.022f;
                Quad(a + Vector3.up * lowerA, a + Vector3.up * 0.502f, b + Vector3.up * 0.502f, b + Vector3.up * lowerB);
            }
        Mesh mesh = new() { name = "Grass Cap" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}

[CustomEditor(typeof(GrassBlock))]
public sealed class GrassBlockEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        GrassBlock block = (GrassBlock)target;
        EditorGUILayout.HelpBox("Wind and colors are on the GrassWind material. Rebuild saves geometry; prefab mesh changes affect all instances using that mesh. Height is limited to 0.01–3 units.", MessageType.Info);
        using (new EditorGUI.DisabledScope(Application.isPlaying || block.GrassMesh == null))
        {
            if (!GUILayout.Button("Rebuild Grass")) return;
            string prefabPath = AssetDatabase.GetAssetPath(block.gameObject);
            PrefabStage stage = PrefabStageUtility.GetPrefabStage(block.gameObject);
            if (stage != null) prefabPath = stage.assetPath;
            string meshPath;
            if (!string.IsNullOrEmpty(prefabPath))
                meshPath = Path.GetDirectoryName(prefabPath).Replace('\\', '/') + "/" + Path.GetFileNameWithoutExtension(prefabPath) + "_Blades.asset";
            else
                meshPath = EditorUtility.SaveFilePanelInProject("Save rebuilt grass mesh", "GrassBlock_Blades", "asset", "Save a mesh for this scene instance.", GrassBlockBuilder.Folder);
            if (string.IsNullOrEmpty(meshPath)) return;
            Undo.RecordObject(block.GrassMesh, "Assign rebuilt grass mesh");
            block.GrassMesh.sharedMesh = GrassBlockBuilder.SaveMesh(GrassBlockBuilder.BuildGrass(block), meshPath);
            EditorUtility.SetDirty(block.GrassMesh);
            if (stage != null) EditorSceneManager.MarkSceneDirty(stage.scene);
            else if (PrefabUtility.IsPartOfPrefabAsset(block)) PrefabUtility.SavePrefabAsset(block.gameObject.transform.root.gameObject);
            else PrefabUtility.RecordPrefabInstancePropertyModifications(block.GrassMesh);
            AssetDatabase.SaveAssets();
            SceneView.RepaintAll();
        }
    }
}
