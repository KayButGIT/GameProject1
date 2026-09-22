using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Terrain trees ignore a prefab's own transform, so a model whose root carries the axis fix lies down
// once it is painted. These prefabs put the fix on a child and keep the root at identity, with the trunk
// base on the origin and an LOD Group, which is what Terrain wants for rotation and billboarding.
// Run from the smoke tests and from -executeMethod; no menu entry on purpose.
public static class TreePrefabBuilder
{
    public const string ModelPath = "Assets/Models/TestTree/source/Oak pack.fbx";
    public const string Folder = "Assets/Prefabs/Trees";

    public static void Build()
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null)
        {
            Debug.LogWarning($"No tree model at {ModelPath}.");
            return;
        }

        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        GameObject source = (GameObject)PrefabUtility.InstantiatePrefab(model);
        List<string> built = new();
        try
        {
            foreach (MeshFilter filter in source.GetComponentsInChildren<MeshFilter>())
            {
                if (!filter.name.EndsWith("_Billboard")) continue;
                built.Add(BuildTree(filter));
            }
        }
        finally
        {
            Object.DestroyImmediate(source);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"TREE_PREFAB_BUILD_PASS: {built.Count} trees in {Folder}: {string.Join(", ", built)}");
    }

    private static string BuildTree(MeshFilter part)
    {
        string treeName = "Tree_" + part.name.Replace("_Billboard", "");
        GameObject root = new(treeName);
        GameObject visual = Object.Instantiate(part.gameObject, root.transform);
        visual.name = "Model";

        // The model imports upright, so its transform under the fbx root is the one to keep.
        visual.transform.localPosition = part.transform.position;
        visual.transform.localRotation = part.transform.rotation;
        visual.transform.localScale = part.transform.lossyScale;

        // Terrain plants a tree by its origin, so the trunk base sits there and the crown is centered.
        Renderer renderer = visual.GetComponentInChildren<Renderer>();
        Bounds bounds = renderer.bounds;
        visual.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

        LODGroup group = root.AddComponent<LODGroup>();
        group.SetLODs(new[] { new LOD(0.02f, new[] { renderer }) });
        group.RecalculateBounds();

        string path = $"{Folder}/{treeName}.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return treeName;
    }
}
