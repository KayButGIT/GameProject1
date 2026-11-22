using System;
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
    private static CellObject shakeTestWall;
    private static WallPassBlockShake shakeTest;
    private static Transform shakeTestVisual;
    private static Transform shakeTestMask;
    private static Vector3 shakeTestVisualRest;
    private static Vector3 shakeTestVisualWorldRest;
    private static Vector3 shakeTestMaskRest;
    private static Bounds shakeTestBoundsRest;
    private static PlayerController shakeTestPlayer;
    private static Vector3 shakeTestPlayerPosition;
    private static Vector2Int shakeTestPlayerCell;
    private static bool shakeTestPlayerWallPass;
    private static Vector3 shakeTestPlayerActualVelocity;
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
    private static void UpdateWallPassBlockShakes() => Call("UpdateWallPassBlockShakes");
    private static void SetField(object target, Type owner, string name, object value) => owner
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void SetActorMoving(GridController actor, bool moving) => typeof(GridController)
        .GetField("<IsMoving>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(actor, moving);
    private static void SetActorDead(GridController actor, bool dead) => typeof(GridController)
        .GetField("<IsDead>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(actor, dead);
    private static string Layout() => string.Join(",", Enumerable.Range(0, 63).Select(i => Map.GetCellKind(new Vector2Int(i / 7, i % 7)).ToString()));
    private static T[] All<T>() where T : UnityEngine.Object => UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);

    private static Transform BreakableXrayMask(CellObject block) =>
        block.transform.parent.Find($"Xray Occluders/Xray Occluders {block.name}");

    private static void CheckShakingXrayAlignment()
    {
        Vector3 offset = shakeTestVisual.position - shakeTestVisualWorldRest;
        Check(Vector3.Distance(shakeTestMask.position, shakeTestMaskRest + offset) < 0.0001f,
            "Breakable block's X-ray mask follows the shaking visual");
        Bounds[] nearby = new Bounds[9];
        int count = Map.GetNearbyDestructibleBounds(Map.CellToWorld(shakeTestWall.Cell), nearby);
        Bounds expected = shakeTestBoundsRest;
        expected.center += offset;
        Check(nearby.Take(count).Any(bounds =>
            Vector3.Distance(bounds.center, expected.center) < 0.0001f && bounds.size == expected.size),
            "X-ray clipping bounds follow the shaking visual");
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
                Map.DestroyDestructibleAt(breakable.Cell);
                Check(Map.IsWalkable(breakable.Cell) && !breakable.gameObject.activeSelf, "Breakable destruction");
                manager.LoadStage(11);
                Check(manager.CurrentTheme.name == "Concrete", "Theme transition");
                layout = Layout();
                Check(game.IsPaused && TransitionText == "STAGE 11", "Loading a stage shows its stage card and freezes play");
                phase = 10;
            }
            else if (phase == 10 && !game.IsPaused)
            {
                Check(TransitionText == null, "Stage card fades out before gameplay resumes");
                shakeTestWall = All<CellObject>().First(c => Map.GetCellKind(c.Cell) == CellKind.Destructible);
                shakeTest = shakeTestWall.GetComponent<WallPassBlockShake>();
                shakeTestVisual = shakeTestWall.transform.Find("Visual");
                Check(shakeTest != null && shakeTestVisual != null, "Breakable block has a dedicated visual shake");
                shakeTestVisualRest = shakeTestVisual.localPosition;
                shakeTestVisualWorldRest = shakeTestVisual.position;
                shakeTestMask = BreakableXrayMask(shakeTestWall);
                Check(shakeTestMask != null && shakeTestMask.GetComponentInChildren<MeshRenderer>(true) != null,
                    "Breakable block has its own X-ray mask meshes");
                shakeTestMaskRest = shakeTestMask.position;
                shakeTestPlayer = All<PlayerController>().First();
                shakeTestPlayerPosition = shakeTestPlayer.transform.position;
                shakeTestPlayerCell = shakeTestPlayer.Cell;
                shakeTestPlayerWallPass = shakeTestPlayer.WallPass;
                shakeTestPlayerActualVelocity = (Vector3)typeof(GridController)
                    .GetField("actualFreeMoveVelocity", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shakeTestPlayer);
                // Keep the scripted movement sample from being replaced by live input in FixedUpdate.
                shakeTestPlayer.enabled = false;
                shakeTestPlayer.transform.position = Map.CellToWorld(shakeTestWall.Cell);
                shakeTestPlayer.GetComponent<Rigidbody>().position = shakeTestPlayer.transform.position;
                shakeTestPlayer.Cell = shakeTestWall.Cell;
                SetField(shakeTestPlayer, typeof(GridController), "actualFreeMoveVelocity", Vector3.forward);
                shakeTestPlayer.WallPass = false;
                UpdateWallPassBlockShakes();
                Check(!shakeTest.IsShaking, "Player without Wall Pass does not shake a breakable block");
                shakeTestPlayer.WallPass = true;
                UpdateWallPassBlockShakes();
                Check(shakeTest.IsShaking, "Moving Player with Wall Pass starts a breakable block shake");
                Check(shakeTest.TryGetStableVisualBounds(out shakeTestBoundsRest) && shakeTestBoundsRest.size.sqrMagnitude > 0f,
                    "Breakable block keeps stable visual bounds while shaking");
                phase = 100;
                deadline = EditorApplication.timeSinceStartup + 0.08;
            }
            else if (phase == 100 && EditorApplication.timeSinceStartup >= deadline)
            {
                Check(Vector3.Distance(shakeTestVisual.localPosition, shakeTestVisualRest) > 0.001f,
                    "Breakable block shakes continuously while the Player passes through");
                CheckShakingXrayAlignment();
                Call("SetPaused", true);
                UpdateWallPassBlockShakes();
                Check(!shakeTest.IsShaking && Vector3.Distance(shakeTestVisual.localPosition, shakeTestVisualRest) < 0.0001f,
                    "Pausing stops and restores the block shake");
                CheckShakingXrayAlignment();
                phase = 101;
                deadline = EditorApplication.timeSinceStartup + 0.08;
            }
            else if (phase == 101 && EditorApplication.timeSinceStartup >= deadline)
            {
                Check(Vector3.Distance(shakeTestVisual.localPosition, shakeTestVisualRest) < 0.0001f,
                    "Paused block visual remains at its original position");
                Call("SetPaused", false);
                UpdateWallPassBlockShakes();
                Check(shakeTest.IsShaking, "Block shake resumes when the moving Player resumes Wall Pass");
                phase = 102;
                deadline = EditorApplication.timeSinceStartup + 0.08;
            }
            else if (phase == 102 && EditorApplication.timeSinceStartup >= deadline)
            {
                Check(Vector3.Distance(shakeTestVisual.localPosition, shakeTestVisualRest) > 0.001f,
                    "Block shake continues after gameplay resumes");
                CheckShakingXrayAlignment();
                shakeTestPlayer.WallPass = false;
                SetField(shakeTestPlayer, typeof(GridController), "actualFreeMoveVelocity", Vector3.zero);
                UpdateWallPassBlockShakes();
                Check(!shakeTest.IsShaking && Vector3.Distance(shakeTestVisual.localPosition, shakeTestVisualRest) < 0.0001f,
                    "Shake stops and restores the visual when the Player leaves");

                var actors = (System.Collections.Generic.List<GridController>)typeof(BombermanPrototype)
                    .GetField("actors", Private).GetValue(game);
                GameObject passerObject = new("Wall pass enemy probe");
                DoriaEnemy wallPassEnemy = passerObject.AddComponent<DoriaEnemy>();
                wallPassEnemy.transform.position = Map.CellToWorld(shakeTestWall.Cell);
                wallPassEnemy.Cell = shakeTestWall.Cell;
                SetActorMoving(wallPassEnemy, true);
                actors.Add(wallPassEnemy);
                UpdateWallPassBlockShakes();
                Check(shakeTest.IsShaking, "Moving wall-passing enemy starts the continuous shake");
                SetActorDead(wallPassEnemy, true);
                UpdateWallPassBlockShakes();
                Check(!shakeTest.IsShaking, "Enemy death stops and restores the block shake");
                SetActorDead(wallPassEnemy, false);
                SetActorMoving(wallPassEnemy, false);
                UpdateWallPassBlockShakes();
                Check(!shakeTest.IsShaking, "Enemy leaving the wall stops the continuous shake");

                GameObject ordinaryObject = new("Ordinary enemy probe");
                DahlEnemy ordinaryEnemy = ordinaryObject.AddComponent<DahlEnemy>();
                ordinaryEnemy.transform.position = Map.CellToWorld(shakeTestWall.Cell);
                ordinaryEnemy.Cell = shakeTestWall.Cell;
                SetActorMoving(ordinaryEnemy, true);
                actors.Add(ordinaryEnemy);
                UpdateWallPassBlockShakes();
                Check(!shakeTest.IsShaking, "Moving ordinary enemy does not shake a breakable block");
                actors.Remove(ordinaryEnemy);
                UnityEngine.Object.DestroyImmediate(ordinaryObject);

                CellObject solidWall = All<CellObject>().First(c => Map.GetCellKind(c.Cell) == CellKind.Solid);
                wallPassEnemy.transform.position = Map.CellToWorld(solidWall.Cell);
                wallPassEnemy.Cell = solidWall.Cell;
                SetActorMoving(wallPassEnemy, true);
                UpdateWallPassBlockShakes();
                Check(!shakeTest.IsShaking, "Moving wall-passing enemy does not shake a solid wall");

                wallPassEnemy.transform.position = Map.CellToWorld(shakeTestWall.Cell);
                wallPassEnemy.Cell = shakeTestWall.Cell;
                UpdateWallPassBlockShakes();
                Check(shakeTest.IsShaking, "Wall shakes before the block is destroyed");
                Map.SetXrayOccluders(true);
                Check(shakeTestMask.gameObject.activeInHierarchy, "The shaking wall's X-ray mask is active");
                Map.DestroyDestructibleAt(shakeTestWall.Cell);
                Check(!shakeTest.IsShaking && Vector3.Distance(shakeTestVisual.localPosition, shakeTestVisualRest) < 0.0001f,
                    "Destroying a shaking wall restores its visual");
                Check(!shakeTestMask.gameObject.activeSelf, "A destroyed wall stops masking X-ray immediately");

                CellObject dissolveWall = All<CellObject>().First(c => Map.GetCellKind(c.Cell) == CellKind.Destructible);
                Transform dissolveMask = BreakableXrayMask(dissolveWall);
                Check(dissolveMask != null && dissolveMask.gameObject.activeInHierarchy, "The wall to dissolve has an active X-ray mask");
                GameObject dissolvingBlock = Map.TakeDestructibleForDissolve(dissolveWall.Cell);
                Check(dissolvingBlock == dissolveWall.gameObject && !dissolveMask.gameObject.activeSelf,
                    "A dissolving wall stops masking X-ray while its visual remains");
                UnityEngine.Object.Destroy(dissolvingBlock);

                actors.Remove(wallPassEnemy);
                UnityEngine.Object.DestroyImmediate(passerObject);

                shakeTestPlayer.transform.position = shakeTestPlayerPosition;
                shakeTestPlayer.GetComponent<Rigidbody>().position = shakeTestPlayerPosition;
                shakeTestPlayer.Cell = shakeTestPlayerCell;
                shakeTestPlayer.WallPass = shakeTestPlayerWallPass;
                SetField(shakeTestPlayer, typeof(GridController), "actualFreeMoveVelocity", shakeTestPlayerActualVelocity);
                shakeTestPlayer.enabled = true;
                game.TryDropBomb();
                Check(All<Bomb>().Length == 1, "Bomb created");
                Call("Explode", new Vector2Int(1, 2));
                Call("SetPaused", true);
                manager.RestartStage();
                Check(game.IsPaused && Time.timeScale == 0f && TransitionText == "STAGE 11" && Layout() == layout, "Restart resets pause and shows the same-stage card");
                phase = 1;
            }
            else if (phase == 1 && !game.IsPaused)
            {
                Check(TransitionText == null, "Restart stage card finishes");
                Check(All<Bomb>().Length == 0 && All<PlayerController>().Length == 1 && All<EnemyController>().Length == 1, "No leftover bombs or duplicate actors");
                Check(All<Transform>().Count(t => t.name == "Generated Bomberman Map") == 1, "Single map");
                Check(All<Transform>().Count(t => t.name == "Pause Canvas") == 1, "Single pause UI");
                Check(!All<Transform>().Any(t => t.name.StartsWith("Explosion ")), "No old explosions");
                Check(Layout() == layout, "Old fuse cannot modify restarted map");
                Type cause = typeof(BombermanPrototype).GetNestedType("PlayerDeathCause", BindingFlags.NonPublic);
                Call("StartPlayerDeath", Enum.Parse(cause, "Bomb"));
                Check(!game.HasLivingPlayer, "Bomb death starts");
                phase = 2;
            }
            else if (phase == 2 && game.HasLivingPlayer)
            {
                Check(manager.CurrentStage == 11 && game.IsPaused && TransitionText == "STAGE 11", "Death restarts current stage with its intro card");
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
                Debug.Log("STAGE_SMOKE_PASS: boundaries, edited lengths, validation, model fit, rails, collision, destruction, X-ray mask alignment, cleanup, scene enemies, stable restart, pause, death, fallback.");
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
