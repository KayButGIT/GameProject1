using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class StageLightingBuilder
{
    public const string ProfilePath = StageExampleBuilder.Folder + "/DayPostProcessing.asset";

    // Saves the built-in day post-processing as an editable asset to assign to a theme's Lighting.
    // Run from the smoke tests and from -executeMethod; no menu entry on purpose.
    public static void CreateDayProfile()
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            StageLighting.ConfigureDefaultProfile(profile);
            foreach (VolumeComponent component in profile.components)
            {
                component.name = component.GetType().Name;
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
            }

            AssetDatabase.SaveAssets();
        }

        Selection.activeObject = profile;
        EditorGUIUtility.PingObject(profile);
    }
}
