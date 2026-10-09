using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ExitDoorPlaySmoke
{
    private const string Running = "Bomberman.ExitDoorSmoke";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static BombermanPrototype game;
    private static StageManager manager;
    private static Vector2Int doorCell, neighbor;
    private static int phase;
    private static int scoreBeforeTransition, livesBeforeTransition;
    private static double deadline, watchdog;
    private static bool failed;

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run in an isolated batch project.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        game = new GameObject("Exit Door Smoke Game").AddComponent<BombermanPrototype>();
        SerializedObject settings = new(game);
        settings.FindProperty("width").intValue = 9;
        settings.FindProperty("height").intValue = 7;
        settings.FindProperty("randomSeed").intValue = 123;
        settings.FindProperty("spawnEnemies").boolValue = false;
        settings.FindProperty("playerCanDieFromBomb").boolValue = false;
        settings.FindProperty("blastRange").intValue = 2;
        settings.FindProperty("enemyPlayerHitDistance").floatValue = 0.05f;
        settings.FindProperty("exitDoorSpawnDelay").floatValue = 0.1f;
        settings.FindProperty("gameOverDelay").floatValue = 0.3f;
        settings.FindProperty("gameClearDelay").floatValue = 0.3f;
        settings.FindProperty("titleSceneName").stringValue = "MissingSmokeTitle";
        settings.FindProperty("playerDeathAnimationTime").floatValue = 0.05f;
        settings.FindProperty("playerDeathParticleTime").floatValue = 0.05f;
        settings.FindProperty("playerRestartDelay").floatValue = 0.05f;
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { EnemyAssetBuilder.PrefabFolder });
        SerializedProperty prefabs = settings.FindProperty("enemyPrefabs");
        prefabs.arraySize = guids.Length;
        for (int i = 0; i < guids.Length; i++)
            prefabs.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i])).GetComponent<EnemyController>();
        settings.ApplyModifiedPropertiesWithoutUndo();
        GameObject enemy = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyAssetBuilder.PrefabFolder + "/Valcom.prefab"));
        enemy.transform.position = new Vector3(2.5f, 0f, 1.5f);
        enemy.GetComponent<EnemyController>().MoveSpeed = 0;
        EditorSceneManager.SaveScene(game.gameObject.scene, "Assets/ExitDoorSmoke.unity");
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
        if (!condition) throw new Exception("Exit door smoke: " + message);
    }
    private static BombermanMap Map => (BombermanMap)typeof(BombermanPrototype).GetField("map", Private).GetValue(game);
    private static int PendingWaves => (int)typeof(BombermanPrototype).GetField("pendingExitWaves", Private).GetValue(game);
    // Coroutine methods need starting, not just invoking.
    private static void StartRoutine(string name)
    {
        game.StartCoroutine((System.Collections.IEnumerator)typeof(BombermanPrototype).GetMethod(name, Private).Invoke(game, null));
    }

    private static void Call(string name, params object[] args) => typeof(BombermanPrototype).GetMethod(name, Private).Invoke(game, args);
    private static T[] All<T>() where T : UnityEngine.Object => UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);

    private static void Teleport(Vector2Int cell)
    {
        PlayerController player = (PlayerController)typeof(BombermanPrototype).GetField("player", Private).GetValue(game);
        Vector3 position = Map.CellToWorld(cell);
        player.GetComponent<Rigidbody>().position = position;
        player.transform.position = position;
    }

    private static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying) return;
            if (failed) throw new Exception("Unexpected runtime error.");
            if (EditorApplication.timeSinceStartup > watchdog) throw new Exception("Exit door smoke timed out.");
            if (phase == 0)
            {
                game = UnityEngine.Object.FindFirstObjectByType<BombermanPrototype>();
                if (game == null || !game.IsReady) return;
                manager = game.GetComponent<StageManager>();
                doorCell = Map.ExitDoor.Cell;
                Check(Map.GetCellKind(doorCell) == CellKind.Destructible && !Map.ExitDoor.IsRevealed && !Map.ExitDoor.gameObject.activeSelf, "Exit hidden under a breakable block");
                manager.RestartStage();
                Check(Map.ExitDoor.Cell == doorCell && !Map.ExitDoor.IsRevealed, "Restart keeps the exit cell");
                Check(game.IsPaused && Time.timeScale == 0f && TransitionCard("STAGE 1"), "Stage restart shows its black stage card and pauses gameplay");
                // Keep the only starting enemy alive and outside every blast and contact check.
                EnemyController valcom = All<EnemyController>().Single();
                valcom.enabled = false;
                valcom.Cell = new Vector2Int(-10, -10);
                valcom.transform.position = new Vector3(-50f, 0f, -50f);
                neighbor = BombermanPrototype.Directions.Select(d => doorCell + d)
                    .Where(c => Map.GetCellKind(c) == CellKind.Empty || Map.GetCellKind(c) == CellKind.Destructible)
                    .OrderBy(c => Map.GetCellKind(c) != CellKind.Empty)
                    .First();
                Call("Explode", neighbor);
                Check(Map.ExitDoor.IsRevealed && Map.ExitDoor.gameObject.activeSelf && Map.IsWalkable(doorCell), "Blast uncovers the exit");
                Check(PendingWaves == 0, "Uncovering blast releases no enemies");
                Teleport(doorCell);
                phase = 10;
                // Let the uncovering blast's flames expire before checking where the next blast reaches.
                deadline = EditorApplication.timeSinceStartup + 1.6;
            }
            else if (phase == 10 && EditorApplication.timeSinceStartup >= deadline)
            {
                Check(!game.IsPaused && !GameObject.Find("Transition Canvas").activeSelf, "Stage card fades away after its hold");
                phase = 1;
                deadline = EditorApplication.timeSinceStartup + 0.1;
            }
            else if (phase == 1 && EditorApplication.timeSinceStartup >= deadline)
            {
                Check(!Map.ExitDoor.IsOpen && !game.IsStageClearing && game.HasLivingPlayer, "Locked exit ignores the player");
                Vector2Int[] corners = { new(1, 1), new(7, 5) };
                Teleport(corners.OrderByDescending(c => Mathf.Abs(c.x - doorCell.x) + Mathf.Abs(c.y - doorCell.y)).First());
                Vector2Int beyond = doorCell + (doorCell - neighbor);
                var cells = (Dictionary<Vector2Int, CellKind>)typeof(BombermanMap).GetField("cells", Private).GetValue(Map);
                cells[beyond] = CellKind.Empty;
                Call("Explode", neighbor);
                Check(PendingWaves == 1, "Blast on the revealed exit starts a wave");
                Check(GameObject.Find($"Explosion {doorCell.x},{doorCell.y}") != null && GameObject.Find($"Explosion {beyond.x},{beyond.y}") == null, "Flames stop at the revealed exit");
                phase = 2;
            }
            else if (phase == 2 && PendingWaves == 0)
            {
                EnemyController[] living = All<EnemyController>().Where(e => !e.IsDead).ToArray();
                Check(living.Length == 10 && living.Count(e => e is OnealEnemy) == 9, "Wave fills the stage to 10 with the stage exit enemy");
                Check(!Map.ExitDoor.IsOpen, "Exit stays locked while enemies live");
                foreach (EnemyController enemy in living) enemy.Die();
                phase = 3;
                deadline = EditorApplication.timeSinceStartup + 0.1;
            }
            else if (phase == 3 && EditorApplication.timeSinceStartup >= deadline)
            {
                Check(Map.ExitDoor.IsOpen, "Exit opens after the last enemy dies");
                scoreBeforeTransition = game.Score;
                livesBeforeTransition = (int)typeof(BombermanPrototype).GetField("livesLeft", Private).GetValue(game);
                typeof(BombermanPrototype).GetField("timeLeft", Private).SetValue(game, 3f);
                Teleport(doorCell);
                phase = 4;
                deadline = EditorApplication.timeSinceStartup + 0.1;
            }
            else if (phase == 4 && EditorApplication.timeSinceStartup >= deadline)
            {
                Check(manager.CurrentStage == 1 && !game.IsStageClearing && game.HasLivingPlayer, "Standing on an open exit alone does not advance");
                game.TryDropBomb();
                Check(manager.CurrentStage == 2 && !game.IsStageClearing && game.HasLivingPlayer, "Space on the open exit loads the next stage");
                Check(All<Bomb>().Length == 0 && game.IsPaused && TransitionCard("STAGE 2"), "Exit entry drops no bomb and shows the next stage card");
                Check(game.Score > scoreBeforeTransition, "Stage-clear time bonus is awarded");
                Check((int)typeof(BombermanPrototype).GetField("livesLeft", Private).GetValue(game) == livesBeforeTransition + 1, "Stage-clear life bonus is awarded");
                phase = 5;
                deadline = EditorApplication.timeSinceStartup + 0.1;
            }
            else if (phase == 5 && EditorApplication.timeSinceStartup >= deadline)
            {
                Check(manager.CurrentStage == 2 && !game.IsStageClearing && game.HasLivingPlayer, "Stage transition remains stable");
                Check(GameObject.Find("Stage Clear Canvas") == null, "Generic stage-clear banner is removed");
                typeof(BombermanPrototype).GetField("destructibleDensity", Private).SetValue(game, 0f);
                manager.LoadStage(1);
                Check(Map.ExitDoor.Cell == new Vector2Int(7, 5) && Map.ExitDoor.IsRevealed, "Open layout shows the exit in the far corner");
                typeof(BombermanPrototype).GetField("livesLeft", Private).SetValue(game, 1);
                phase = 11;
                deadline = EditorApplication.timeSinceStartup + 1.6;
            }
            else if (phase == 11 && EditorApplication.timeSinceStartup >= deadline)
            {
                Check(!game.IsPaused, "Stage card finishes before player death");
                Type cause = typeof(BombermanPrototype).GetNestedType("PlayerDeathCause", BindingFlags.NonPublic);
                Call("StartPlayerDeath", Enum.Parse(cause, "Bomb"));
                phase = 12;
                deadline = EditorApplication.timeSinceStartup + 4f;
            }
            else if (phase == 12)
            {
                if (!TransitionCard("GAME OVER"))
                {
                    Check(EditorApplication.timeSinceStartup < deadline, "Last lost life shows the Game Over card");
                    return;
                }

                Check((int)typeof(BombermanPrototype).GetField("livesLeft", Private).GetValue(game) == 0, "Game Over appears when lives reach zero");
                phase = 13;
                deadline = EditorApplication.timeSinceStartup + 0.6;
            }
            else if (phase == 13 && EditorApplication.timeSinceStartup >= deadline)
            {
                Check(Mathf.Abs(TransitionAlpha() - 1f) < 0.01f, "Game Over card fades to full black");
                phase = 14;
                deadline = EditorApplication.timeSinceStartup + 0.35;
            }
            else if (phase == 14 && EditorApplication.timeSinceStartup >= deadline)
            {
                Check(manager.CurrentStage == 1 && game.IsPaused && TransitionCard("STAGE 1"), "No-title fallback restarts with a stage card");
                Check((int)typeof(BombermanPrototype).GetField("livesLeft", Private).GetValue(game) > 0, "Fallback restores lives");

                // A sequence gives the run a last stage; clearing it ends the game instead of loading another.
                StageTheme endingTheme = ScriptableObject.CreateInstance<StageTheme>();
                StageSequence ending = ScriptableObject.CreateInstance<StageSequence>();
                ending.Phases.Add(new StageSequence.Phase { Theme = endingTheme, StageCount = 2 });
                SerializedObject managerSettings = new(manager);
                managerSettings.FindProperty("sequence").objectReferenceValue = ending;
                managerSettings.ApplyModifiedPropertiesWithoutUndo();
                manager.LoadStage(1);
                Check(manager.TotalStages == 2 && !manager.OnLastStage, "Stage 1 of 2 is not the last stage");
                manager.LoadStage(2);
                GameProgress.Save(2);
                Check(manager.OnLastStage && GameProgress.HasSave, "Stage 2 of 2 is the last stage");
                Teleport(Map.ExitDoor.Cell);
                Call("LateUpdate");
                Check(game.IsPaused && Time.timeScale == 0f && TransitionCard("STAGE 2"), "Direct stage load shows the current stage card");
                phase = 9;
                deadline = EditorApplication.timeSinceStartup + 1.6;
            }
            else if (phase == 9 && EditorApplication.timeSinceStartup >= deadline)
            {
                Check(!game.IsPaused, "Last stage card finishes before gameplay input");
                game.TryDropBomb();
                Check(TransitionCard("ALL STAGES CLEAR"), "Clearing the last stage shows the game-clear card");
                Check(!GameProgress.HasSave && GameProgress.RequestedStage == 0, "The ending clears the saved stage");
                Check(manager.CurrentStage == 2, "The ending does not load another stage");
                phase = 6;
                deadline = EditorApplication.timeSinceStartup + 0.6;
            }
            else if (phase == 6 && EditorApplication.timeSinceStartup >= deadline)
            {
                Check(Mathf.Abs(TransitionAlpha() - 1f) < 0.01f, "Game-clear card fades to full black");
                phase = 7;
                deadline = EditorApplication.timeSinceStartup + 0.4;
            }
            else if (phase == 7 && EditorApplication.timeSinceStartup >= deadline)
            {
                // With the title scene in Build Settings the ending loads it, exactly as Game Over does.
                bool atTitle = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Title";
                bool restarted = manager != null && manager.CurrentStage == 1 && TransitionCard("STAGE 1");
                Check(atTitle || restarted, "The ending hands the game to the title screen");
                Check(!GameProgress.HasSave, "Continue stays off after the ending");
                Debug.Log("EXIT_DOOR_SMOKE_PASS: stage cards, hidden exit, stable cell, uncovering blast, locked exit, flame stop, exit enemy wave to 10, opening, Space entry, rewards, game-clear card, save cleared.");
                Finish(0);
            }
        }
        catch (Exception exception) { Debug.LogException(exception); Finish(1); }
    }

    private static bool TransitionCard(string text)
    {
        GameObject canvas = GameObject.Find("Transition Canvas");
        UnityEngine.UI.Text label = canvas != null ? canvas.GetComponentInChildren<UnityEngine.UI.Text>(true) : null;
        return canvas != null && canvas.activeInHierarchy && label != null && label.text == text;
    }

    private static float TransitionAlpha()
    {
        GameObject canvas = GameObject.Find("Transition Canvas");
        CanvasGroup group = canvas != null ? canvas.GetComponent<CanvasGroup>() : null;
        return group != null ? group.alpha : 0f;
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
