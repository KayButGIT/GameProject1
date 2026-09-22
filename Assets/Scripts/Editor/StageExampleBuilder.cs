using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StageExampleBuilder
{
    public const string Folder = "Assets/Stages";

    // Run from the smoke tests and from -executeMethod; no menu entry on purpose.
    public static void Build()
    {
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        StageTheme brick = Theme("Brick", "Concrete1", "Brick2");
        StageTheme concrete = Theme("Concrete", "Concrete2", "Concrete3");
        StageTheme snow = Theme("Snow", null, null);
        string path = Folder + "/ExampleSequence.asset";
        if (AssetDatabase.LoadAssetAtPath<StageSequence>(path) == null)
        {
            StageSequence sequence = ScriptableObject.CreateInstance<StageSequence>();
            sequence.Phases.Add(new StageSequence.Phase { Theme = brick, StageCount = 10 });
            sequence.Phases.Add(new StageSequence.Phase { Theme = concrete, StageCount = 20 });
            sequence.Phases.Add(new StageSequence.Phase { Theme = snow, StageCount = 30 });
            AssetDatabase.CreateAsset(sequence, path);
        }
        AssetDatabase.SaveAssets();
    }

    private static StageTheme Theme(string name, string solid, string breakable)
    {
        string path = $"{Folder}/{name}.asset";
        StageTheme theme = AssetDatabase.LoadAssetAtPath<StageTheme>(path);
        if (theme != null) return theme;
        theme = ScriptableObject.CreateInstance<StageTheme>();
        Fit(theme.SolidBlock, solid);
        Fit(theme.BreakableBlock, breakable);
        AssetDatabase.CreateAsset(theme, path);
        return theme;
    }

    private static void Fit(StageTheme.VisualSlot slot, string name)
    {
        if (name == null) return;
        StageTheme.VisualVariant variant = new() { Prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Models/Blocks/Stage1/{name}.prefab") };
        if (variant.Prefab == null || variant.Prefab.GetComponentInChildren<MeshFilter>()?.sharedMesh == null)
            throw new System.InvalidOperationException($"Invalid block prefab: {name}");
        GameObject sample = Object.Instantiate(variant.Prefab);
        try
        {
            sample.transform.position = Vector3.zero;
            Renderer[] renderers = sample.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            Vector3 size = bounds.size;
            variant.Scale = new Vector3(1f / size.x, 1f / size.y, 1f / size.z);
            // Existing examples have cubic geometry, so their authored rotation is preserved.
            variant.Position = -Vector3.Scale(bounds.center, variant.Scale);
        }
        finally { Object.DestroyImmediate(sample); }
        slot.Variants.Add(variant);
    }

    // Batch setup runs only in an isolated copy, then the generated assets are copied back.
    public static void BuildAndWirePrototype()
    {
        Build();
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype.unity");
        BombermanPrototype game = Object.FindFirstObjectByType<BombermanPrototype>();
        StageManager manager = game.GetComponent<StageManager>();
        if (manager == null) manager = game.gameObject.AddComponent<StageManager>();
        SerializedObject settings = new(manager);
        settings.FindProperty("sequence").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StageSequence>(Folder + "/ExampleSequence.asset");
        settings.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(game.gameObject.scene);
        Debug.Log("STAGE_EXAMPLES_PASS");
    }
}
