using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds a stage's arena into the open scene without Play Mode. The preview is never saved,
// and while Live Preview is on it follows edits to the theme and the map settings.
[InitializeOnLoad]
public static class StagePreview
{
    public const string RootName = "Stage Preview";
    private const string LiveKey = "Bomberman.LivePreview";

    private static StageManager previewManager;
    private static StageTheme previewTheme;
    private static int previewStage;
    private static StageLighting lighting;
    private static bool refreshQueued;

    static StagePreview()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode) Clear();
        };
        EditorSceneManager.sceneClosing += (_, _) => Clear();
        ObjectChangeEvents.changesPublished += OnChangesPublished;
        // A recompile wipes these statics while the preview's own objects survive, so leftovers go now.
        if (FindRoot() == null) StageLighting.SweepOwnObjects();
    }

    public static bool Live
    {
        get => EditorPrefs.GetBool(LiveKey, true);
        set => EditorPrefs.SetBool(LiveKey, value);
    }

    public static void Build(StageManager manager, int stage, StageTheme themeOverride = null)
    {
        Clear();
        StageTheme theme = themeOverride;
        if (theme == null && manager.Sequence != null && !manager.Sequence.TryResolve(stage, out _, out theme))
        {
            manager.Sequence.Validate(out string error);
            Debug.LogWarning(error, manager.Sequence);
            return;
        }

        GameObject root = manager.GetComponent<BombermanPrototype>().BuildArenaPreview(stage, theme);
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            child.gameObject.hideFlags = HideFlags.DontSave;

        previewManager = manager;
        previewStage = stage;
        previewTheme = theme;
        ApplyLighting(theme);
    }

    private static GameObject FindRoot()
    {
        foreach (GameObject root in Resources.FindObjectsOfTypeAll<GameObject>())
            if (root != null && root.name == RootName && root.transform.parent == null && !EditorUtility.IsPersistent(root)) return root;
        return null;
    }

    public static void Clear()
    {
        RestoreLighting();
        previewManager = null;
        previewTheme = null;
        previewStage = 0;
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

    // Called by the editor's change events, and directly by tests.
    public static void RequestRefresh(Object changed)
    {
        if (!Live || previewManager == null || changed == null) return;
        bool mine = changed == previewTheme || changed == previewManager
            || changed == previewManager.gameObject || changed == previewManager.GetComponent<BombermanPrototype>();
        if (!mine || refreshQueued) return;
        refreshQueued = true;
        EditorApplication.update += Refresh;
    }

    // Rebuilds a queued refresh right away instead of waiting for the next tick.
    public static void FlushRefresh()
    {
        if (refreshQueued) Refresh();
    }

    // One rebuild per editor tick, however many changes a drag published.
    private static void Refresh()
    {
        EditorApplication.update -= Refresh;
        refreshQueued = false;
        if (!Live || previewManager == null) return;
        StageManager manager = previewManager;
        StageTheme theme = previewTheme;
        int stage = previewStage;
        bool fromSequence = manager.Sequence != null && manager.Sequence.TryResolve(stage, out _, out StageTheme resolved) && resolved == theme;
        Build(manager, stage, fromSequence ? null : theme);
    }

    private static void OnChangesPublished(ref ObjectChangeEventStream stream)
    {
        if (!Live || previewManager == null) return;
        for (int i = 0; i < stream.length; i++)
        {
            int instanceId;
            switch (stream.GetEventType(i))
            {
                case ObjectChangeKind.ChangeAssetObjectProperties:
                    stream.GetChangeAssetObjectPropertiesEvent(i, out ChangeAssetObjectPropertiesEventArgs asset);
                    instanceId = asset.instanceId;
                    break;
                case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                    stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out ChangeGameObjectOrComponentPropertiesEventArgs component);
                    instanceId = component.instanceId;
                    break;
                default:
                    continue;
            }

            RequestRefresh(EditorUtility.EntityIdToObject(instanceId));
        }
    }

    // Theme lighting is applied to the open scene and handed back when the preview goes away.
    private static void ApplyLighting(StageTheme theme)
    {
        Camera camera = Camera.main;
        if (theme == null || camera == null) return;
        lighting ??= StageLighting.Create();
        lighting.Apply(theme.Lighting, camera);
        // Neither the volume nor the theme's sun belongs in the saved scene.
        foreach (string name in new[] { StageLighting.VolumeName, StageLighting.SunName })
        {
            GameObject owned = GameObject.Find(name);
            if (owned != null) owned.hideFlags = HideFlags.DontSave;
        }
    }

    private static void RestoreLighting()
    {
        if (lighting == null) return;
        Camera camera = Camera.main;
        if (camera != null) lighting.Restore(camera);
        lighting = null;
        StageLighting.SweepOwnObjects();
    }
}
