using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EnemyPlaySmoke
{
    private const string Running = "Bomberman.EnemySmoke.Running";
    private static BombermanPrototype game;
    private static EnemyController[] enemies;
    private static Vector3[] positions;
    private static double deadline;
    private static double watchdog;
    private static int phase;
    private static EnemyController dying, trapped;
    private static bool failed;
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run smoke checks in an isolated batch project.");
        EnemyValidation.Run();
        EnemyAppearanceValidation.Run();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        game = new GameObject("Smoke Game").AddComponent<BombermanPrototype>();
        SerializedObject settings = new(game);
        settings.FindProperty("spawnEnemies").boolValue = true;
        settings.FindProperty("useOriginalStageEnemies").boolValue = false;
        settings.FindProperty("destructibleDensity").floatValue = 0f;
        settings.FindProperty("randomSeed").intValue = 123;
        string[] counts = { "valcomCount", "onealCount", "dahlCount", "minuoCount", "ovapeCount", "doriaCount", "passCount", "pontanCount" };
        foreach (string count in counts) settings.FindProperty(count).intValue = 1;
        string[] paths = AssetDatabase.FindAssets("t:Prefab", new[] { EnemyAssetBuilder.PrefabFolder });
        SerializedProperty prefabs = settings.FindProperty("enemyPrefabs");
        prefabs.arraySize = paths.Length;
        for (int i = 0; i < paths.Length; i++) prefabs.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(paths[i])).GetComponent<EnemyController>();
        settings.ApplyModifiedPropertiesWithoutUndo();
        // A ninth, scene-placed enemy uses a replacement visual and override clips.
        GameObject placed = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyAssetBuilder.PrefabFolder + "/Valcom.prefab"));
        placed.transform.position = new Vector3(0.5f, 0f, -3.5f);
        EnemyController actor = placed.GetComponent<EnemyController>();
        Transform previous = placed.transform.Find("VisualRoot/Model");
        GameObject replacement = UnityEngine.Object.Instantiate(previous.gameObject, previous.parent);
        replacement.name = "Model";
        UnityEngine.Object.DestroyImmediate(previous.gameObject);
        actor.ModelAnimator = replacement.GetComponent<Animator>();
        actor.BodyRenderer = replacement.GetComponentInChildren<Renderer>();
        AnimatorOverrideController replacementController = new(actor.ModelAnimator.runtimeAnimatorController);
        foreach (AnimationClip original in actor.ModelAnimator.runtimeAnimatorController.animationClips)
        {
            AnimationClip custom = UnityEngine.Object.Instantiate(original);
            custom.name = "Replacement" + original.name;
            AssetDatabase.CreateAsset(custom, "Assets/EnemySmoke" + original.name + ".anim");
            replacementController[original.name] = custom;
        }
        AssetDatabase.CreateAsset(replacementController, "Assets/EnemySmokeOverride.overrideController");
        actor.ModelAnimator.runtimeAnimatorController = replacementController;
        replacement.transform.localScale = Vector3.one * 0.8f;
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/EnemySmoke.unity");
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }
    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(Running, false)) return;
        deadline = EditorApplication.timeSinceStartup + 90;
        watchdog = deadline;
        EditorApplication.update += Tick;
        Application.logMessageReceived += Log;
    }
    private static void Log(string message, string stack, LogType type)
    {
        // Unity 6000.3 can throw while creating its search index in a fresh batch project.
        // This editor-only subsystem is unrelated to the runtime under test.
        if (stack.Contains("UnityEditor.Search.SearchDatabase")) return;
        if (type == LogType.Exception || type == LogType.Error) failed = true;
    }
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }
    private static void Pause(bool value) => typeof(BombermanPrototype).GetMethod("SetPaused", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { value });
    private static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying) return;
            if (failed) throw new Exception("Unexpected runtime error; see log.");
            double now = EditorApplication.timeSinceStartup;
            if (now > watchdog) throw new Exception("Play smoke timed out.");
            // Gameplay deadlines use scaled game time; only the paused phase uses wall time.
            // Batch editor stalls otherwise let assertions outrun movement and animation.
            double clock = phase == 2 ? now : Time.timeAsDouble;
            if (phase == 0)
            {
                game = UnityEngine.Object.FindFirstObjectByType<BombermanPrototype>();
                if (game == null || !game.IsReady)
                {
                    if (now > deadline) throw new Exception("Game startup timed out.");
                    return;
                }
                enemies = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
                Check(enemies.Length == 9, "Eight spawned enemies plus one scene-placed enemy expected.");
                GameObject reference = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyAssetBuilder.PrefabFolder + "/Doria.prefab"));
                reference.GetComponent<EnemyController>().enabled = false;
                reference.GetComponentInChildren<Animator>().enabled = false;
                EnemyAppearanceValidation.Capture(reference, "prefab");
                UnityEngine.Object.DestroyImmediate(reference);
                positions = enemies.Select(e => e.transform.position).ToArray();
                phase = 1; deadline = Time.timeAsDouble + 1.2;
            }
            else if (clock >= deadline && phase == 1)
            {
                for (int i = 0; i < enemies.Length; i++)
                {
                    Check(enemies[i].transform.position != positions[i], enemies[i].name + " did not move.");
                    Check(enemies[i].ModelAnimator.GetCurrentAnimatorStateInfo(0).IsName("Walk"), "Walk transition missing.");
                    EnemyAppearanceValidation.AssertWidth(enemies[i].gameObject);
                }
                EnemyAppearanceValidation.Capture(enemies.First(e => e is DoriaEnemy).gameObject, "play-mode");
                Pause(true); positions = enemies.Select(e => e.transform.position).ToArray();
                phase = 2; deadline = now + 0.25;
            }
            else if (clock >= deadline && phase == 2)
            {
                for (int i = 0; i < enemies.Length; i++)
                    Check(enemies[i].transform.position == positions[i] && enemies[i].ModelAnimator.speed == 0f, "Pause failed.");
                Pause(false);
                dying = enemies.First(e => e is OvapeEnemy);
                var map = (BombermanMap)typeof(BombermanPrototype).GetField("map", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
                var cells = (System.Collections.Generic.Dictionary<Vector2Int, CellKind>)typeof(BombermanMap).GetField("cells", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(map);
                cells[dying.Cell] = CellKind.Destructible;
                MethodInfo explode = typeof(BombermanPrototype).GetMethod("Explode", BindingFlags.Instance | BindingFlags.NonPublic);
                explode.Invoke(game, new object[] { dying.Cell + Vector2Int.left });
                Check(map.GetCellKind(dying.Cell) == CellKind.Empty, "Wall containing enemy was not destroyed.");
                int particles = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t => t.name == "Enemy Death Particles");
                explode.Invoke(game, new object[] { dying.Cell + Vector2Int.left });
                Check(UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t => t.name == "Enemy Death Particles") == particles, "Duplicate death particles.");
                trapped = enemies.First(e => e is ValcomEnemy && !e.IsDead);
                foreach (Vector2Int d in BombermanPrototype.Directions) cells[trapped.Cell + d] = CellKind.Solid;
                Check(dying.IsDead && !dying.IsMoving, "Death gameplay state failed.");
                phase = 3; deadline = Time.timeAsDouble + 0.6;
            }
            else if (clock >= deadline && phase == 3)
            {
                Check(dying != null && dying.ModelAnimator.GetCurrentAnimatorStateInfo(0).IsName("Death"), "Death animation missing.");
                EnemyAppearanceValidation.AssertWidth(dying.gameObject);
                Check(!trapped.IsMoving && trapped.ModelAnimator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), "Trapped enemy did not transition to Idle.");
                phase = 4; deadline = Time.timeAsDouble + 0.8;
            }
            else if (clock >= deadline && phase == 4)
            {
                Check(dying == null, "Dead enemy was not removed.");
                Debug.Log("ENEMY PLAY SMOKE PASSED: spawning, scene registration, model replacement, movement, Walk, pause, Death, delayed removal.");
                Finish(0);
            }
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



