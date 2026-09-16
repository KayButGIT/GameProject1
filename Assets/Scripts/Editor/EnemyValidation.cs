using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Runs against actual Unity components and serialized prefabs, without changing the open scene.
public static class EnemyValidation
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("Enemy validation failed: " + message);
        checks++;
    }
    private static object Call(object target, string method, params object[] args) => typeof(EnemyController).GetMethod(method, Private).Invoke(target, args);
    private static void Set(object target, Type owner, string field, object value) => owner.GetField(field, Private).SetValue(target, value);
    private static T Get<T>(object target, Type owner, string field) => (T)owner.GetField(field, Private).GetValue(target);

    [MenuItem("Tools/Bomberman/Validate Enemy Assets and Behaviors")]
    public static void Run()
    {
        checks = 0;
        UnityEngine.Random.State randomState = UnityEngine.Random.state;
        var scene = EditorSceneManager.NewPreviewScene();
        GameObject host = new("Enemy test host");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(host, scene);
        BombermanPrototype game = host.AddComponent<BombermanPrototype>();
        BombermanMap map = new(15, 15, 0f, 1, null);
        Dictionary<Vector2Int, CellKind> cells = Get<Dictionary<Vector2Int, CellKind>>(map, typeof(BombermanMap), "cells");
        for (int x = 0; x < 15; x++) for (int y = 0; y < 15; y++)
            cells[new Vector2Int(x, y)] = x == 0 || y == 0 || x == 14 || y == 14 ? CellKind.Solid : CellKind.Empty;
        GameObject playerObject = new("Test player"); playerObject.transform.SetParent(host.transform);
        PlayerController player = playerObject.AddComponent<PlayerController>();
        Set(game, typeof(BombermanPrototype), "map", map);
        Set(game, typeof(BombermanPrototype), "player", player);
        string[] names = { "Valcom", "O'neal", "Dahl", "Minuo", "Ovape", "Doria", "Pass", "Pontan" };
        float[] speeds = { 2f, 3.5f, 3.5f, 4f, 1.5f, 1f, 4.5f, 5.5f };
        try
        {
            for (int i = 0; i < names.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyAssetBuilder.PrefabFolder + "/" + names[i] + ".prefab");
                Check(prefab != null, names[i] + " prefab exists");
                GameObject instance = UnityEngine.Object.Instantiate(prefab, host.transform);
                EnemyController enemy = instance.GetComponent<EnemyController>();
                enemy.transform.position = map.CellToWorld(new Vector2Int(5, 5));
                game.RegisterEnemy(enemy); game.RegisterEnemy(enemy);
                Check(Get<List<GridController>>(game, typeof(BombermanPrototype), "actors").FindAll(a => a == enemy).Count == 1, "registration is idempotent");
                Check(enemy.MoveSpeed == speeds[i], names[i] + " speed");
                Check(instance.transform.Find("VisualRoot/Model") != null && enemy.ModelAnimator != null, "replaceable animated model");
                Check(!enemy.ModelAnimator.applyRootMotion, "root motion disabled");
                Check(enemy.ModelAnimator.runtimeAnimatorController is AnimatorOverrideController, "per-enemy override controller");
                Check(enemy.ModelAnimator.runtimeAnimatorController.animationClips.Length == 3, "three placeholder clips");
                Check(enemy.CanPassDestructibleWalls == (i == 4 || i == 5 || i == 7), "wall-pass species");
                Vector2Int obstacle = new(6, 5);
                cells[obstacle] = CellKind.Destructible;
                Check(game.CanEnemyTraverse(enemy, obstacle) == enemy.CanPassDestructibleWalls, "soft wall movement");
                cells[obstacle] = CellKind.Solid;
                Check(!game.CanEnemyTraverse(enemy, obstacle), "solid wall movement");
                Check(!game.CanEnemyTraverse(enemy, new Vector2Int(-1, 5)), "out of bounds");
                cells[obstacle] = CellKind.Empty;
                var bombs = Get<Dictionary<Vector2Int, Bomb>>(game, typeof(BombermanPrototype), "bombs");
                bombs[obstacle] = null;
                Check(!game.CanEnemyTraverse(enemy, obstacle), "bomb movement");
                bombs.Clear();
                player.Cell = new Vector2Int(7, 5);
                if (i == 4)
                {
                    Check(enemy.AcquisitionProbability == 0.2f && enemy.DetectionRange == 4, "Ovape acquisition defaults");
                    // Exercise both sides of the 20% threshold without statistical assertions.
                    int rejectedSeed = SeedForAcquisition(false);
                    int acceptedSeed = SeedForAcquisition(true);
                    UnityEngine.Random.InitState(rejectedSeed);
                    Call(enemy, "UpdateChase", 0f);
                    Check(!enemy.IsChasing, "Ovape can ignore visible player");
                    UnityEngine.Random.InitState(acceptedSeed);
                    Call(enemy, "UpdateChase", 0f);
                    Check(!enemy.IsChasing, "no repeated roll while waiting at the same center");
                    Set(enemy, typeof(EnemyController), "acquisitionChecked", false);
                    enemy.StartMove(enemy.Cell, enemy.transform.position);
                    Call(enemy, "UpdateChase", 0f);
                    Check(!enemy.IsChasing, "no acquisition between centers");
                    Set(enemy, typeof(GridController), "moveProgress", 1f);
                    // Finish the artificial move with a zero chance, then test a fresh encounter below.
                    enemy.AcquisitionProbability = 0f;
                    Call(enemy, "Update");
                    Check(!enemy.IsChasing, "zero acquisition chance");
                    typeof(GridController).GetProperty("IsMoving").SetValue(enemy, false);
                    enemy.Cell = new Vector2Int(5, 5);
                    enemy.transform.position = map.CellToWorld(enemy.Cell);
                    enemy.AcquisitionProbability = 0.2f;
                    Set(enemy, typeof(EnemyController), "acquisitionChecked", false);
                    UnityEngine.Random.InitState(acceptedSeed);
                }
                if (enemy.Chase != EnemyController.ChaseMode.None)
                {
                    Check(enemy.CanSeePlayer(), "aligned player detected");
                    cells[obstacle] = CellKind.Solid;
                    Check(!enemy.CanSeePlayer(), "hard wall occludes sight");
                    cells[obstacle] = CellKind.Destructible;
                    Check(enemy.CanSeePlayer() == enemy.CanPassDestructibleWalls, "soft wall sight");
                    cells[obstacle] = CellKind.Empty;
                    bombs[obstacle] = null;
                    Check(!enemy.CanSeePlayer(), "bomb occludes sight"); bombs.Clear();
                    if (i == 7)
                    {
                        player.Cell = new Vector2Int(13, 13);
                        Check(!enemy.CanSeePlayer(), "Pontan target outside sight range");
                        Call(enemy, "UpdateChase", 0f);
                        Check(enemy.IsChasing, "Pontan starts pursuit without sight");
                        player.Cell = new Vector2Int(7, 5);
                    }
                    Call(enemy, "UpdateChase", 0f);
                    Check(enemy.IsChasing, "starts chase");
                    Check((Vector2Int)Call(enemy, "ChooseChaseDirection") == Vector2Int.right, "chase reduces distance");
                    if (enemy.Chase == EnemyController.ChaseMode.Timed)
                    {
                        float duration = Get<float>(enemy, typeof(EnemyController), "chaseRemaining");
                        Check(duration >= enemy.ChaseMinSeconds && duration <= enemy.ChaseMaxSeconds, "configured chase duration");
                        if (i == 4) Check(duration == 2f && enemy.DetectionCooldown == 3f, "Ovape timing defaults");
                        Call(enemy, "UpdateChase", duration - 0.01f); Check(enemy.IsChasing, "timed chase still active");
                        Call(enemy, "UpdateChase", 0.02f); Check(!enemy.IsChasing, "timed chase expires");
                        Call(enemy, "UpdateChase", enemy.DetectionCooldown - 0.1f); Check(!enemy.IsChasing, "cooldown blocks reacquisition");
                        if (i == 4) UnityEngine.Random.InitState(SeedForAcquisition(true));
                        Call(enemy, "UpdateChase", 0.11f); Check(enemy.IsChasing, "reacquires after cooldown");
                    }
                    else if (i >= 6)
                    {
                        player.Cell = new Vector2Int(13, 13);
                        Call(enemy, "UpdateChase", 100f);
                        Check(enemy.IsChasing, "persistent pursuit survives unseen timeout");
                        Check(Get<Vector2Int>(enemy, typeof(EnemyController), "lastSeen") == player.Cell, "persistent pursuit targets current player cell");
                        Set(game, typeof(BombermanPrototype), "player", null);
                        Call(enemy, "UpdateChase", 0f);
                        Check(!enemy.IsChasing, "pursuit ends without living player");
                        Set(game, typeof(BombermanPrototype), "player", player);
                    }
                    else
                    {
                        player.Cell = new Vector2Int(7, 6);
                        Call(enemy, "UpdateChase", enemy.MemorySeconds - 0.01f); Check(enemy.IsChasing, "last-seen memory retained");
                        Call(enemy, "UpdateChase", 0.02f); Check(!enemy.IsChasing, "unseen chase expires");
                    }
                }
                else { Call(enemy, "UpdateChase", 10f); Check(!enemy.IsChasing, "patrol species never chase"); }
                if (i == 5)
                {
                    Check(enemy.AvoidBombLanes && enemy.DetectionRange == 6 && enemy.MemorySeconds == 3f, "Doria defaults");
                    ValidateBombAvoidance(game, map, cells, bombs, enemy, host.transform);
                }
                if (enemy.Chase == EnemyController.ChaseMode.Escape)
                {
                    player.Cell = new Vector2Int(7, 5);
                    Call(enemy, "UpdateChase", 0f);
                    enemy.DetectionRange = 12;
                    player.Cell = new Vector2Int(13, 5);
                    Call(enemy, "UpdateChase", 1.99f);
                    Check(enemy.IsChasing, "escape range requires sustained timeout");
                    Call(enemy, "UpdateChase", 0.02f);
                    Check(!enemy.IsChasing, "escape range ends visible chase after two seconds");
                }
                Set(enemy, typeof(EnemyController), "heading", Vector2Int.up);
                Set(enemy, typeof(EnemyController), "patrolRemaining", 1);
                Check((Vector2Int)Call(enemy, "ChoosePatrolDirection") == Vector2Int.up, "continues patrol axis");
                cells[new Vector2Int(5, 6)] = CellKind.Solid;
                Check((Vector2Int)Call(enemy, "ChoosePatrolDirection") == Vector2Int.down, "reverses blocked patrol");
                foreach (Vector2Int d in BombermanPrototype.Directions) cells[enemy.Cell + d] = CellKind.Solid;
                Check((Vector2Int)Call(enemy, "ChoosePatrolDirection") == Vector2Int.zero, "trapped waits");
                cells[new Vector2Int(6, 5)] = CellKind.Empty;
                Check((Vector2Int)Call(enemy, "ChoosePatrolDirection") == Vector2Int.right, "trapped recovers");
                foreach (Vector2Int d in BombermanPrototype.Directions) cells[enemy.Cell + d] = CellKind.Empty;
                enemy.AxisChangeChance = 1f;
                Set(enemy, typeof(EnemyController), "patrolRemaining", 0);
                Check(((Vector2Int)Call(enemy, "ChoosePatrolDirection")).x != 0, "axis change after completed run");
                if (i == 2) Check(enemy.PatrolMinCells == 6 && enemy.PatrolMaxCells == 10, "Dahl long run");
                Set(game, typeof(BombermanPrototype), "paused", true);
                Vector3 before = enemy.transform.position;
                Call(enemy, "Update"); Check(enemy.transform.position == before && enemy.ModelAnimator.speed == 0f, "paused movement and animation");
                Set(game, typeof(BombermanPrototype), "paused", false);
                enemy.Die(); enemy.Die();
                Check(enemy.IsDead && !enemy.IsMoving && !enemy.IsChasing && enemy != null, "death immediate gameplay stop, delayed removal");
                UnityEngine.Object.DestroyImmediate(instance);
            }
            Debug.Log($"ENEMY VALIDATION PASSED: {checks} checks");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            UnityEngine.Random.state = randomState;
        }
    }

    private static int SeedForAcquisition(bool accepted)
    {
        for (int seed = 0; seed < 1000; seed++)
        {
            UnityEngine.Random.InitState(seed);
            if ((UnityEngine.Random.value < 0.2f) == accepted) return seed;
        }
        throw new Exception("Unable to select deterministic random seed.");
    }

    private static void ValidateBombAvoidance(BombermanPrototype game, BombermanMap map,
        Dictionary<Vector2Int, CellKind> cells, Dictionary<Vector2Int, Bomb> bombs, EnemyController enemy, Transform parent)
    {
        void BombAt(Vector2Int cell)
        {
            GameObject obj = new("Test bomb"); obj.transform.SetParent(parent);
            Bomb bomb = obj.AddComponent<Bomb>(); bomb.Cell = cell; bombs[cell] = bomb;
        }
        Set(game, typeof(BombermanPrototype), "blastRange", 3);
        BombAt(new Vector2Int(5, 5));
        Check(game.IsCellThreatenedByBomb(new Vector2Int(5, 5)), "bomb origin threatened");
        foreach (Vector2Int direction in BombermanPrototype.Directions)
        {
            Vector2Int near = new Vector2Int(5, 5) + direction;
            Vector2Int wall = near + direction;
            Vector2Int beyond = wall + direction;
            cells[wall] = CellKind.Destructible;
            Check(game.IsCellThreatenedByBomb(near) && game.IsCellThreatenedByBomb(wall), "blast includes first soft wall");
            Check(!game.IsCellThreatenedByBomb(beyond), "soft wall shields beyond");
            Check(map.GetCellKind(wall) == CellKind.Destructible && bombs.Count == 1, "threat query is read-only");
            cells[wall] = CellKind.Solid;
            Check(!game.IsCellThreatenedByBomb(wall) && !game.IsCellThreatenedByBomb(beyond), "hard wall shields itself and beyond");
            cells[wall] = CellKind.Empty;
            Check(game.IsCellThreatenedByBomb(beyond) && !game.IsCellThreatenedByBomb(beyond + direction), "blast range boundary");
        }
        Check(!game.IsCellThreatenedByBomb(new Vector2Int(6, 6)), "diagonals safe");
        bombs.Clear(); BombAt(new Vector2Int(1, 1));
        Check(!game.IsCellThreatenedByBomb(new Vector2Int(-1, 1)), "map bounds stop blast");
        bombs.Clear(); Set(game, typeof(BombermanPrototype), "blastRange", 1);
        BombAt(new Vector2Int(7, 5));
        Set(enemy, typeof(EnemyController), "heading", Vector2Int.right);
        Vector2Int choice = (Vector2Int)Call(enemy, "ChooseDirection");
        Check(choice != Vector2Int.zero && !game.IsCellThreatenedByBomb(enemy.Cell + choice), "Doria prefers safe neighbor");
        // Four bombs threaten all adjacent cells, but do not reach Doria's current cell.
        BombAt(new Vector2Int(3, 5)); BombAt(new Vector2Int(5, 3)); BombAt(new Vector2Int(5, 7));
        Check((Vector2Int)Call(enemy, "ChooseDirection") == Vector2Int.zero, "Doria waits on safe cell when neighbors threatened");
        bombs.Clear(); Set(game, typeof(BombermanPrototype), "blastRange", 3);
        BombAt(new Vector2Int(2, 5)); BombAt(new Vector2Int(8, 5)); BombAt(new Vector2Int(5, 2)); BombAt(new Vector2Int(5, 8));
        Check(game.IsCellThreatenedByBomb(enemy.Cell), "fallback starts on threatened cell");
        Check((Vector2Int)Call(enemy, "ChooseDirection") == Vector2Int.right, "Doria uses normal movement if all choices threatened");
        bombs.Clear(); Set(game, typeof(BombermanPrototype), "blastRange", 1);
        Check((Vector2Int)Call(enemy, "ChooseDirection") == Vector2Int.right, "avoidance filter does not leak between decisions");
    }
}

