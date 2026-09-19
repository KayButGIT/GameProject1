using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds a stage's arena into the open scene without Play Mode. The preview is never saved.
[InitializeOnLoad]
public static class StagePreview
{
    public const string RootName = "Stage Preview";

    static StagePreview()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode) Clear();
        };
        EditorSceneManager.sceneClosing += (_, _) => Clear();
    }

    public static void Build(StageManager manager, int stage)
    {
        Clear();
        StageTheme theme = null;
        if (manager.Sequence != null && !manager.Sequence.TryResolve(stage, out _, out theme))
        {
            manager.Sequence.Validate(out string error);
            Debug.LogWarning(error, manager.Sequence);
            return;
        }

        GameObject root = manager.GetComponent<BombermanPrototype>().BuildArenaPreview(stage, theme);
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            child.gameObject.hideFlags = HideFlags.DontSave;
    }

    public static void Clear()
    {
        foreach (GameObject root in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (root == null || root.name != RootName || root.transform.parent != null || EditorUtility.IsPersistent(root)) continue;
            // Placeholder materials are created for the preview, so they go with it.
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                foreach (Material material in renderer.sharedMaterials)
                    if (material != null && !EditorUtility.IsPersistent(material)) Object.DestroyImmediate(material);
            Object.DestroyImmediate(root);
        }
    }
}
