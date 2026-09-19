using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StageVisualsSmoke
{
    private const string Running = "Bomberman.StageVisualsSmoke";
    private const string PreviewSignature = "Bomberman.StageVisualsSmoke.Preview";
    private const string BlockFolder = "Assets/Models/Blocks/Stage1";
    private const string ScenePath = "Assets/StageVisualsSmoke.unity";
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
        SessionState.SetString(PreviewSignature, Signature(preview.transform));
        EditorSceneManager.SaveScene(game.gameObject.scene, ScenePath);
        Check(!File.ReadAllText(ScenePath).Contains(StagePreview.RootName), "Saved scene excludes the preview");
        StagePreview.Clear();
        Check(FindPreview() == null, "Clear removes the preview");

        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
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
            "  BorderMaterial: {fileID: 0}\n");
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        StageTheme legacy = AssetDatabase.LoadAssetAtPath<StageTheme>(path);
        Check(legacy != null && legacy.SolidBlock.Variants.Count == 1, "Single-prefab slot migrates to one variant");
        StageTheme.VisualVariant migrated = legacy.SolidBlock.Variants[0];
        Check(migrated.Prefab == concretes[0] && migrated.Position == new Vector3(0f, -0.5f, 0f) && migrated.Rotation == new Vector3(0f, 90f, 0f)
            && migrated.Scale == new Vector3(1f, 2f, 1f) && migrated.Weight == 1f, "Migration keeps the old offsets");
        Check(legacy.BreakableBlock.Variants.Count == 0 && legacy.Floor.Variants.Count == 0, "Empty legacy slots stay empty");
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

            StageManager manager = game.GetComponent<StageManager>();
            manager.RestartStage();
            Check(Signature(GameObject.Find("Generated Bomberman Map").transform) == signature, "Restart reproduces every variant and turn");
            manager.CurrentTheme.BorderBlock.Variants.Clear();
            manager.RestartStage();
            Check(SolidWalls().Where(IsBorderWall).All(t => Variant(t.Find("Visual")) is 0 or 1), "Empty Border Block falls back to Solid Block");
            Debug.Log("STAGE_VISUALS_SMOKE_PASS: legacy migration, edit-mode preview, unsaved preview, clear, preview matches play, border slot, weighted pillar variants, zero weight, quarter turns, primitive fallback, stable restart, border fallback.");
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
