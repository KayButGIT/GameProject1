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
        settings.FindProperty("stageClearDelay").floatValue = 0.3f;
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
                phase = 1;
                // Let the uncovering blast's flames expire before checking where the next blast reaches.
                deadline = Time.timeAsDouble + 1.3;
            }
            else if (phase == 1 && Time.timeAsDouble >= deadline)
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
                deadline = Time.timeAsDouble + 0.1;
            }
            else if (phase == 3 && Time.timeAsDouble >= deadline)
            {
                Check(Map.ExitDoor.IsOpen, "Exit opens after the last enemy dies");
                Teleport(doorCell);
                phase = 4;
                deadline = Time.timeAsDouble + 1.0;
            }
            else if (phase == 4)
            {
                if (!game.IsStageClearing)
                {
                    Check(Time.timeAsDouble < deadline, "Player on the open exit clears the stage");
                    return;
                }
                Type cause = typeof(BombermanPrototype).GetNestedType("PlayerDeathCause", BindingFlags.NonPublic);
                Call("StartPlayerDeath", Enum.Parse(cause, "Bomb"));
                game.TryDropBomb();
                Check(game.HasLivingPlayer && All<Bomb>().Length == 0 && manager.CurrentStage == 1, "Clearing player cannot die or drop bombs");
                phase = 5;
                deadline = Time.timeAsDouble + 0.6;
            }
            else if (phase == 5 && Time.timeAsDouble >= deadline)
            {
                Check(manager.CurrentStage == 2 && !game.IsStageClearing && game.HasLivingPlayer, "Stage clear loads the next stage");
                Check(!GameObject.Find("Stage Clear Canvas").transform.GetChild(0).gameObject.activeSelf, "Banner hidden on the new stage");
                typeof(BombermanPrototype).GetField("destructibleDensity", Private).SetValue(game, 0f);
                manager.LoadStage(1);
                Check(Map.ExitDoor.Cell == new Vector2Int(7, 5) && Map.ExitDoor.IsRevealed, "Open layout shows the exit in the far corner");
                Debug.Log("EXIT_DOOR_SMOKE_PASS: hidden exit, stable cell, uncovering blast, locked exit, flame stop, exit enemy wave to 10, opening, stage clear, safe clear, open-layout fallback.");
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
