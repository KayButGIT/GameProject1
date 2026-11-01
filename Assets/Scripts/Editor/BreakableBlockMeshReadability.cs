using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

internal static class BreakableBlockMeshReadability
{
    public static string GetSettingsSignature(StageTheme theme)
    {
        StringBuilder signature = new();
        List<StageTheme.VisualVariant> variants = theme != null ? theme.BreakableBlock?.Variants : null;
        if (variants == null) return string.Empty;

        signature.Append(variants.Count).Append('|');
        foreach (StageTheme.VisualVariant variant in variants)
        {
            if (variant == null)
            {
                signature.Append("null|");
                continue;
            }

            signature.Append(variant.Prefab != null ? AssetDatabase.GetAssetPath(variant.Prefab) : string.Empty)
                .Append(':').Append((int)variant.BurnMode).Append('|');
        }
        return signature.ToString();
    }

    public static void SyncAllThemes()
    {
        HashSet<string> breakableModels = new();
        HashSet<string> woodModels = new();
        Dictionary<string, string[]> dependenciesByPrefab = new();

        foreach (string themeGuid in AssetDatabase.FindAssets("t:StageTheme"))
        {
            string themePath = AssetDatabase.GUIDToAssetPath(themeGuid);
            StageTheme theme = AssetDatabase.LoadAssetAtPath<StageTheme>(themePath);
            List<StageTheme.VisualVariant> variants = theme != null ? theme.BreakableBlock?.Variants : null;
            if (variants == null) continue;

            foreach (StageTheme.VisualVariant variant in variants)
            {
                if (variant == null || variant.Prefab == null) continue;
                string prefabPath = AssetDatabase.GetAssetPath(variant.Prefab);
                if (string.IsNullOrEmpty(prefabPath)) continue;

                if (!dependenciesByPrefab.TryGetValue(prefabPath, out string[] dependencies))
                    dependenciesByPrefab[prefabPath] = dependencies = AssetDatabase.GetDependencies(prefabPath, true);

                foreach (string dependency in dependencies)
                {
                    if (AssetImporter.GetAtPath(dependency) is not ModelImporter) continue;
                    breakableModels.Add(dependency);
                    if (variant.BurnMode == StageTheme.BreakableBlockBurnMode.WoodBlock)
                        woodModels.Add(dependency);
                }
            }
        }

        foreach (string modelPath in breakableModels)
        {
            if (AssetImporter.GetAtPath(modelPath) is not ModelImporter importer) continue;
            bool shouldBeReadable = woodModels.Contains(modelPath);
            if (importer.isReadable == shouldBeReadable) continue;

            importer.isReadable = shouldBeReadable;
            importer.SaveAndReimport();
        }
    }
}
