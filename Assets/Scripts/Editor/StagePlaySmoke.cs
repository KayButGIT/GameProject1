using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StagePlaySmoke
{
    private const string Running = "Bomberman.StageSmoke";
    private static BombermanPrototype game;
    private static StageManager manager;
    private static string layout;
    private static int phase;
    private static double deadline, watchdog;
    private static bool failed;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run in an isolated batch project.");
        StageExampleBuilder.Build();
        Check(UnityEngine.Object.FindObjectsByType<SoundManager>(FindObjectsSortMode.None).Length == 0,
            "Prototype smoke starts without a scene SoundManager");
        StageSequence sequence = AssetDatabase.LoadAssetAtPath<StageSequence>(StageExampleBuilder.Folder + "/ExampleSequence.asset");
        int[] stages = { 1, 10, 11, 30, 31, 60, 61, int.MaxValue };
        int[] phases = { 0, 0, 1, 1, 2, 2, 2, 2 };
        for (int i = 0; i < stages.Length; i++)
            Check(sequence.TryResolve(stages[i], out int index, out _) && index == phases[i], "Phase boundary " + stages[i]);
        Check(!sequence.TryResolve(0, out _, out _), "Reject stage zero");
        StageSequence copy = UnityEngine.Object.Instantiate(sequence);
        copy.Phases[0].StageCount = 3;
        Check(copy.TryResolve(4, out int changed, out _) && changed == 1, "Edited phase lengths");
        copy.Phases[0].StageCount = 0;
        Check(!copy.Validate(out _), "Reject zero-length phase");
        copy.Phases[0].StageCount = 1;
        copy.Phases[0].Theme = null;
        Check(!copy.Validate(out _), "Reject missing theme");
        UnityEngine.Object.DestroyImmediate(copy);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        sequence = AssetDatabase.LoadAssetAtPath<StageSequence>(StageExampleBuilder.Folder + "/ExampleSequence.asset");
        game = new GameObject("Stage Smoke Game").AddComponent<BombermanPrototype>();
        SerializedObject settings = new(game);
        settings.FindProperty("width").intValue = 9;
        settings.FindProperty("height").intValue = 7;
        settings.FindProperty("randomSeed").intValue = 123;
        settings.FindProperty("spawnEnemies").boolValue = false;
        settings.FindProperty("playerDeathAnimationTime").floatValue = 0.05f;
        settings.FindProperty("playerDeathParticleTime").floatValue = 0.05f;
        settings.ApplyModifiedPropertiesWithoutUndo();
        manager = game.gameObject.AddComponent<StageManager>();
        settings = new SerializedObject(manager);
        settings.FindProperty("sequence").objectReferenceValue = sequence;
        settings.ApplyModifiedPropertiesWithoutUndo();
        GameObject enemy = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Valcom.prefab"));
        enemy.transform.position = new Vector3(2.5f, 0f, 1.5f);
        enemy.GetComponent<EnemyController>().MoveSpeed = 0;
        EditorSceneManager.SaveScene(game.gameObject.scene, "Assets/StageSmoke.unity");
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
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
        if (!condition) throw new Exception("Stage smoke: " + message);
    }
    private static BombermanMap Map => (BombermanMap)typeof(BombermanPrototype).GetField("map", Private).GetValue(game);
    private static string TransitionText => GameObject.Find("Transition Canvas")?.GetComponentInChildren<UnityEngine.UI.Text>(true)?.text;
    private static void Call(string name, params object[] args) => typeof(BombermanPrototype).GetMethod(name, Private).Invoke(game, args);
    private static string Layout() => string.Join(",", Enumerable.Range(0, 63).Select(i => Map.GetCellKind(new Vector2Int(i / 7, i % 7)).ToString()));
    private static T[] All<T>() where T : UnityEngine.Object => UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);

    private static void CheckWallPassBlockShake(CellObject breakable)
    {
        WallPassBlockShake shake = breakable.GetComponent<WallPassBlockShake>();
        Transform visual = breakable.transform.Find("Visual") ?? breakable.transform.Find("Block Visual");
        Check(shake != null && visual != null, "Breakable block has a shakeable visual and fixed collider root");
        Vector3 visualStart = visual.localPosition;
        Vector3 blockStart = breakable.transform.position;
        Bounds colliderStart = breakable.GetComponent<BoxCollider>().bounds;
        Bounds[] xrayBoundsBefore = new Bounds[9];
        int xrayBoundsCount = Map.GetNearbyDestructibleBounds(Map.CellToWorld(breakable.Cell), xrayBoundsBefore);
        List<GridController> actors = (List<GridController>)typeof(BombermanPrototype).GetField("actors", Private).GetValue(game);
        DoriaEnemy first = CreateWallPassTestActor(breakable);
        DoriaEnemy second = CreateWallPassTestActor(breakable);
        ValcomEnemy nonWallPass = new GameObject("Non Wall Pass Shake Test Actor").AddComponent<ValcomEnemy>();
        nonWallPass.enabled = false;
        nonWallPass.transform.position = Map.CellToWorld(breakable.Cell);
        nonWallPass.StartMove(breakable.Cell, Map.CellToWorld(breakable.Cell));
        actors.Add(first);
        actors.Add(second);
        actors.Add(nonWallPass);
        try
        {
            Call("UpdateWallPassBlockShakes");
            Check(!shake.IsShaking, "Stationary wall-pass actors do not shake their block");

            nonWallPass.transform.position = Map.CellToWorld(breakable.Cell);
            Call("UpdateWallPassBlockShakes");
            Check(!shake.IsShaking, "Moving non-wall-pass actors do not shake a block");

            first.StartMove(breakable.Cell, Map.CellToWorld(breakable.Cell));
            Call("UpdateWallPassBlockShakes");
            shake.SendMessage("LateUpdate");
            Bounds[] xrayBoundsDuringShake = new Bounds[9];
            int xrayBoundsDuringShakeCount = Map.GetNearbyDestructibleBounds(Map.CellToWorld(breakable.Cell), xrayBoundsDuringShake);
            Check(shake.IsShaking && (visual.localPosition - visualStart).sqrMagnitude > 0.0000001f
                && breakable.transform.position == blockStart
                && breakable.GetComponent<BoxCollider>().bounds.center == colliderStart.center
                && xrayBoundsDuringShakeCount == xrayBoundsCount
                && Enumerable.Range(0, xrayBoundsCount).All(i => xrayBoundsDuringShake[i].Equals(xrayBoundsBefore[i])),
                "Wall shakes the visual without moving its collider or x-ray cutout bounds");

            second.StartMove(breakable.Cell, Map.CellToWorld(breakable.Cell));
            first.transform.position = Map.CellToWorld(breakable.Cell + Vector2Int.right);
            Call("UpdateWallPassBlockShakes");
            Check(shake.IsShaking, "A second wall-pass actor keeps the block shaking");

            second.transform.position = Map.CellToWorld(breakable.Cell + Vector2Int.right);
            Call("UpdateWallPassBlockShakes");
            Check(!shake.IsShaking && visual.localPosition == visualStart, "Leaving the block stops shake and restores its visual");

            second.transform.position = Map.CellToWorld(breakable.Cell);
            Call("UpdateWallPassBlockShakes");
            Check(shake.IsShaking, "Wall-pass occupancy resumes after re-entering the block");

            Call("SetPaused", true);
            Call("UpdateWallPassBlockShakes");
            Check(!shake.IsShaking && visual.localPosition == visualStart, "Pausing stops shake and restores the exact visual position");
            Call("SetPaused", false);

            first.transform.position = Map.CellToWorld(breakable.Cell);
            second.transform.position = Map.CellToWorld(breakable.Cell);
            Call("UpdateWallPassBlockShakes");
            Check(shake.IsShaking, "Wall-pass occupancy resumes after unpausing");

            typeof(GridController).GetProperty(nameof(GridController.IsDead)).GetSetMethod(true).Invoke(first, new object[] { true });
            typeof(GridController).GetProperty(nameof(GridController.IsDead)).GetSetMethod(true).Invoke(second, new object[] { true });
            Call("UpdateWallPassBlockShakes");
            Check(!shake.IsShaking && visual.localPosition == visualStart, "Dead actors stop shake and restore the exact visual position");
        }
        finally
        {
            if (game.IsPaused) Call("SetPaused", false);
            actors.Remove(first);
            actors.Remove(second);
            actors.Remove(nonWallPass);
            Call("UpdateWallPassBlockShakes");
            UnityEngine.Object.DestroyImmediate(first.gameObject);
            UnityEngine.Object.DestroyImmediate(second.gameObject);
            UnityEngine.Object.DestroyImmediate(nonWallPass.gameObject);
        }
    }

    private static DoriaEnemy CreateWallPassTestActor(CellObject breakable)
    {
        GameObject actorObject = new("Wall Pass Shake Test Actor");
        actorObject.transform.position = Map.CellToWorld(breakable.Cell);
        DoriaEnemy actor = actorObject.AddComponent<DoriaEnemy>();
        actor.enabled = false;
        actor.Cell = breakable.Cell;
        return actor;
    }

    private static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying) return;
            if (failed) throw new Exception("Unexpected runtime error.");
            if (EditorApplication.timeSinceStartup > watchdog) throw new Exception("Stage smoke timed out.");
            if (phase == 0)
            {
                game = UnityEngine.Object.FindFirstObjectByType<BombermanPrototype>();
                if (game == null || !game.IsReady) return;
                Check(SoundManager.Instance != null && SoundManager.Instance.Settings != null
                    && UnityEngine.Object.FindObjectsByType<SoundManager>(FindObjectsSortMode.None).Length == 1,
                    "Direct gameplay startup bootstraps exactly one shared SoundManager");
                manager = game.GetComponent<StageManager>();
                Check(manager.CurrentStage == 1 && manager.CurrentTheme != null && manager.CurrentTheme.name == "Brick", "Initial stage");
                Check(All<EnemyController>().Length == 1, "Placed enemy registration");
                CellObject solid = All<CellObject>().First(c => Map.GetCellKind(c.Cell) == CellKind.Solid);
                Check(solid.transform.Find("Visual") != null, "Theme model attached");
                Check(solid.GetComponent<BoxCollider>().enabled && solid.transform.localScale.x == 0.92f, "Collider preserved");
                Check(solid.transform.Find("Visual").GetComponentsInChildren<Collider>().All(c => !c.enabled), "Visual colliders disabled");
                Bounds bounds = solid.transform.Find("Visual").GetComponentInChildren<Renderer>().bounds;
                Check(Mathf.Abs(bounds.size.x - 1f) < 0.01f && Mathf.Abs(bounds.size.y - 1f) < 0.01f, "Example model keeps its own size");
                CellObject breakable = All<CellObject>().First(c => Map.GetCellKind(c.Cell) == CellKind.Destructible);
                CheckWallPassBlockShake(breakable);
                WallPassBlockShake breakableShake = breakable.GetComponent<WallPassBlockShake>();
                Transform breakableVisual = breakable.transform.Find("Visual") ?? breakable.transform.Find("Block Visual");
                Vector3 breakableVisualStart = breakableVisual.localPosition;
                Map.SetWallPassBlockShaking(new HashSet<Vector2Int> { breakable.Cell });
                breakableShake.SendMessage("LateUpdate");
                Map.DestroyDestructibleAt(breakable.Cell);
                Check(Map.IsWalkable(breakable.Cell) && !breakable.gameObject.activeSelf
                    && breakableVisual.localPosition == breakableVisualStart,
                    "Breakable destruction restores the visual and removes its block");
                manager.LoadStage(11);
                Check(manager.CurrentTheme.name == "Concrete", "Theme transition");
                layout = Layout();
                Check(game.IsPaused && TransitionText == "STAGE 11", "Loading a stage shows its stage card and freezes play");
                phase = 10;
                deadline = EditorApplication.timeSinceStartup + 1.6;
            }
            else if (phase == 10 && EditorApplication.timeSinceStartup >= deadline)
            {
                Check(!game.IsPaused, "Stage card fades out before gameplay resumes");
                game.TryDropBomb();
                Check(All<Bomb>().Length == 1, "Bomb created");
                Call("Explode", new Vector2Int(1, 2));
                Call("SetPaused", true);
                manager.RestartStage();
                Check(game.IsPaused && Time.timeScale == 0f && TransitionText == "STAGE 11" && Layout() == layout, "Restart resets pause and shows the same-stage card");
                phase = 1;
                deadline = EditorApplication.timeSinceStartup + 1.6;
            }
            else if (phase == 1 && EditorApplication.timeSinceStartup >= deadline)
            {
                Check(!game.IsPaused, "Restart stage card finishes");
                Check(All<Bomb>().Length == 0 && All<PlayerController>().Length == 1 && All<EnemyController>().Length == 1, "No leftover bombs or duplicate actors");
                Check(All<Transform>().Count(t => t.name == "Generated Bomberman Map") == 1, "Single map");
                Check(All<Transform>().Count(t => t.name == "Pause Canvas") == 1, "Single pause UI");
                Check(!All<Transform>().Any(t => t.name.StartsWith("Explosion ")), "No old explosions");
                Check(Layout() == layout, "Old fuse cannot modify restarted map");
                Type cause = typeof(BombermanPrototype).GetNestedType("PlayerDeathCause", BindingFlags.NonPublic);
                Call("StartPlayerDeath", Enum.Parse(cause, "Bomb"));
                phase = 2;
                deadline = EditorApplication.timeSinceStartup + 0.4;
            }
            else if (phase == 2 && EditorApplication.timeSinceStartup >= deadline)
            {
                Check(manager.CurrentStage == 11 && game.HasLivingPlayer && game.IsPaused && TransitionText == "STAGE 11" && Layout() == layout, "Death restarts current stage with its intro card");
                manager.LoadStage(61);
                Check(manager.CurrentTheme.name == "Snow", "Last theme persists");
                Check(All<CellObject>().All(c => c.GetComponentsInChildren<Renderer>().Any(r => r.enabled)), "Missing visual slots use primitives");
                manager.NextStage();
                Check(manager.CurrentStage == 62, "Manual next stage");
                StageTheme floorTheme = ScriptableObject.CreateInstance<StageTheme>();
                floorTheme.Floor = manager.Sequence.Phases[0].Theme.SolidBlock;
                StageSequence floorSequence = ScriptableObject.CreateInstance<StageSequence>();
                floorSequence.Phases.Add(new StageSequence.Phase { Theme = floorTheme, StageCount = 1 });
                SerializedObject settings = new(manager);
                settings.FindProperty("sequence").objectReferenceValue = floorSequence;
                settings.ApplyModifiedPropertiesWithoutUndo();
                manager.LoadStage(1);
                Transform floor = All<Transform>().First(t => t.name == "Floor 1,1");
                Check(floor.Find("Visual") != null && !floor.GetComponent<Renderer>().enabled, "Floor replacement");
                Check(All<Transform>().Count(t => t.name.EndsWith(" Rail")) == 4, "Themes without a border prefab keep the rails");
                settings.FindProperty("sequence").objectReferenceValue = null;
                settings.ApplyModifiedPropertiesWithoutUndo();
                manager.LoadStage(1);
                Check(manager.CurrentTheme == null && manager.CurrentPhase == 0, "No sequence fallback");
                Check(All<CellObject>().All(c => c.GetComponentsInChildren<Renderer>().Any(r => r.enabled)), "Original blocks without sequence");
                UnityEngine.Object.Destroy(floorSequence);
                UnityEngine.Object.Destroy(floorTheme);
                Debug.Log("STAGE_SMOKE_PASS: boundaries, edited lengths, validation, model fit, rails, collision, destruction, cleanup, scene enemies, stable restart, pause, death, fallback.");
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
