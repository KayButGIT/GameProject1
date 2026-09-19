using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StageEnemiesPlaySmoke
{
    private const string Running = "Bomberman.StageEnemiesSmoke";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static BombermanPrototype game;
    private static StageManager manager;
    private static int phase;
    private static double deadline, watchdog;
    private static bool failed;

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run in an isolated batch project.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        game = new GameObject("Stage Enemies Smoke Game").AddComponent<BombermanPrototype>();
        SerializedObject settings = new(game);
        settings.FindProperty("width").intValue = 15;
        settings.FindProperty("height").intValue = 11;
        settings.FindProperty("randomSeed").intValue = 123;
        settings.FindProperty("spawnEnemies").boolValue = true;
        settings.FindProperty("useOriginalStageEnemies").boolValue = true;
        settings.FindProperty("playerCanDieFromBomb").boolValue = false;
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { EnemyAssetBuilder.PrefabFolder });
        SerializedProperty prefabs = settings.FindProperty("enemyPrefabs");
        prefabs.arraySize = guids.Length;
        for (int i = 0; i < guids.Length; i++)
            prefabs.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i])).GetComponent<EnemyController>();
        settings.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(game.gameObject.scene, "Assets/StageEnemiesSmoke.unity");
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
        if (!condition) throw new Exception("Stage enemies smoke: " + message);
    }
    private static object Field(string name) => typeof(BombermanPrototype).GetField(name, Private).GetValue(game);
    private static BombermanMap Map => (BombermanMap)Field("map");
    private static EnemyController[] Living => ((List<GridController>)Field("actors")).OfType<EnemyController>().Where(e => e != null && !e.IsDead).ToArray();
    private static int Count<T>() => Living.Count(e => e is T);
    private static string HudText => GameObject.Find("Stage HUD Canvas").GetComponentInChildren<UnityEngine.UI.Text>().text;

    private static void CheckFinalStageRoster(string message)
    {
        Check(Living.Length == 10 && Count<DoriaEnemy>() == 2 && Count<OvapeEnemy>() == 1 && Count<PassEnemy>() == 5 && Count<PontanEnemy>() == 2, message);
        Check((Type)Field("exitWaveEnemyType") == typeof(PontanEnemy), message + " exit enemy");
    }

    private static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying) return;
            if (failed) throw new Exception("Unexpected runtime error.");
            if (EditorApplication.timeSinceStartup > watchdog) throw new Exception("Stage enemies smoke timed out.");
            if (phase == 0)
            {
                game = UnityEngine.Object.FindFirstObjectByType<BombermanPrototype>();
                if (game == null || !game.IsReady) return;
                manager = game.GetComponent<StageManager>();
                EnemyController[] enemies = Living;
                Check(enemies.Length == 6 && enemies.All(e => e is ValcomEnemy), "Stage 1 spawns six Valcoms");
                Check(enemies.All(e => e.Cell.x >= 5 && Map.GetCellKind(e.Cell) == CellKind.Empty), "Stage enemies start on empty cells from column 5");
                Check(enemies.Select(e => e.Cell).Distinct().Count() == enemies.Length, "Stage enemies start on separate cells");
                Check(HudText == "TIME 200", "HUD shows the stage time");
                Check((Type)Field("exitWaveEnemyType") == typeof(OnealEnemy), "Stage 1 exit enemy");
                manager.LoadStage(35);
                Check(Living.Length == 9 && Count<DahlEnemy>() == 2 && Count<MinuoEnemy>() == 1 && Count<DoriaEnemy>() == 1 && Count<OvapeEnemy>() == 3 && Count<PassEnemy>() == 2, "Stage 35 roster skips the empty slot");
                manager.LoadStage(50);
                CheckFinalStageRoster("Stage 50 roster");
                manager.LoadStage(51);
                CheckFinalStageRoster("Later stages repeat stage 50");
                typeof(BombermanPrototype).GetField("stageTimeLimit", Private).SetValue(game, 0.3f);
                manager.LoadStage(1);
                phase = 1;
                deadline = Time.timeAsDouble + 3.0;
            }
            else if (phase == 1)
            {
                if (!(bool)Field("timeUp"))
                {
                    Check(Time.timeAsDouble < deadline, "Stage timer runs out");
                    return;
                }
                EnemyController[] pontans = Living;
                Check(pontans.Length == 10 && pontans.All(e => e is PontanEnemy), "Time-out replaces every enemy with ten Pontans");
                Check(pontans.All(e => (e.Cell.x >= 3 || e.Cell.y >= 3) && Map.GetCellKind(e.Cell) == CellKind.Empty), "Time-out Pontans avoid walls and the start corner");
                Check(HudText == "TIME 0", "HUD shows zero after time-out");
                Debug.Log("STAGE_ENEMIES_SMOKE_PASS: stage 1 roster, spawn cells, HUD, exit enemy table, empty slots, stage 50 roster, repeat after 50, time-out Pontans.");
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
