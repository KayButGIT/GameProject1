using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StageVisualsSmoke
{
    private const string Running = "Bomberman.StageVisualsSmoke";
    private const string PreviewSignature = "Bomberman.StageVisualsSmoke.Preview";
    private const string BlockFolder = "Assets/Models/Blocks/Stage1";
    private const string ScenePath = "Assets/StageVisualsSmoke.unity";
    private const string BorderPrefabName = "MapBorder";
    private const string SceneryPrefabName = "Rock_A";
    private const string OtherSceneryPrefabName = "MushroomA";
    private static readonly Vector3 SceneryOffset = new(3f, 0f, -2f);
    private static readonly Vector3 ArenaCenter = new(-0.5f, 0f, -0.5f);
    private static readonly Color SceneSunColor = new(0.2f, 0.4f, 0.9f);
    private const int MapWidth = 15;
    private const int MapHeight = 11;
    private static GameObject[] concretes;
    private static double watchdog;
    private static bool failed;

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run in an isolated batch project.");
        concretes = LoadConcretes();
        CheckLegacyMigration();

        // A new single scene unloads unreferenced assets, so create the scene before the test assets.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        concretes = LoadConcretes();
        StageTheme theme = ScriptableObject.CreateInstance<StageTheme>();
        theme.SolidBlock.RandomYRotation = true;
        theme.SolidBlock.Variants.Add(new StageTheme.VisualVariant { Prefab = concretes[0] });
        theme.SolidBlock.Variants.Add(new StageTheme.VisualVariant { Prefab = concretes[1] });
        theme.SolidBlock.Variants.Add(new StageTheme.VisualVariant { Prefab = concretes[2], Weight = 0f });
        theme.BorderBlock.Variants.Add(new StageTheme.VisualVariant { Prefab = concretes[3] });
        theme.Floor.Variants.Add(new StageTheme.VisualVariant { Prefab = concretes[0] });
        // Same offset as the block variants above, so the door's height can be compared with theirs.
        theme.ExitDoor.Variants.Add(new StageTheme.VisualVariant { Prefab = concretes[1] });
        theme.Border.Prefab = LoadPrefab(BorderPrefabName);
        theme.Border.AutoFit = true;
        Check(theme.Border.Prefab != null, "Border prefab loaded");
        theme.Scenery.Prefab = LoadPrefab(SceneryPrefabName);
        theme.Scenery.Offset = SceneryOffset;
        theme.Scenery.Scale = new Vector3(2f, 2f, 2f);
        Check(theme.Scenery.Prefab != null && LoadPrefab(OtherSceneryPrefabName) != null, "Scenery prefabs loaded");
        AssetDatabase.CreateAsset(theme, "Assets/StageVisualsTheme.asset");
        StageSequence sequence = ScriptableObject.CreateInstance<StageSequence>();
        sequence.Phases.Add(new StageSequence.Phase { Theme = theme, StageCount = 1 });
        AssetDatabase.CreateAsset(sequence, "Assets/StageVisualsSequence.asset");
        AssetDatabase.SaveAssets();

        BombermanPrototype game = new GameObject("Stage Visuals Smoke Game").AddComponent<BombermanPrototype>();
        SerializedObject settings = new(game);
        settings.FindProperty("width").intValue = MapWidth;
        settings.FindProperty("height").intValue = MapHeight;
        settings.FindProperty("randomSeed").intValue = 123;
        settings.FindProperty("spawnEnemies").boolValue = false;
        settings.ApplyModifiedPropertiesWithoutUndo();
        StageManager manager = game.gameObject.AddComponent<StageManager>();
        settings = new SerializedObject(manager);
        settings.FindProperty("sequence").objectReferenceValue = sequence;
        settings.ApplyModifiedPropertiesWithoutUndo();
        Check(manager.Sequence == sequence && sequence.Phases[0].Theme.SolidBlock.Variants.Count == 3, "Test theme assigned");

        Camera camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        Light sceneSun = new GameObject("Scene Sun").AddComponent<Light>();
        sceneSun.type = LightType.Directional;
        sceneSun.color = SceneSunColor;
        sceneSun.intensity = 0.4f;

        List<string> errors = new();
        Application.LogCallback capture = (message, stack, type) =>
        {
            if (type == LogType.Error || type == LogType.Exception) errors.Add(message);
        };
        Application.logMessageReceived += capture;
        try { StagePreview.Build(manager, 1); }
        finally { Application.logMessageReceived -= capture; }
        Check(errors.Count == 0, "Edit-mode preview builds without errors: " + string.Join("; ", errors));
        GameObject preview = FindPreview();
        Check(preview != null, "Preview root exists");
        Check(preview.GetComponentsInChildren<Transform>(true).All(t => (t.gameObject.hideFlags & HideFlags.DontSave) == HideFlags.DontSave), "Preview objects are never saved");
        CheckBorderSize(preview.transform.Find("Generated Bomberman Map/Arena Border"), "edit-mode preview");
        Check(preview.transform.Find(StageTheme.SceneryName) != null, "The edit-mode preview shows the theme's scenery");
        CheckMenu();
        CheckStageConfigWindow(theme, sequence);
        CheckPreviewLighting(theme, sceneSun);
        CheckLivePreview(manager, theme, preview);
        preview = FindPreview();
        SessionState.SetString(PreviewSignature, Signature(preview.transform));
        EditorSceneManager.SaveScene(game.gameObject.scene, ScenePath);
        Check(!File.ReadAllText(ScenePath).Contains(StagePreview.RootName), "Saved scene excludes the preview");
        StagePreview.Clear();
        Check(FindPreview() == null, "Clear removes the preview");
        Check(sceneSun.enabled && Mathf.Abs(sceneSun.color.r - SceneSunColor.r) < 0.001f && Mathf.Abs(sceneSun.intensity - 0.4f) < 0.001f,
            "Clearing the preview gives the scene its own sun back");
        Check(Live(StageLighting.SunName) == 0 && Live(StageLighting.VolumeName) == 0, "Clearing the preview removes the theme's sun and volume");

        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }

    // Preview objects carry HideFlags.DontSave, which FindObjectsByType skips, and objects
    // waiting for deferred destruction are deactivated first, so only live ones count.
    private static int Live(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .Count(g => g != null && g.name == name && g.transform.parent == null
                && !EditorUtility.IsPersistent(g) && g.activeSelf && g.scene.IsValid());
    }

    // Setup builders run from the tests, so the menu keeps the one entry the team uses.
    private static void CheckMenu()
    {
        List<string> paths = new();
        foreach (Type type in typeof(StageConfigWindow).Assembly.GetTypes())
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                foreach (MenuItem item in method.GetCustomAttributes<MenuItem>())
                    if (item.menuItem.StartsWith("Tools/Bomberman/")) paths.Add(item.menuItem);

        paths.Sort();
        Check(paths.Count == 1 && paths[0] == "Tools/Bomberman/Stage Config",
            "Tools > Bomberman holds Stage Config alone, found: " + string.Join(", ", paths));
    }

    // The window edits a theme picked from a sequence, without taking over the Inspector.
    private static void CheckStageConfigWindow(StageTheme theme, StageSequence sequence)
    {
        UnityEngine.Object selected = Selection.activeObject;
        StageConfigWindow window = StageConfigWindow.Open();
        window.BindSequence(sequence);
        Check(window.Sequence == sequence && window.Theme == theme, "The window works from the sequence and its first phase");
        Check(window.ThemeEditor != null && window.ThemeEditor.target == theme, "The window draws that theme's own editor");

        string[] labels = window.PhaseLabels();
        Check(labels.Length == sequence.Phases.Count && labels[0].StartsWith("Phase 1 — ") && labels[0].Contains("Stages 1"),
            "Phase labels come from the sequence and carry stage ranges: " + string.Join(" | ", labels));

        StageTheme other = ScriptableObject.CreateInstance<StageTheme>();
        AssetDatabase.CreateAsset(other, "Assets/StageConfigOtherTheme.asset");
        StageSequence otherSequence = ScriptableObject.CreateInstance<StageSequence>();
        otherSequence.Phases.Add(new StageSequence.Phase { Theme = other, StageCount = 5 });
        AssetDatabase.CreateAsset(otherSequence, "Assets/StageConfigOtherSequence.asset");
        window.BindSequence(otherSequence);
        Check(window.Theme == other && window.ThemeEditor.target == other, "Another sequence binds its own theme");

        window.Bind(theme);
        Check(window.Theme == theme, "A theme outside the sequence can still be picked");
        window.BindSequence(sequence);

        Check(Selection.activeObject == selected, "The window never changes the selection");
        window.Close();
        Check(Resources.FindObjectsOfTypeAll<StageThemeEditor>().Length == 0
            && Resources.FindObjectsOfTypeAll<StageSequenceEditor>().Length == 0, "Closing the window leaves no editor behind");
        AssetDatabase.DeleteAsset("Assets/StageConfigOtherTheme.asset");
        AssetDatabase.DeleteAsset("Assets/StageConfigOtherSequence.asset");
    }

    // The preview lights the scene like Play Mode does, so the theme can be judged without pressing Play.
    private static void CheckPreviewLighting(StageTheme theme, Light sceneSun)
    {
        StageTheme.ThemeLighting lighting = theme.Lighting;
        Check(!sceneSun.enabled, "The preview turns the scene's own sun off");
        GameObject sunObject = GameObject.Find(StageLighting.SunName);
        Light sun = sunObject != null ? sunObject.GetComponentInChildren<Light>() : null;
        Check(sun != null && Mathf.Abs(sun.color.r - lighting.SunColor.r) < 0.001f
            && Mathf.Abs(sun.intensity - lighting.SunIntensity) < 0.001f
            && Quaternion.Angle(sun.transform.rotation, Quaternion.Euler(lighting.SunRotation)) < 0.1f,
            "The preview lights the stage with the theme's own sun");
        Check((sunObject.hideFlags & HideFlags.DontSave) == HideFlags.DontSave, "The preview's sun is never saved");
        Check(Live(StageLighting.VolumeName) == 1 && Live(StageLighting.SunName) == 1, "The preview leaves one volume and one sun");
    }

    // Editing a previewed theme rebuilds the preview, unless Live Preview is off.
    private static void CheckLivePreview(StageManager manager, StageTheme theme, GameObject preview)
    {
        int builtId = preview.GetInstanceID();
        StagePreview.Live = true;
        theme.Floor.Variants.Clear();
        StagePreview.RequestRefresh(theme);
        StagePreview.FlushRefresh();
        GameObject rebuilt = FindPreview();
        Check(rebuilt != null && rebuilt.GetInstanceID() != builtId, "A theme edit rebuilds the live preview");
        Check(rebuilt.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Floor "))
            .All(t => t.Find("Visual") == null), "The rebuilt preview shows the edit");

        StagePreview.Live = false;
        theme.Floor.Variants.Add(new StageTheme.VisualVariant { Prefab = concretes[0] });
        StagePreview.RequestRefresh(theme);
        StagePreview.FlushRefresh();
        Check(FindPreview().GetInstanceID() == rebuilt.GetInstanceID(), "Live Preview off leaves the preview alone");

        StagePreview.Live = true;
        // Leftovers of a domain reload must not pile up on the next build.
        new GameObject(StageLighting.VolumeName).AddComponent<UnityEngine.Rendering.Volume>().isGlobal = true;
        StagePreview.Build(manager, 1);
        Check(Live(StageLighting.VolumeName) == 1 && Live(StageLighting.SunName) == 1, "Rebuilding sweeps leftover lighting objects");
    }

    private static void CheckLegacyMigration()
    {
        const string path = "Assets/StageVisualsLegacyTheme.asset";
        string scriptGuid = AssetDatabase.AssetPathToGUID("Assets/Scripts/World/StageTheme.cs");
        Check(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(concretes[0], out string prefabGuid, out long prefabId), "Prefab identifier");
        File.WriteAllText(path,
            "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n" +
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n" +
            "  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n" +
            $"  m_Script: {{fileID: 11500000, guid: {scriptGuid}, type: 3}}\n  m_Name: StageVisualsLegacyTheme\n  m_EditorClassIdentifier: \n" +
            $"  SolidBlock:\n    Prefab: {{fileID: {prefabId}, guid: {prefabGuid}, type: 3}}\n" +
            "    Position: {x: 0, y: -0.5, z: 0}\n    Rotation: {x: 0, y: 90, z: 0}\n    Scale: {x: 1, y: 2, z: 1}\n" +
            "  Lighting:\n    Enabled: 0\n");
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        StageTheme legacy = AssetDatabase.LoadAssetAtPath<StageTheme>(path);
        Check(legacy != null && legacy.SolidBlock.Variants.Count == 1, "Single-prefab slot migrates to one variant");
        StageTheme.VisualVariant migrated = legacy.SolidBlock.Variants[0];
        Check(migrated.Prefab == concretes[0] && migrated.Position == new Vector3(0f, -0.5f, 0f) && migrated.Rotation == new Vector3(0f, 90f, 0f)
            && migrated.Scale == new Vector3(1f, 2f, 1f) && migrated.Weight == 1f, "Migration keeps the old offsets");
        Check(legacy.BreakableBlock.Variants.Count == 0 && legacy.Floor.Variants.Count == 0, "Empty legacy slots stay empty");
        CheckNewEntryDefaults(scriptGuid, prefabGuid, prefabId);
    }

    // Entries the Inspector creates arrive with zeros; they must come back with the block defaults.
    private static void CheckNewEntryDefaults(string scriptGuid, string prefabGuid, long prefabId)
    {
        const string path = "Assets/StageVisualsZeroTheme.asset";
        string variant = $"    - Prefab: {{fileID: {prefabId}, guid: {prefabGuid}, type: 3}}\n" +
            "      Position: {x: 0, y: 0, z: 0}\n      Rotation: {x: 0, y: 0, z: 0}\n";
        File.WriteAllText(path,
            "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n" +
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n" +
            "  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n" +
            $"  m_Script: {{fileID: 11500000, guid: {scriptGuid}, type: 3}}\n  m_Name: StageVisualsZeroTheme\n  m_EditorClassIdentifier: \n" +
            "  SolidBlock:\n    Variants:\n" + variant + "      Scale: {x: 0, y: 0, z: 0}\n      Weight: 0\n" +
            "  BreakableBlock:\n    Variants:\n" + variant + "      Scale: {x: 1, y: 1, z: 1}\n      Weight: 0\n" +
            "  Lighting:\n    Enabled: 0\n");
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        StageTheme theme = AssetDatabase.LoadAssetAtPath<StageTheme>(path);
        StageTheme.VisualVariant added = theme.SolidBlock.Variants[0];
        Check(added.Position == new Vector3(0f, -0.5f, 0f) && added.Scale == Vector3.one && added.Weight == 1f, "New entries get the block defaults");
        Check(theme.BreakableBlock.Variants[0].Weight == 0f && theme.BreakableBlock.Variants[0].Scale == Vector3.one, "Variants disabled with weight zero stay disabled");
    }

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(Running, false)) return;
        watchdog = EditorApplication.timeSinceStartup + 120;
        EditorApplication.update += Tick;
        Application.logMessageReceived += Log;
    }

    private static void Log(string message, string stack, LogType type)
    {
        if (stack.Contains("UnityEditor.Search.SearchDatabase")) return;
        if (type == LogType.Error || type == LogType.Exception) failed = true;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("Stage visuals smoke: " + message);
    }

    // Looked up by name, so moving a prefab into another folder does not break the test.
    private static GameObject LoadPrefab(string name)
    {
        string path = AssetDatabase.FindAssets($"{name} t:prefab")
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(candidate => Path.GetFileNameWithoutExtension(candidate) == name);
        return path == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static GameObject[] LoadConcretes()
    {
        return new[] { "Concrete1", "Concrete2", "Concrete3", "Concrete4" }
            .Select(name => AssetDatabase.LoadAssetAtPath<GameObject>($"{BlockFolder}/{name}.prefab"))
            .ToArray();
    }

    private static GameObject FindPreview()
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(g => g.name == StagePreview.RootName && g.transform.parent == null && !EditorUtility.IsPersistent(g));
    }

    // Variants share one mesh and differ by material, so a block is identified by its material and quarter turn.
    private static string Signature(Transform root)
    {
        return string.Join("|", root.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name.StartsWith("Solid Wall ") || t.name.StartsWith("Destructible Wall "))
            .OrderBy(t => t.name, StringComparer.Ordinal)
            .Select(t =>
            {
                Transform visual = t.Find("Visual");
                return visual == null ? t.name : $"{t.name}:{Variant(visual)}:{QuarterTurns(visual)}";
            }));
    }

    private static Transform[] SolidWalls()
    {
        return GameObject.Find("Generated Bomberman Map").GetComponentsInChildren<Transform>(true)
            .Where(t => t.name.StartsWith("Solid Wall ")).ToArray();
    }

    private static bool IsBorderWall(Transform wall)
    {
        int[] cell = wall.name.Substring("Solid Wall ".Length).Split(',').Select(int.Parse).ToArray();
        return cell[0] == 0 || cell[1] == 0 || cell[0] == MapWidth - 1 || cell[1] == MapHeight - 1;
    }

    private static int Variant(Transform visual)
    {
        Material material = visual.GetComponentInChildren<Renderer>().sharedMaterial;
        return Array.FindIndex(concretes, prefab => prefab.GetComponentInChildren<Renderer>().sharedMaterial == material);
    }

    private static int QuarterTurns(Transform visual)
    {
        int variant = Variant(visual);
        if (variant < 0) return -1;
        Quaternion turn = visual.localRotation * Quaternion.Inverse(concretes[variant].transform.localRotation);
        for (int quarter = 0; quarter < 4; quarter++)
            if (Quaternion.Angle(turn, Quaternion.Euler(0f, 90f * quarter, 0f)) < 0.5f) return quarter;
        return -1;
    }

    // The cell's primitive is scaled thin for floors and narrow for blocks; a model must not inherit that.
    private static void CheckModelShape(Transform visual, string what)
    {
        Check(visual != null && Variant(visual) >= 0, what + " is attached");
        Vector3 expected = concretes[Variant(visual)].transform.localScale;
        Vector3 actual = visual.lossyScale;
        Check(Mathf.Abs(actual.x - expected.x) < 0.001f && Mathf.Abs(actual.y - expected.y) < 0.001f && Mathf.Abs(actual.z - expected.z) < 0.001f,
            $"{what} keeps the prefab's own scale, got {actual.x}, {actual.y}, {actual.z}");
    }

    // A model whose pivot sits in a corner, the way a terrain does, still has to end up over the arena.
    private static void CheckSceneryCentering(StageManager manager)
    {
        GameObject source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        source.name = "Off Center Scenery";
        UnityEngine.Object.DestroyImmediate(source.GetComponent<Collider>());
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.transform.SetParent(source.transform);
        block.transform.localPosition = new Vector3(8f, 0f, 6f);
        block.transform.localScale = new Vector3(4f, 1f, 4f);

        StageTheme.SceneryVisual scenery = manager.CurrentTheme.Scenery;
        GameObject previous = scenery.Prefab;
        Vector3 previousScale = scenery.Scale;
        scenery.Prefab = source;
        scenery.Scale = Vector3.one;
        manager.RestartStage();
        Check(Measure(Scenery(), out Bounds centered), "Off center scenery is measurable");
        Vector3 target = ArenaCenter + SceneryOffset;
        Check(Mathf.Abs(centered.center.x - target.x) < 0.01f && Mathf.Abs(centered.center.z - target.z) < 0.01f,
            $"Center On Arena puts the middle of the model over the arena, got {centered.center.x}, {centered.center.z}");

        scenery.CenterOnArena = false;
        manager.RestartStage();
        Check(Scenery().Find("Visual").localPosition == Vector3.zero, "Center On Arena off keeps the prefab pivot");

        scenery.CenterOnArena = true;
        scenery.Prefab = previous;
        scenery.Scale = previousScale;
        manager.RestartStage();
        UnityEngine.Object.DestroyImmediate(source);
    }

    private static bool Measure(Transform scenery, out Bounds bounds)
    {
        bounds = default;
        if (scenery == null) return false;
        if (!VisualBounds.TryMeasure(scenery, scenery.Find("Visual").gameObject, out Bounds local)) return false;
        bounds = new Bounds(scenery.TransformPoint(local.center), local.size);
        return true;
    }

    // Destroyed scenery lingers until the end of the frame, so only the live holder counts.
    private static Transform Scenery()
    {
        BombermanPrototype game = UnityEngine.Object.FindFirstObjectByType<BombermanPrototype>();
        if (game == null) return null;
        return game.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(child => child.name == StageTheme.SceneryName && child.parent == game.transform && child.gameObject.activeSelf);
    }

    private static Transform FindBorderModel()
    {
        return GameObject.Find("Generated Bomberman Map").transform.Find("Arena Border");
    }

    private static Transform[] Rails()
    {
        return GameObject.Find("Generated Bomberman Map").GetComponentsInChildren<Transform>(true)
            .Where(t => t.name.EndsWith(" Rail")).ToArray();
    }

    // The model replaces the rails and stretches across the map, whatever size the prefab was saved at.
    private static void CheckBorderModel()
    {
        Transform border = FindBorderModel();
        Check(border != null, "The theme's border model is placed");
        Check(Rails().Length == 0, "The border model replaces the rails");
        CheckBorderSize(border, "Play Mode");
        Check(border.GetComponentsInChildren<Collider>(true).All(collider => !collider.enabled), "The border model never blocks movement");
    }

    // Measured from the meshes, because a renderer reports empty bounds in the frame it was created in.
    private static void CheckBorderSize(Transform border, string where)
    {
        Check(border != null, $"The border model is placed in the {where}");
        Bounds bounds = default;
        bool measured = false;
        foreach (MeshFilter filter in border.GetComponentsInChildren<MeshFilter>())
        {
            Bounds local = filter.sharedMesh.bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 size = new((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
                Vector3 point = filter.transform.TransformPoint(local.center + Vector3.Scale(local.extents, size));
                if (measured) bounds.Encapsulate(point);
                else { bounds = new Bounds(point, Vector3.zero); measured = true; }
            }
        }

        Check(measured, $"The border model draws something in the {where}");
        Check(Mathf.Abs(bounds.size.x - MapWidth) < 0.01f && Mathf.Abs(bounds.size.z - MapHeight) < 0.01f,
            $"Auto Fit sizes the border to the map in the {where}, got {bounds.size.x} x {bounds.size.z}");
        Check(Mathf.Abs(bounds.center.x + 0.5f) < 0.01f && Mathf.Abs(bounds.center.z + 0.5f) < 0.01f,
            $"Auto Fit centers the border on the arena in the {where}, got {bounds.center.x}, {bounds.center.z}");
    }

    private static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying) return;
            if (failed) throw new Exception("Unexpected runtime error.");
            if (EditorApplication.timeSinceStartup > watchdog) throw new Exception("Stage visuals smoke timed out.");
            BombermanPrototype game = UnityEngine.Object.FindFirstObjectByType<BombermanPrototype>();
            if (game == null || !game.IsReady) return;
            concretes = LoadConcretes();
            Transform map = GameObject.Find("Generated Bomberman Map").transform;
            string signature = Signature(map);
            Check(signature == SessionState.GetString(PreviewSignature, ""), "Edit-mode preview matches Play Mode");

            Transform[] solidWalls = SolidWalls();
            Check(solidWalls.All(t => t.Find("Visual") != null), "Every solid block has a model");
            Transform[] borderVisuals = solidWalls.Where(IsBorderWall).Select(t => t.Find("Visual")).ToArray();
            Check(borderVisuals.Length > 0 && borderVisuals.All(v => Variant(v) == 3 && QuarterTurns(v) == 0), "Border Block models the outer ring");
            Transform[] pillarVisuals = solidWalls.Where(t => !IsBorderWall(t)).Select(t => t.Find("Visual")).ToArray();
            int[] variants = pillarVisuals.Select(Variant).ToArray();
            Check(variants.Contains(0) && variants.Contains(1) && variants.All(variant => variant == 0 || variant == 1), "Pillar variants are mixed and weight zero is never picked");
            int[] turns = pillarVisuals.Select(QuarterTurns).ToArray();
            Check(turns.All(quarter => quarter >= 0) && turns.Distinct().Count() > 1, "Random Y rotation uses quarter turns");
            Check(map.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Destructible Wall ")).All(t => t.Find("Visual") == null),
                "Empty slots keep the primitive block");
            CheckModelShape(solidWalls[0].Find("Visual"), "A block model");
            Transform doorVisual = map.GetComponentsInChildren<Transform>(true)
                .First(t => t.name.StartsWith("Exit Door ")).Find("Door Visual/Visual");
            Check(doorVisual != null && Mathf.Abs(doorVisual.position.y - solidWalls[0].Find("Visual").position.y) < 0.001f,
                "The exit door model stands as high as a block model");
            Transform floor = map.GetComponentsInChildren<Transform>(true).First(t => t.name.StartsWith("Floor "));
            CheckModelShape(floor.Find("Visual"), "A floor model");

            CheckBorderModel();
            Transform scenery = Scenery();
            Check(scenery != null && scenery.parent == game.transform, "Scenery hangs off the game object, not the stage");
            Check((scenery.position - (ArenaCenter + SceneryOffset)).magnitude < 0.01f, "Scenery is centered on the arena plus its offset");
            GameObject sceneryPrefab = LoadPrefab(SceneryPrefabName);
            Check(scenery.Find("Visual").localScale == Vector3.Scale(sceneryPrefab.transform.localScale, new Vector3(2f, 2f, 2f)), "Scenery keeps the prefab scale times Scale");
            CheckSceneryCentering(manager: game.GetComponent<StageManager>());
            scenery = Scenery();
            int sceneryId = scenery.gameObject.GetInstanceID();

            StageManager manager = game.GetComponent<StageManager>();
            manager.RestartStage();
            Check(Scenery() != null && Scenery().gameObject.GetInstanceID() == sceneryId, "Restarting a stage keeps the same scenery");
            Check(Signature(GameObject.Find("Generated Bomberman Map").transform) == signature, "Restart reproduces every variant and turn");
            manager.CurrentTheme.BorderBlock.Variants.Clear();
            manager.RestartStage();
            Check(SolidWalls().Where(IsBorderWall).All(t => Variant(t.Find("Visual")) is 0 or 1), "Empty Border Block falls back to Solid Block");

            manager.CurrentTheme.Border.Prefab = null;
            manager.RestartStage();
            Check(FindBorderModel() == null && Rails().Length == 4, "Without a border prefab the four rails come back");

            GameObject otherScenery = LoadPrefab(OtherSceneryPrefabName);
            manager.CurrentTheme.Scenery.Prefab = otherScenery;
            manager.RestartStage();
            Check(Scenery() != null && Scenery().gameObject.GetInstanceID() != sceneryId, "Different scenery is rebuilt");
            Check(Scenery().Find("Visual").GetComponentInChildren<MeshFilter>().sharedMesh
                == otherScenery.GetComponentInChildren<MeshFilter>().sharedMesh, "The rebuilt scenery uses the new prefab");
            manager.CurrentTheme.Scenery.Prefab = null;
            manager.RestartStage();
            Check(Scenery() == null, "Clearing the scenery prefab removes it");
            Debug.Log("STAGE_VISUALS_SMOKE_PASS: legacy migration, new entry defaults, edit-mode preview, stage config window, sequence phases, single menu entry, preview lighting, preview sun, swept leftovers, live rebuild, live toggle off, unsaved preview, clear, lighting restored, preview matches play, border slot, weighted pillar variants, zero weight, quarter turns, primitive fallback, unsquashed block and floor models, exit door height, stable restart, border fallback, border model fits the map in edit mode and Play Mode, rail fallback, scenery placement, scenery centering, scenery pivot mode, scenery reuse, scenery swap, scenery removal.");
            Finish(0);
        }
        catch (Exception exception) { Debug.LogException(exception); Finish(1); }
    }

    private static void Finish(int code)
    {
        SessionState.SetBool(Running, false);
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= Log;
        Time.timeScale = 1f;
        EditorApplication.Exit(code);
    }
}
