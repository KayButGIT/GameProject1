using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class StageEnemiesPlaySmoke
{
    private const string Running = "Bomberman.StageEnemiesSmoke";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static BombermanPrototype game;
    private static StageManager manager;
    private static int phase;
    private static double deadline, watchdog;
    private static double lastExitSpawnTime;
    private static int lastExitEnemyCount;
    private static int staggeredExitSpawnCount;
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
        settings.FindProperty("playerBodyNormalMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Body_Normal.mat");
        settings.FindProperty("playerBodyInvincibleMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Body_Invincible.mat");
        settings.FindProperty("playerFaceNormalMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Face_Normal.mat");
        settings.FindProperty("playerFaceBlinkMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Face_Blink.mat");
        settings.FindProperty("playerFaceInvincibleMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Face_Invincible.mat");
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
    private static void SetField(string name, object value) => typeof(BombermanPrototype).GetField(name, Private).SetValue(game, value);
    private static object Invoke(string name, params object[] arguments) => typeof(BombermanPrototype).GetMethod(name, Private).Invoke(game, arguments);
    private static BombermanMap Map => (BombermanMap)Field("map");
    private static EnemyController[] Living => ((List<GridController>)Field("actors")).OfType<EnemyController>().Where(e => e != null && !e.IsDead).ToArray();
    private static int Count<T>() => Living.Count(e => e is T);
    private static string HudText => GameObject.Find("Stage HUD Canvas").GetComponentInChildren<UnityEngine.UI.Text>().text;

    private static void CheckFinalStageRoster(string message)
    {
        Check(Living.Length == 10 && Count<OvapeEnemy>() == 2 && Count<DoriaEnemy>() == 1 && Count<PassEnemy>() == 5 && Count<PontanEnemy>() == 2, message);
        Check((Type)Field("exitWaveEnemyType") == typeof(PontanEnemy), message + " exit enemy");
    }

    private static void CheckDifficultyMapping()
    {
        Check(OriginalStageEnemies.ResolveReferenceStage(1, 1) == 1, "One-stage campaign uses NES Stage 1");
        Check(OriginalStageEnemies.ResolveReferenceStage(1, 30) == 1, "Finite campaign begins at NES Stage 1");
        Check(OriginalStageEnemies.ResolveReferenceStage(15, 30) == 19, "30-stage campaign eases into the reference curve");
        Check(OriginalStageEnemies.ResolveReferenceStage(30, 30) == 50, "Finite campaign ends at NES Stage 50");
        Check(OriginalStageEnemies.ResolveReferenceStage(50, 50) == 50, "50-stage campaign maps its end to NES Stage 50");
        Check(OriginalStageEnemies.ResolveReferenceStage(50, 0) == 50, "Endless campaign preserves the original first 50 stages");
        for (int stage = 51; stage <= 60; stage++)
            Check(OriginalStageEnemies.ResolveReferenceStage(stage, 0) == stage - 10, "Endless campaign cycles NES Stages 41-50");

        Check(OriginalStagePowerUps.GetForStage(1) == PowerUpType.FireUp, "NES Stage 1 fixed Fire Up");
        Check(OriginalStagePowerUps.GetForStage(26) == PowerUpType.Mystery, "NES Stage 26 fixed Mystery");
        Check(OriginalStagePowerUps.GetForStage(50) == PowerUpType.Mystery, "NES Stage 50 fixed Mystery");
        Check(OriginalStagePowerUps.GetForStage(30, 30) == PowerUpType.Mystery, "30-stage campaign maps its final pickup to NES Stage 50");
        Check(OriginalStagePowerUps.GetForStage(51) == PowerUpType.Detonator, "Endless Stage 51 pickup cycles to NES Stage 41");

        PowerUpType[] expected =
        {
            PowerUpType.FireUp, PowerUpType.BombUp, PowerUpType.Detonator, PowerUpType.SpeedUp, PowerUpType.BombUp,
            PowerUpType.BombUp, PowerUpType.FireUp, PowerUpType.Detonator, PowerUpType.BombPass, PowerUpType.WallPass,
            PowerUpType.BombUp, PowerUpType.BombUp, PowerUpType.Detonator, PowerUpType.BombPass, PowerUpType.FireUp,
            PowerUpType.WallPass, PowerUpType.BombUp, PowerUpType.BombPass, PowerUpType.BombUp, PowerUpType.Detonator,
            PowerUpType.BombPass, PowerUpType.Detonator, PowerUpType.BombUp, PowerUpType.Detonator, PowerUpType.BombPass,
            PowerUpType.Mystery, PowerUpType.FireUp, PowerUpType.BombUp, PowerUpType.Detonator, PowerUpType.FlamePass,
            PowerUpType.WallPass, PowerUpType.BombUp, PowerUpType.Detonator, PowerUpType.Mystery, PowerUpType.BombPass,
            PowerUpType.FlamePass, PowerUpType.Detonator, PowerUpType.FireUp, PowerUpType.WallPass, PowerUpType.Mystery,
            PowerUpType.Detonator, PowerUpType.WallPass, PowerUpType.BombPass, PowerUpType.Detonator, PowerUpType.Mystery,
            PowerUpType.WallPass, PowerUpType.BombPass, PowerUpType.Detonator, PowerUpType.FlamePass, PowerUpType.Mystery
        };
        for (int stage = 1; stage <= expected.Length; stage++)
            Check(OriginalStagePowerUps.GetForStage(stage) == expected[stage - 1], $"NES Stage {stage} fixed pickup matches original table");
    }

    private static void CheckPowerUpAndCheatRules()
    {
        Check(GameObject.Find("Open Cheat Menu") != null, "Optional HUD cheat button is created when enabled");
        RectTransform panelRect = GameObject.Find("Cheat Panel").GetComponent<RectTransform>();
        RectTransform titleRect = GameObject.Find("Cheat Menu Title").GetComponent<RectTransform>();
        Check(panelRect.sizeDelta == new Vector2(700f, 700f), "Cheat panel fits the stage selector and power-up controls");
        Check(Mathf.Approximately(titleRect.anchorMin.x, 0.5f) && Mathf.Approximately(titleRect.anchorMax.x, 0.5f),
            "Cheat title is centered within the panel");
        game.OpenCheatMenu();
        Check(game.IsPaused && GameObject.Find("Cheat Panel").activeSelf, "Cheat button opens a pausing panel");
        InputField stageInput = GameObject.Find("Stage Number Input").GetComponent<InputField>();
        Check(stageInput.contentType == InputField.ContentType.IntegerNumber && GameObject.Find("Go To Stage Cheat") != null,
            "Cheat panel provides a numeric stage field and Go button");
        Check(GameObject.Find("Reveal Power Up Location Cheat") != null, "Cheat panel provides the reveal-location action");
        game.CloseCheatMenu();
        Check(!game.IsPaused && !GameObject.Find("Cheat Panel").activeSelf, "Closing cheat panel restores gameplay");
        Check(!game.CheatGoToStage(0, out _) && manager.CurrentStage == 1, "Stage selector rejects stage zero");
        Check(game.CheatGoToStage(2, out _) && manager.CurrentStage == 2, "Stage selector loads the requested stage");
        manager.LoadStage(1);
        int revealScore = game.Score;
        Check(game.CheatRevealPowerUpLocation() && GameObject.Find("Cheat Hidden Power-up Marker") != null,
            "Reveal cheat marks the hidden item's location");
        Check((bool)Field("hasHiddenPowerUp") && ((List<PowerUp>)Field("powerUps")).Count == 0 && game.Score == revealScore,
            "Reveal marker does not uncover or collect the item");
        manager.LoadStage(1);

        int scoreBeforeCheats = game.Score;
        game.CheatGrantAllPowerUps();
        Check(game.PowerUps.bombUps == 9 && game.PowerUps.fireUps == 4 && game.PowerUps.speedUps == 1,
            "All Power-Ups fills permanent upgrades to NES caps");
        Check(game.PowerUps.cheatBombUps == 9 && game.PowerUps.cheatFireUps == 4 && game.PowerUps.cheatSpeedUps == 1,
            "All Power-Ups records which permanent levels came from the cheat");
        Check(game.PowerUps.detonator && game.PowerUps.bombPass && game.PowerUps.wallPass && game.PowerUps.flamePass,
            "All Power-Ups grants each special ability");
        Check(game.Score == scoreBeforeCheats, "Cheat grants award no pickup score");
        Invoke("ResetPowerUps");

        int scoreBeforePickup = game.Score;
        Invoke("ApplyPowerUp", PowerUpType.BombUp, true);
        Check(game.Score == scoreBeforePickup + 1000, "Ordinary power-up pickup awards 1,000 points");
        Invoke("ResetPowerUps");

        for (int i = 0; i < 16; i++) Invoke("ApplyPowerUp", PowerUpType.BombUp, false);
        for (int i = 0; i < 8; i++) Invoke("ApplyPowerUp", PowerUpType.FireUp, false);
        for (int i = 0; i < 5; i++) Invoke("ApplyPowerUp", PowerUpType.SpeedUp, false);
        Check((int)Field("maxBombs") == 10 && (int)Field("blastRange") == 5 && game.PowerUps.speedUps == 1,
            "Repeated pickups stop at 10 bombs, range 5, and one Speed Up");
        float baseSpeed = (float)Field("playerMoveSpeed");
        float baseAcceleration = (float)Field("playerAcceleration");
        float baseDeceleration = (float)Field("playerDeceleration");
        Check(Mathf.Approximately((float)Invoke("GetPlayerMoveSpeed"), baseSpeed * 2f), "Speed Up doubles base movement speed");
        Check(Mathf.Approximately(((PlayerController)Field("player")).MoveSpeed, baseSpeed * 2f), "Speed Up applies doubled movement speed to the player");
        Check(Mathf.Approximately(((PlayerController)Field("player")).FreeMoveAcceleration, baseAcceleration * 2f), "Speed Up scales acceleration so the boost ramps up at the same rate");
        Check(Mathf.Approximately(((PlayerController)Field("player")).FreeMoveDeceleration, baseDeceleration * 2f), "Speed Up scales deceleration so braking remains proportional");
        SoundManager soundManager = SoundManager.Instance;
        Check(soundManager != null, "Footstep test can use the shared SoundManager");
        soundManager.PlayFootstep(game.CurrentStagePhase, 1f);
        AudioSource footstepSource = (AudioSource)typeof(SoundManager).GetField("footstepSource", Private).GetValue(soundManager);
        float normalFootstepPitch = footstepSource.pitch;
        soundManager.PlayFootstep(game.CurrentStagePhase, 2f);
        Check(footstepSource.pitch > normalFootstepPitch, "Speed multiplier raises footstep pitch while preserving pitch variation");
        MethodInfo walkCycleMethod = typeof(PlayerAnimationBridge).GetMethod("CalculateWalkCycleSpeed", Private | BindingFlags.Static);
        float normalWalkCycleSpeed = (float)walkCycleMethod.Invoke(null, new object[] { baseSpeed, baseSpeed, 1f });
        float boostedWalkCycleSpeed = (float)walkCycleMethod.Invoke(null, new object[] { baseSpeed * 2f, baseSpeed * 2f, game.PlayerSpeedMultiplier });
        Check(Mathf.Approximately(boostedWalkCycleSpeed, normalWalkCycleSpeed * 2f),
            "Speed Up doubles animation footstep cadence to match player movement");
        Invoke("ResetPowerUps");
        Check(Mathf.Approximately(((PlayerController)Field("player")).MoveSpeed, baseSpeed), "Reset restores base movement speed");
        Check(Mathf.Approximately(((PlayerController)Field("player")).FreeMoveAcceleration, baseAcceleration), "Reset restores base movement acceleration");
        Check(Mathf.Approximately(((PlayerController)Field("player")).FreeMoveDeceleration, baseDeceleration), "Reset restores base movement deceleration");

        Invoke("ApplyPowerUp", PowerUpType.Detonator, false);
        game.TryDropBomb();
        Dictionary<Vector2Int, Bomb> placedBombs = (Dictionary<Vector2Int, Bomb>)Field("bombs");
        Check(placedBombs.Count == 1, "Player can place a bomb with Detonator");
        Bomb testBomb = placedBombs.Values.Single();
        typeof(Bomb).GetField("blocksMovement", Private).SetValue(testBomb, true);
        Check(game.IsBombBlockingCell(testBomb.Cell), "Bomb blocks the player before Bomb Pass");
        Invoke("ApplyPowerUp", PowerUpType.BombPass, false);
        Check(!game.IsBombBlockingCell(testBomb.Cell), "Bomb Pass lets the player walk through bombs");
        Vector2Int secondBombCell = new(-1, -1);
        for (int x = 1; x < 14 && secondBombCell.x < 0; x++)
        for (int y = 1; y < 10; y++)
        {
            Vector2Int candidate = new(x, y);
            if (Map.IsWalkable(candidate) && candidate != testBomb.Cell && !Living.Any(enemy => enemy.Cell == candidate))
            {
                secondBombCell = candidate;
                break;
            }
        }
        Check(secondBombCell.x >= 0, "Find an empty cell for a second detonator test bomb");
        GameObject secondBombObject = new("Second Detonator Test Bomb");
        secondBombObject.transform.SetParent(((GameObject)Field("stageRoot")).transform, true);
        secondBombObject.transform.position = Map.CellToWorld(secondBombCell);
        Bomb secondBomb = secondBombObject.AddComponent<Bomb>();
        secondBomb.Initialize(secondBombCell, ((PlayerController)Field("player")).transform, 0f);
        placedBombs.Add(secondBombCell, secondBomb);
        Invoke("DetonateAllBombs");
        Check(testBomb.DetonateRequested && secondBomb.DetonateRequested, "One Detonator press requests every placed bomb together");

        Invoke("ApplyPowerUp", PowerUpType.WallPass, false);
        Check(((PlayerController)Field("player")).CanPassDestructibleWalls, "Wall Pass allows passage through destructible walls");
        SetField("playerCanDieFromBomb", true);
        Invoke("ApplyPowerUp", PowerUpType.FlamePass, false);
        Invoke("DamageActorsAt", game.PlayerCell);
        Check(game.HasLivingPlayer, "Flame Pass protects the player from their own bomb blast");
        SetField("playerCanDieFromBomb", false);
        Invoke("ResetPowerUps");

        game.CheatGrantPowerUp(PowerUpType.Mystery);
        Check(Mathf.Approximately((float)Field("mysteryTimeLeft"), 35f), "Mystery grants the original 35-second invincibility duration");
        Check(game.IsInvincible, "Mystery protects against enemy contact and bomb damage");
        PlayerController mysteryPlayer = (PlayerController)Field("player");
        SetField("playerCanDieFromBomb", true);
        Invoke("DamageActorsAt", game.PlayerCell);
        Check(!mysteryPlayer.IsDead, "Mystery prevents bomb damage even when ordinary bombs are lethal");
        EnemyController contactEnemy = Living.First();
        Vector2Int previousEnemyCell = contactEnemy.Cell;
        Vector3 previousEnemyPosition = contactEnemy.transform.position;
        contactEnemy.Cell = game.PlayerCell;
        contactEnemy.transform.position = mysteryPlayer.transform.position;
        Invoke("KillPlayerIfEnemyTouches");
        Check(!mysteryPlayer.IsDead, "Mystery prevents death on enemy contact");
        contactEnemy.Cell = previousEnemyCell;
        contactEnemy.transform.position = previousEnemyPosition;
        Invoke("UpdatePowerUps");
        Material bodyInvincible = (Material)typeof(PlayerController).GetField("bodyInvincibleMaterial", Private).GetValue(mysteryPlayer);
        int bodyIndex = (int)typeof(PlayerController).GetField("bodyMaterialIndex", Private).GetValue(mysteryPlayer);
        Material faceInvincible = (Material)typeof(PlayerController).GetField("faceInvincibleMaterial", Private).GetValue(mysteryPlayer);
        int faceIndex = (int)typeof(PlayerController).GetField("faceMaterialIndex", Private).GetValue(mysteryPlayer);
        Renderer faceRenderer = (Renderer)typeof(PlayerController).GetField("faceRenderer", Private).GetValue(mysteryPlayer);
        Check(mysteryPlayer.BodyRenderer.enabled && mysteryPlayer.BodyRenderer.materials[bodyIndex] == bodyInvincible,
            "Mystery applies the invincible body material without hiding the player");
        Check(faceRenderer.materials[faceIndex] == faceInvincible, "Mystery applies the invincible face material without blinking");
        SetField("mysteryTimeLeft", 0f);
        Invoke("UpdatePowerUps");
        Check(!game.IsInvincible, "Mystery protection ends when its timer expires");
        Material bodyNormal = (Material)typeof(PlayerController).GetField("bodyNormalMaterial", Private).GetValue(mysteryPlayer);
        Material faceNormal = (Material)typeof(PlayerController).GetField("faceNormalMaterial", Private).GetValue(mysteryPlayer);
        Check(mysteryPlayer.BodyRenderer.materials[bodyIndex] == bodyNormal, "Mystery expiry restores the normal body material");
        Check(faceRenderer.materials[faceIndex] == faceNormal, "Mystery expiry restores the normal face material");
        SetField("playerCanDieFromBomb", false);
        game.CheatGrantAllPowerUps();
        Check((int)Field("maxBombs") == 10 && (int)Field("blastRange") == 5, "Grant All remains clamped to maxima");
        game.SetGodMode(true);
        Check(game.GodModeEnabled, "God Mode cheat enables god mode");
        game.ToggleGodMode();
        Check(!game.GodModeEnabled, "God Mode menu toggle disables god mode");
        Invoke("ResetPowerUps");

        PowerUpState state = game.PowerUps;
        state.bombUps = 2;
        state.fireUps = 3;
        state.speedUps = 1;
        state.detonator = state.bombPass = state.wallPass = state.flamePass = true;
        SetField("mysteryTimeLeft", 10f);
        Invoke("ResetTemporaryPowerUpsOnDeath");
        Check(state.bombUps == 2 && state.fireUps == 3 && state.speedUps == 1,
            "Death keeps permanent Bomb, Fire, and Speed upgrades");
        Check(!state.detonator && !state.bombPass && !state.wallPass && !state.flamePass
            && (float)Field("mysteryTimeLeft") == 0f, "Death clears temporary abilities and Mystery");
        state.bombUps = 3;
        state.fireUps = 3;
        state.speedUps = 1;
        state.cheatBombUps = 1;
        state.cheatFireUps = 2;
        state.cheatSpeedUps = 1;
        SetField("maxBombs", (int)Field("baseMaxBombs") + state.bombUps);
        SetField("blastRange", (int)Field("baseBlastRange") + state.fireUps);
        state.detonator = state.bombPass = state.wallPass = state.flamePass = true;
        SetField("mysteryTimeLeft", 10f);
        Type deathCause = typeof(BombermanPrototype).GetNestedType("PlayerDeathCause", BindingFlags.NonPublic);
        System.Collections.IEnumerator deathSequence = (System.Collections.IEnumerator)Invoke(
            "PlayerDeathSequence", Enum.Parse(deathCause, "Enemy"));
        Check(deathSequence.MoveNext(), "Death sequence starts");
        Check(!state.detonator && !state.bombPass && !state.wallPass && !state.flamePass
            && (float)Field("mysteryTimeLeft") == 0f, "Death sequence clears temporary power-ups immediately");
        Check(state.bombUps == 2 && state.fireUps == 1 && state.speedUps == 0
            && (int)Field("maxBombs") == (int)Field("baseMaxBombs") + 2
            && (int)Field("blastRange") == (int)Field("baseBlastRange") + 1
            && state.cheatBombUps == 0 && state.cheatFireUps == 0 && state.cheatSpeedUps == 0,
            "Death removes permanent levels granted by cheats while keeping normal pickups");
        manager.LoadStage(1);
        SetField("powerUpState", new PowerUpState());
        SetField("maxBombs", Field("baseMaxBombs"));
        SetField("blastRange", Field("baseBlastRange"));
        game.SetGodMode(false);

        int stageBeforeSkip = manager.CurrentStage;
        game.CheatSkipToNextStage();
        Check(manager.CurrentStage == stageBeforeSkip + 1, "Next Stage cheat advances the stage");
        manager.LoadStage(stageBeforeSkip);
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
                if (game.IsPaused) return;
                CheckDifficultyMapping();
                manager = game.GetComponent<StageManager>();
                EnemyController[] enemies = Living;
                Check(enemies.Length == 6 && enemies.All(e => e is ValcomEnemy), "Stage 1 spawns six Valcoms");
                Check(enemies.All(e => e.Cell.x >= 5 && Map.GetCellKind(e.Cell) == CellKind.Empty), "Stage enemies start on empty cells from column 5");
                Check(enemies.All(e => Mathf.Abs(e.Cell.x - game.PlayerCell.x) + Mathf.Abs(e.Cell.y - game.PlayerCell.y) > 5),
                    "Stage enemies start more than five tiles from the player");
                Check(enemies.Select(e => e.Cell).Distinct().Count() == enemies.Length, "Stage enemies start on separate cells");
                Check(HudText == "TIME 200", "HUD shows the stage time");
                Check((Type)Field("exitWaveEnemyType") == typeof(OnealEnemy), "Stage 1 exit enemy");
                CheckPowerUpAndCheatRules();
                manager.LoadStage(1);
                Vector2Int itemCell = (Vector2Int)Field("hiddenPowerUpCell");
                Vector2Int blastOrigin = BombermanPrototype.Directions
                    .Select(direction => itemCell + direction)
                    .FirstOrDefault(cell => Map.IsWalkable(cell) && cell != game.PlayerCell
                        && !Living.Any(enemy => enemy.Cell == cell));
                Check(Map.IsWalkable(blastOrigin) && blastOrigin != game.PlayerCell
                    && !Living.Any(enemy => enemy.Cell == blastOrigin), "Find an empty adjacent cell to reveal the hidden power-up");
                Invoke("Explode", blastOrigin);
                Check(((List<PowerUp>)Field("powerUps")).Count == 1, "Blast revealing a hidden power-up leaves it intact");
                Transform powerUpVisual = ((List<PowerUp>)Field("powerUps"))[0].transform.Find("Visual");
                Check(powerUpVisual.Find("Outline Shell") == null
                    && powerUpVisual.Cast<Transform>().Count(child => child.name == "Square Outline Edge") == 4,
                    "Power-up highlight is a flat four-edge square");
                int survivingEnemiesBeforeWave = Living.Length;
                Invoke("Explode", blastOrigin);
                Check(((List<PowerUp>)Field("powerUps")).Count == 0, "Later blast destroys the revealed power-up");
                Check(Living.Length == survivingEnemiesBeforeWave && (int)Field("pendingExitWaves") == 0,
                    "Blast destroying a power-up releases no enemies");
                phase = 1;
                deadline = EditorApplication.timeSinceStartup + 3.0;
            }
            else if (phase == 1)
            {
                Check(Living.Length == 6 && (int)Field("pendingExitWaves") == 0,
                    "Destroyed power-up leaves the enemy count unchanged");
                List<GridController> actors = (List<GridController>)Field("actors");
                foreach (EnemyController enemy in Living)
                {
                    actors.Remove(enemy);
                    UnityEngine.Object.Destroy(enemy.gameObject);
                }
                Vector2Int exitCell = Map.ExitDoor.Cell;
                if (Map.GetCellKind(exitCell) == CellKind.Destructible) Map.DestroyDestructibleAt(exitCell);
                Check(Map.ExitDoor.IsRevealed, "Exit is visible before the exit blast test");
                Vector2Int nearbyPlayerCell = BombermanPrototype.Directions
                    .Select(direction => exitCell + direction)
                    .FirstOrDefault(cell => Map.IsWalkable(cell));
                Check(Map.IsWalkable(nearbyPlayerCell), "Find a player cell close enough to bomb the door");
                PlayerController testPlayer = (PlayerController)Field("player");
                testPlayer.Cell = nearbyPlayerCell;
                testPlayer.transform.position = Map.CellToWorld(nearbyPlayerCell);
                Invoke("Explode", exitCell);
                lastExitEnemyCount = Living.Length;
                lastExitSpawnTime = -1d;
                staggeredExitSpawnCount = 0;
                phase = 2;
                deadline = EditorApplication.timeSinceStartup + 3.0;
            }
            else if (phase == 2)
            {
                int currentExitEnemyCount = Living.Length;
                if (currentExitEnemyCount > lastExitEnemyCount)
                {
                    Check(currentExitEnemyCount == lastExitEnemyCount + 1, "Exit wave adds at most one enemy between spawn intervals");
                    double now = EditorApplication.timeSinceStartup;
                    if (lastExitSpawnTime >= 0d)
                        Check(now - lastExitSpawnTime >= 0.45d, "Exit enemies are staggered by about half a second");
                    lastExitSpawnTime = now;
                    staggeredExitSpawnCount++;
                    lastExitEnemyCount = currentExitEnemyCount;
                }
                if ((int)Field("pendingExitWaves") > 0) return;
                Check(Living.Length == 3 && Living.Count(e => e.name.Contains("(Exit)")) == 3,
                    "First revealed-exit hit releases three enemies");
                Check((int)Field("exitDoorHitCount") == 1, "First door hit is recorded for incremental spawn counts");
                Check(staggeredExitSpawnCount == 3, "All three exit enemies were observed as separate spawns");
                Vector3 playerPosition = ((PlayerController)Field("player")).transform.position;
                Check(Living.All(e => (e.transform.position - playerPosition).sqrMagnitude >= 0.81f),
                    "Exit wave completes while the player stays near the door without overlapping");
                Check(Living.Select(e => e.Cell).Distinct().Count() == Living.Length,
                    "Exit enemies do not stack on the same cell");
                Invoke("Explode", Map.ExitDoor.Cell);
                lastExitEnemyCount = Living.Length;
                phase = 3;
                deadline = EditorApplication.timeSinceStartup + 4.0;
            }
            else if (phase == 3)
            {
                if ((int)Field("pendingExitWaves") > 0)
                {
                    Check(EditorApplication.timeSinceStartup < deadline, "Staggered exit wave completes");
                    return;
                }
                Check(Living.Length == lastExitEnemyCount + 1 && Living.Count(e => e.name.Contains("(Exit)")) == Living.Length,
                    "Each repeated exit hit releases exactly one more enemy");
                Check((int)Field("exitDoorHitCount") == 2, "Repeated door hit increments the door-hit count");
                manager.LoadStage(35);
                Check(Living.Length == 9 && Count<DahlEnemy>() == 2 && Count<MinuoEnemy>() == 1 && Count<OvapeEnemy>() == 1 && Count<DoriaEnemy>() == 3 && Count<PassEnemy>() == 2, "Stage 35 roster skips the empty slot");
                Check((Type)Field("exitWaveEnemyType") == typeof(DoriaEnemy), "Stage 35 exit enemy");
                manager.LoadStage(50);
                CheckFinalStageRoster("Stage 50 roster");
                manager.LoadStage(51);
                Check(Living.Length == 10 && Count<DahlEnemy>() == 1 && Count<MinuoEnemy>() == 1 && Count<OvapeEnemy>() == 1 && Count<DoriaEnemy>() == 3 && Count<PassEnemy>() == 4, "Endless Stage 51 cycles to NES Stage 41");
                Check((Type)Field("exitWaveEnemyType") == typeof(OvapeEnemy), "Endless Stage 51 exit enemy cycles to NES Stage 41");
                typeof(BombermanPrototype).GetField("stageTimeLimit", Private).SetValue(game, 0.3f);
                manager.LoadStage(1);
                phase = 4;
                deadline = EditorApplication.timeSinceStartup + 3.0;
            }
            else if (phase == 4)
            {
                if (!(bool)Field("timeUp"))
                {
                    Check(EditorApplication.timeSinceStartup < deadline, "Stage timer runs out");
                    return;
                }
                EnemyController[] pontans = Living;
                Check(pontans.Length == 10 && pontans.All(e => e is PontanEnemy), "Time-out replaces every enemy with ten Pontans");
                Check(pontans.All(e => Mathf.Abs(e.Cell.x - game.PlayerCell.x) + Mathf.Abs(e.Cell.y - game.PlayerCell.y) > 5
                    && Map.GetCellKind(e.Cell) == CellKind.Empty), "Time-out Pontans spawn on walkable cells more than five Manhattan steps from the player");
                Check(HudText == "TIME 0", "HUD shows zero after time-out");
                Debug.Log("STAGE_ENEMIES_SMOKE_PASS: stage scaling, endless cycle, corrected enemy IDs, power-up table/caps/death/cheats, revealed-item blast waves, enemy caps, HUD, timeout distance.");
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
