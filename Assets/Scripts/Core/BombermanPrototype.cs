using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-100)]
public sealed class BombermanPrototype : MonoBehaviour
{
    private const float TransitionFadeSeconds = 0.5f;
    private const float StageCardHoldSeconds = 1f;

    private enum PlayerDeathCause
    {
        Bomb,
        Enemy
    }

    public static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    [Header("Map")]
    [SerializeField] private int width = 31;
    [SerializeField] private int height = 13;
    [SerializeField, Range(0f, 1f)] private float destructibleDensity = 0.55f;
    [SerializeField] private int randomSeed = 0;

    [Header("Player")]
    [SerializeField] private float playerMoveSpeed = 6f;
    [SerializeField] private float playerAcceleration = 18f;
    [SerializeField] private float playerDeceleration = 24f;
    [SerializeField] private float playerTurnSpeed = 10f;
    [Tooltip("Actual wall-blocking capsule radius. Use 0.50 or less to fit the 1.08-unit passages.")]
    [SerializeField, Min(0.05f)] private float playerColliderRadius = 0.5f;
    [SerializeField, Min(0.05f)] private float playerColliderHeight = 1.125f;
    [SerializeField] private GameObject playerModelPrefab;
    [SerializeField] private float playerModelScale = 8.91f;
    [SerializeField] private RuntimeAnimatorController playerAnimatorController;
    [SerializeField] private Material playerBodyNormalMaterial;
    [SerializeField] private Material playerBodyBurntMaterial;
    [SerializeField] private Material playerBodyInvincibleMaterial;
    [SerializeField] private Material playerFaceNormalMaterial;
    [SerializeField] private Material playerFaceBlinkMaterial;
    [SerializeField] private Material playerFaceInvincibleMaterial;
    [SerializeField] private Material playerFaceDeadMaterial;
    [SerializeField] private Material playerFaceDeadBurntMaterial;
    [SerializeField] private GameObject bombModelPrefab;
    [SerializeField] private int maxBombs = 1;
    [SerializeField] private int blastRange = 1;
    [SerializeField] private float bombFuseTime = 2f;
    [SerializeField, Min(0f)] private float bombReleasePadding = 0.65f;
    [Tooltip("Bomb blasts can hurt Bomberman unless Flame Pass or Mystery protects him.")]
    [SerializeField] private bool playerCanDieFromBomb = true;
    [Tooltip("God mode: the player survives bombs, enemies, and the time-out Pontans.")]
    [SerializeField] private bool godMode;
    [Tooltip("Deaths allowed before the game ends, as in the original game.")]
    [SerializeField, Min(0)] private int playerLives = 3;
    [Tooltip("Scene loaded after GAME OVER. It must be in Build Settings.")]
    [SerializeField] private string titleSceneName = "Title";
    [Tooltip("Seconds the GAME OVER card stays fully visible before the title screen loads.")]
    [SerializeField, Min(0f)] private float gameOverDelay = 2.5f;
    [Tooltip("Text shown on the game-clear transition screen.")]
    [SerializeField] private string gameClearText = "ALL STAGES CLEAR";
    [Tooltip("Seconds the game-clear card stays fully visible before the title screen loads.")]
    [SerializeField, Min(0f)] private float gameClearDelay = 4f;
    [SerializeField, Min(0f)] private float playerDeathAnimationTime = 0.85f;
    [SerializeField, Min(0f)] private float playerDeathParticleTime = 0.6f;
    [SerializeField] private float playerRestartDelay = 0.15f;

    [Header("Scoring")]
    [Tooltip("Score for killing each enemy type.")]
    [SerializeField, Min(0)] private int valcomScore = 100;
    [SerializeField, Min(0)] private int onealScore = 200;
    [SerializeField, Min(0)] private int dahlScore = 400;
    [SerializeField, Min(0)] private int minuoScore = 800;
    [SerializeField, Min(0)] private int doriaScore = 1000;
    [SerializeField, Min(0)] private int ovapeScore = 2000;
    [SerializeField, Min(0)] private int passScore = 4000;
    [SerializeField, Min(0)] private int pontanScore = 8000;
    [Tooltip("Score per whole second left on the stage clock when the stage is cleared.")]
    [SerializeField, Min(0)] private int timeBonusPerSecond = 10;
    [Tooltip("Lives added when a stage is cleared successfully.")]
    [SerializeField, Min(0)] private int stageClearLifeBonus = 1;

    [Header("Power-ups")]
    [Tooltip("Hide one power-up under a random breakable block on every stage.")]
    [SerializeField] private bool spawnPowerUps = true;
    [Tooltip("Optional stage-by-stage override. Leave empty to use the original 50-stage power-up table.")]
    [SerializeField] private PowerUpType[] stagePowerUps;
    [SerializeField] private bool showCheatMenuButton = true;
    [Tooltip("Optional models, in PowerUpType order (Bomb Up, Fire Up, Speed Up, Detonator, Bomb Pass, Wall Pass, Flame Pass, Mystery). Empty slots use the built-in marker.")]
    [SerializeField] private GameObject[] powerUpPrefabs;
    [Tooltip("How close the player must be to an item to pick it up.")]
    [SerializeField, Min(0.05f)] private float powerUpPickupDistance = 0.55f;
    [SerializeField, Min(0)] private int powerUpScore = 1000;
    [Tooltip("Maximum total bombs the player can place at once, including the starting bomb.")]
    [SerializeField, Min(1)] private int maxBombLimit = 10;
    [Tooltip("Longest blast range Fire Up can reach.")]
    [SerializeField, Min(1)] private int blastRangeLimit = 5;
    [Tooltip("How many Speed Ups can stack.")]
    [SerializeField, Min(0)] private int speedUpLimit = 1;
    [Tooltip("Seconds of invincibility from the Mystery power-up.")]
    [SerializeField, Min(0f)] private float mysteryDuration = 35f;

    [Header("Enemies")]
    [SerializeField] private bool spawnEnemies = false;
    [Tooltip("Spawn each stage's enemies from the original game's table. Turn off to use the counts below.")]
    [SerializeField] private bool useOriginalStageEnemies = true;
    [SerializeField] private int onealCount = 3;
    [SerializeField] private int dahlCount = 3;
    [SerializeField] private int pontanCount = 3;
    [SerializeField] private int passCount = 3;
    [SerializeField] private int valcomCount = 3;
    [SerializeField] private int ovapeCount = 3;
    [SerializeField] private int doriaCount = 3;
    [SerializeField] private int minuoCount = 3;
    [SerializeField, Min(0.05f)] private float enemyPlayerHitDistance = 0.45f;
    [Tooltip("Write exit door and enemy spawn events to the Console.")]
    [SerializeField] private bool logSpawnEvents = true;

    [Tooltip("Enemy prefab settings control movement and behavior.")]
    [SerializeField] private EnemyController[] enemyPrefabs;
    [Header("Exit Door")]
    [Tooltip("How close the player must stand to the door center to enter an open exit.")]
    [SerializeField, Min(0.05f)] private float exitDoorEnterDistance = 0.4f;
    [Tooltip("A blast on the revealed exit releases the stage's exit enemy from the original table until this many are alive. Zero disables it.")]
    [SerializeField, Min(0)] private int exitDoorPopulationCap = 10;
    [SerializeField, Min(0f)] private float exitDoorSpawnDelay = 0.5f;
    [Header("Stage Timer")]
    [Tooltip("Seconds per stage (200 in the original game). When time runs out, every enemy is replaced by Pontans. Zero disables the timer.")]
    [SerializeField, Min(0f)] private float stageTimeLimit = 200f;
    [Header("Camera")]
    [SerializeField] private float cameraOrthographicSize = 6.9f;
    [SerializeField] private float cameraHeight = 18f;
    [SerializeField] private float cameraPitchAngle = 55f;
    [SerializeField] private float cameraFollowSpeed = 8f;
    [SerializeField] private float horizontalEdgePadding = 1.25f;

    private readonly Dictionary<Vector2Int, Bomb> bombs = new();
    private readonly List<GridController> actors = new();

    private BombermanMaterials materials;
    private BombermanMap map;
    private PauseMenu pauseMenu;
    private CheatMenu cheatMenu;
    private ScreenTransitionOverlay transitionOverlay;
    private Camera mainCamera;
    private Vector3 cameraBasePosition;
    private Vector3 cameraFollowVelocity;
    private Quaternion cameraRotation;
    private PlayerController player;
    private int activeBombs;
    private bool paused;
    private bool cheatMenuOpen;
    private bool wasPausedBeforeCheatMenu;
    private bool transitionActive;
    private bool restarting;
    private StageManager stageManager;
    private GameObject stageRoot;
    private GameObject sceneryRoot;
    private StageTheme.SceneryVisual builtScenery;
    private GameObject sceneEnemyTemplates;
    private int sessionSeed;
    private int restartLayoutSeed;
    private bool hasRestartLayoutSeed;
    private bool firstStage = true;
    private System.Type exitWaveEnemyType;
    private int pendingExitWaves;
    private bool stageClearing;
    private StageHud stageHud;
    private float timeLeft;
    private bool timeUp;
    private StageLighting stageLighting;
    private StageTheme.ThemeLighting currentLighting;
    private int livesLeft;
    private int score;
    private PowerUpState powerUpState = new();
    private readonly List<PowerUp> powerUps = new();
    private int baseMaxBombs;
    private int baseBlastRange;
    private int bombSequence;
    private float mysteryTimeLeft;
    private bool hasHiddenPowerUp;
    private Vector2Int hiddenPowerUpCell;
    private PowerUpType hiddenPowerUpType;

    public bool IsPaused => paused || transitionActive;
    public bool IsStageClearing => stageClearing;
    public bool HasLivingPlayer => player != null && !player.IsDead;
    public bool IsReady => map != null && player != null;
    public bool IsLastStage => stageManager != null && stageManager.OnLastStage;
    public bool GodModeEnabled => godMode;
    public Vector2Int PlayerCell => player != null ? player.Cell : Vector2Int.zero;
    public int Score => score;
    public PowerUpState PowerUps => powerUpState;
    public bool IsInvincible => godMode || mysteryTimeLeft > 0f;
    public int CurrentStageNumber => stageManager != null ? stageManager.CurrentStage : 1;
    // 1-based map/theme phase from the Stage Sequence (1, 2, 3...). Footsteps follow this, not the raw
    // stage number, so every stage on the same map uses the same walking sound. 1 when there is no sequence.
    public int CurrentStagePhase => stageManager != null ? Mathf.Max(1, stageManager.CurrentPhase) : 1;

    public bool IsBombBlockingCell(Vector2Int cell)
    {
        // Bomb Pass: the player walks through bombs.
        if (powerUpState.bombPass)
        {
            return false;
        }

        return bombs.TryGetValue(cell, out Bomb bomb)
            && bomb != null
            && bomb.BlocksMovement;
    }

    private void Start()
    {
        width = Mathf.Max(5, width | 1);
        height = Mathf.Max(5, height | 1);

        maxBombLimit = Mathf.Clamp(maxBombLimit, 1, 10);
        blastRangeLimit = Mathf.Clamp(blastRangeLimit, 1, 5);
        speedUpLimit = Mathf.Clamp(speedUpLimit, 0, 1);
        maxBombs = Mathf.Clamp(maxBombs, 1, maxBombLimit);
        blastRange = Mathf.Clamp(blastRange, 1, blastRangeLimit);
        baseMaxBombs = maxBombs;
        baseBlastRange = blastRange;
        materials = BombermanMaterials.Create();
        sessionSeed = randomSeed == 0 ? System.Environment.TickCount : randomSeed;
        pauseMenu = PauseMenu.Create();
        cheatMenu = CheatMenu.Create(this, showCheatMenuButton);
        transitionOverlay = ScreenTransitionOverlay.Create();
        stageHud = StageHud.Create();
        livesLeft = playerLives;
        score = 0;
        stageLighting = StageLighting.Create();
        sceneEnemyTemplates = new GameObject("Scene Enemy Templates");
        sceneEnemyTemplates.transform.SetParent(transform, false);
        sceneEnemyTemplates.SetActive(false);
        foreach (EnemyController enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            Instantiate(enemy.gameObject, sceneEnemyTemplates.transform, true);
        stageManager = GetComponent<StageManager>();
        if (stageManager == null) stageManager = gameObject.AddComponent<StageManager>();
        RestoreSavedRun();
        stageManager.Initialize(this);
    }

    internal void LoadStage(int stage, StageTheme theme)
    {
        StopAllCoroutines();
        if (cheatMenuOpen)
        {
            cheatMenuOpen = false;
            cheatMenu.SetVisible(false);
        }
        transitionActive = false;
        SetPaused(false);
        transitionOverlay.Hide();
        restarting = false;
        stageClearing = false;
        pendingExitWaves = 0;
        timeLeft = stageTimeLimit;
        timeUp = false;
        stageHud.SetVisible(true);
        stageHud.SetTimeVisible(stageTimeLimit > 0f);
        stageHud.SetTime(timeLeft);
        stageHud.SetScore(score);
        stageHud.SetLives(livesLeft);
        mysteryTimeLeft = 0f;
        powerUps.Clear();
        hasHiddenPowerUp = false;
        bombSequence = 0;
        // Continue on the title screen restarts this stage with the score, lives, and power-ups saved here.
        GameProgress.SaveRun(stage, score, livesLeft, powerUpState);
        if (stageRoot != null)
        {
            stageRoot.SetActive(false);
            Destroy(stageRoot);
        }
        bombs.Clear();
        actors.Clear();
        activeBombs = 0;
        player = null;
        stageRoot = new GameObject($"Stage {stage}");
        stageRoot.transform.SetParent(transform, false);
        ApplyScenery(theme);
        int layoutSeed = hasRestartLayoutSeed ? restartLayoutSeed : GetStageSeed(sessionSeed, stage);
        hasRestartLayoutSeed = false;
        SoundManager.Instance?.ResetExitOpenForStage();
        map = new BombermanMap(width, height, destructibleDensity, layoutSeed, materials, theme, stageRoot.transform);
        ConfigureCamera();
        currentLighting = theme != null ? theme.Lighting : null;
        stageLighting.Apply(currentLighting, mainCamera);
        map.Generate();
        PlanHiddenPowerUp(stage, layoutSeed);
        SpawnPlayer();
        if (firstStage)
        {
            foreach (EnemyController enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                RegisterEnemy(enemy);
            firstStage = false;
        }
        else
        {
            foreach (Transform template in sceneEnemyTemplates.transform)
            {
                GameObject instance = Instantiate(template.gameObject, stageRoot.transform);
                instance.name = template.name;
                RegisterEnemy(instance.GetComponent<EnemyController>());
            }
        }
        if (spawnEnemies) SpawnEnemies(stage);
        int campaignLength = stageManager != null ? stageManager.TotalStages : 0;
        exitWaveEnemyType = OriginalStageEnemies.GetExitEnemy(stage, campaignLength);
        LogSpawnEvent($"Stage {stage} starts with {DescribeEnemies()}. Bombing the open exit releases {exitWaveEnemyType.Name}.");
        StartCoroutine(StageIntroSequence(stage));
    }

    // Scenery is heavy, so it outlives the stage root and is rebuilt only when a stage asks for different scenery.
    private void ApplyScenery(StageTheme theme)
    {
        StageTheme.SceneryVisual scenery = theme != null ? theme.Scenery : null;
        bool wanted = scenery != null && scenery.Prefab != null;
        if (wanted && sceneryRoot != null && scenery.Matches(builtScenery)) return;

        if (sceneryRoot != null)
        {
            sceneryRoot.SetActive(false);
            Destroy(sceneryRoot);
            sceneryRoot = null;
        }

        builtScenery = wanted ? scenery.Copy() : null;
        if (wanted) sceneryRoot = scenery.Attach(transform, ArenaCenter(width, height));
    }

    private static Vector3 ArenaCenter(int mapWidth, int mapHeight)
    {
        return new Vector3(mapWidth / 2 - mapWidth / 2f, 0f, mapHeight / 2 - mapHeight / 2f);
    }

    // Builds a stage's arena without actors for the Stage Manager's edit-mode scene preview.
    // With a fixed Random Seed the layout and model variants match Play Mode.
    public GameObject BuildArenaPreview(int stage, StageTheme theme)
    {
        GameObject root = new("Stage Preview");
        int previewSessionSeed = randomSeed == 0 ? System.Environment.TickCount : randomSeed;
        int previewWidth = Mathf.Max(5, width | 1);
        int previewHeight = Mathf.Max(5, height | 1);
        BombermanMap previewMap = new(
            previewWidth,
            previewHeight,
            destructibleDensity,
            GetStageSeed(previewSessionSeed, stage),
            BombermanMaterials.Create(),
            theme,
            root.transform);
        previewMap.Generate();
        if (theme != null) theme.Scenery.Attach(root.transform, ArenaCenter(previewWidth, previewHeight));
        return root;
    }

    private static int GetStageSeed(int session, int stage)
    {
        return unchecked(session + (stage - 1) * 486187739);
    }

    internal void RandomizeRestartLayout()
    {
        restartLayoutSeed = CreateRandomSeed();
        hasRestartLayoutSeed = true;
    }

    private static int CreateRandomSeed()
    {
        int seed = System.Guid.NewGuid().GetHashCode();
        return seed == 0 ? 1 : seed;
    }

    private void Update()
    {
        if (!transitionActive && WasPressed(Key.Escape))
        {
            if (cheatMenuOpen) CloseCheatMenu();
            else SetPaused(!paused);
        }

        // Theme lighting edits in the Inspector show immediately while playing in the Editor.
        if (Application.isEditor && mainCamera != null)
        {
            stageLighting.Apply(currentLighting, mainCamera);
        }

        UpdateStageTimer();
        UpdatePowerUps();
    }

    private void UpdateStageTimer()
    {
        if (stageTimeLimit <= 0f || timeUp || IsPaused || restarting || stageClearing || !HasLivingPlayer)
        {
            return;
        }

        timeLeft = Mathf.Max(0f, timeLeft - Time.deltaTime);
        stageHud.SetTime(timeLeft);
        if (timeLeft <= 0f)
        {
            timeUp = true;
            SpawnTimeOutPontans();
        }
    }

    // Time-out repopulates the stage with Pontans, but keeps every new Pontan away from the player.
    private void SpawnTimeOutPontans()
    {
        EnemyController pontanPrefab = FindEnemyPrefab(typeof(PontanEnemy));
        if (pontanPrefab == null)
        {
            Debug.LogError($"Assign the {nameof(PontanEnemy)} prefab to Enemy Prefabs on {name}.", this);
            return;
        }

        List<GridController> enemies = new();
        foreach (GridController actor in actors)
        {
            if (actor == null || actor.IsPlayer || actor.IsDead) continue;
            enemies.Add(actor);
        }

        foreach (GridController actor in enemies)
        {
            actors.Remove(actor);
            Destroy(actor.gameObject);
        }

        int spawned = 0;
        for (; spawned < OriginalStageEnemies.MaxEnemies; spawned++)
        {
            if (!TryFindTimeOutSpawnCell(out Vector2Int cell))
            {
                Debug.LogWarning($"Only {spawned} time-out Pontans could be placed more than five tiles from the player.", this);
                break;
            }

            EnemyController pontan = Instantiate(pontanPrefab, map.CellToWorld(cell), Quaternion.identity);
            pontan.name = $"{pontanPrefab.name} (Time Up)";
            RegisterEnemy(pontan);
        }

        LogSpawnEvent($"Time ran out. Spawned {spawned} Pontans more than five tiles from the player.");
    }

    private bool TryFindTimeOutSpawnCell(out Vector2Int result)
    {
        List<Vector2Int> candidates = new();
        Vector2Int playerCell = player != null ? player.Cell : new Vector2Int(1, 1);
        for (int x = 1; x < width - 1; x++)
        {
            for (int y = 1; y < height - 1; y++)
            {
                Vector2Int cell = new(x, y);
                if (!IsWalkable(cell) || IsActorAt(cell)) continue;
                int manhattanDistance = Mathf.Abs(cell.x - playerCell.x) + Mathf.Abs(cell.y - playerCell.y);
                if (manhattanDistance <= 5) continue;
                candidates.Add(cell);
            }
        }

        if (candidates.Count == 0)
        {
            result = default;
            return false;
        }

        result = candidates[Random.Range(0, candidates.Count)];
        return true;
    }

    private void LateUpdate()
    {
        UpdateCameraFollow();
        CollectPowerUps();
        KillPlayerIfEnemyTouches();
        UpdateExitDoor();
    }

    private void ConfigureCamera()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject cameraObject = new("Main Camera");
            cameraObject.tag = "MainCamera";
            mainCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        Vector3 center = map.CellToWorld(new Vector2Int(width / 2, height / 2));
        cameraRotation = Quaternion.Euler(cameraPitchAngle, 0f, 0f);
        cameraBasePosition = GetCameraPositionLookingAt(center);
        cameraFollowVelocity = Vector3.zero;

        mainCamera.transform.position = cameraBasePosition;
        mainCamera.transform.rotation = cameraRotation;
        mainCamera.orthographic = true;
        mainCamera.orthographicSize = cameraOrthographicSize;
    }

    private void UpdateCameraFollow()
    {
        if (mainCamera == null || player == null)
        {
            return;
        }

        Vector3 playerPosition = player.transform.position;
        float targetX = Mathf.Clamp(playerPosition.x, GetMinCameraX(), GetMaxCameraX());
        Vector3 targetPosition = new(targetX, cameraBasePosition.y, cameraBasePosition.z);
        float smoothTime = cameraFollowSpeed > 0.001f ? 1f / cameraFollowSpeed : 0f;
        if (smoothTime <= 0f)
        {
            mainCamera.transform.position = targetPosition;
            cameraFollowVelocity = Vector3.zero;
            return;
        }

        mainCamera.transform.position = Vector3.SmoothDamp(
            mainCamera.transform.position,
            targetPosition,
            ref cameraFollowVelocity,
            smoothTime,
            Mathf.Infinity,
            Time.deltaTime);
    }

    private Vector3 GetCameraPositionLookingAt(Vector3 groundTarget)
    {
        Vector3 forward = cameraRotation * Vector3.forward;
        float distanceToGround = cameraHeight / Mathf.Max(0.01f, -forward.y);
        return groundTarget - forward * distanceToGround;
    }

    private float GetMinCameraX()
    {
        float visibleHalfWidth = mainCamera.orthographicSize * mainCamera.aspect;
        float leftEdge = map.LeftWorldX - horizontalEdgePadding;
        float rightEdge = map.RightWorldX + horizontalEdgePadding;
        return Mathf.Min((leftEdge + rightEdge) * 0.5f, leftEdge + visibleHalfWidth);
    }

    private float GetMaxCameraX()
    {
        float visibleHalfWidth = mainCamera.orthographicSize * mainCamera.aspect;
        float leftEdge = map.LeftWorldX - horizontalEdgePadding;
        float rightEdge = map.RightWorldX + horizontalEdgePadding;
        return Mathf.Max((leftEdge + rightEdge) * 0.5f, rightEdge - visibleHalfWidth);
    }

    private void SpawnPlayer()
    {
        player = PlayerController.Create(
            "Bomberman",
            new Vector2Int(1, 1),
            map,
            materials.Player,
            materials.PlayerDead,
            0.9f,
            playerColliderRadius,
            playerColliderHeight,
            playerModelPrefab,
            playerModelScale,
            playerAnimatorController,
            playerBodyNormalMaterial,
            playerBodyBurntMaterial,
            playerBodyInvincibleMaterial,
            playerFaceNormalMaterial,
            playerFaceBlinkMaterial,
            playerFaceInvincibleMaterial,
            playerFaceDeadMaterial,
            playerFaceDeadBurntMaterial);
        player.MoveSpeed = GetPlayerMoveSpeed();
        player.WallPass = powerUpState.wallPass;
        player.FreeMoveAcceleration = GetPlayerMoveAcceleration();
        player.FreeMoveDeceleration = GetPlayerMoveDeceleration();
        player.FreeMoveTurnSpeed = playerTurnSpeed;
        player.Initialize(this);
        player.transform.SetParent(stageRoot.transform, true);
        actors.Add(player);
    }

    private float GetPlayerMoveSpeed()
    {
        return playerMoveSpeed * (powerUpState.speedUps > 0 ? 2f : 1f);
    }

    private float GetPlayerMoveAcceleration()
    {
        return playerAcceleration * (powerUpState.speedUps > 0 ? 2f : 1f);
    }

    private float GetPlayerMoveDeceleration()
    {
        return playerDeceleration * (powerUpState.speedUps > 0 ? 2f : 1f);
    }

    private void SpawnEnemies(int stage)
    {
        if (useOriginalStageEnemies)
        {
            // Original rule: stage enemies start at least five columns from the player's corner.
            int campaignLength = stageManager != null ? stageManager.TotalStages : 0;
            foreach ((System.Type enemyType, int count) in OriginalStageEnemies.GetRoster(stage, campaignLength))
                SpawnEnemyGroup(enemyType, count, () => FindOriginalSpawnCell(5));
            return;
        }

        SpawnEnemyGroup(typeof(OnealEnemy), onealCount, FindEnemySpawnCell);
        SpawnEnemyGroup(typeof(DahlEnemy), dahlCount, FindEnemySpawnCell);
        SpawnEnemyGroup(typeof(PontanEnemy), pontanCount, FindEnemySpawnCell);
        SpawnEnemyGroup(typeof(PassEnemy), passCount, FindEnemySpawnCell);
        SpawnEnemyGroup(typeof(ValcomEnemy), valcomCount, FindEnemySpawnCell);
        SpawnEnemyGroup(typeof(OvapeEnemy), ovapeCount, FindEnemySpawnCell);
        SpawnEnemyGroup(typeof(DoriaEnemy), doriaCount, FindEnemySpawnCell);
        SpawnEnemyGroup(typeof(MinuoEnemy), minuoCount, FindEnemySpawnCell);
    }

    private void SpawnEnemyGroup(System.Type enemyType, int count, System.Func<Vector2Int> findCell)
    {
        if (count <= 0) return;
        count = Mathf.Min(count, Mathf.Max(0, OriginalStageEnemies.MaxEnemies - CountLivingEnemies()));
        if (count == 0) return;
        EnemyController prefab = FindEnemyPrefab(enemyType);
        if (prefab == null)
        {
            Debug.LogError($"Assign the {enemyType.Name} prefab to Enemy Prefabs on {name}.", this);
            return;
        }
        for (int i = 0; i < count; i++)
        {
            Vector2Int cell = findCell();
            if (!IsSafeEnemySpawnCell(cell))
            {
                Debug.LogWarning($"Stopped spawning {enemyType.Name}: no unoccupied cell more than five tiles from the player is available.", this);
                break;
            }

            EnemyController enemy = Instantiate(prefab, map.CellToWorld(cell), Quaternion.identity);
            enemy.name = $"{prefab.name} {i + 1}";
            RegisterEnemy(enemy);
        }
    }

    private EnemyController FindEnemyPrefab(System.Type enemyType)
    {
        if (enemyPrefabs != null)
            foreach (EnemyController candidate in enemyPrefabs)
                if (enemyType.IsInstanceOfType(candidate)) return candidate;
        return null;
    }

    private int CountLivingEnemies()
    {
        int count = 0;
        foreach (GridController actor in actors)
        {
            if (actor != null && !actor.IsPlayer && !actor.IsDead)
            {
                count++;
            }
        }

        return count;
    }

    public void RegisterEnemy(EnemyController enemy)
    {
        if (!IsReady || enemy == null || actors.Contains(enemy)) return;
        if (CountLivingEnemies() >= OriginalStageEnemies.MaxEnemies)
        {
            Debug.LogWarning($"The stage already has {OriginalStageEnemies.MaxEnemies} living enemies; ignoring {enemy.name}.", this);
            enemy.gameObject.SetActive(false);
            return;
        }
        Vector2Int cell = map.WorldToCell(enemy.transform.position);
        if (!IsSafeEnemySpawnCell(cell))
        {
            if (!TryFindSafeEnemySpawnCell(cell, out Vector2Int safeCell))
            {
                Debug.LogWarning($"Could not spawn {enemy.name}: no unoccupied cell more than five tiles from the player is available.", this);
                enemy.gameObject.SetActive(false);
                return;
            }

            cell = safeCell;
        }

        enemy.Cell = cell;
        enemy.transform.position = map.CellToWorld(enemy.Cell);
        enemy.transform.SetParent(stageRoot.transform, true);
        actors.Add(enemy);
        enemy.Initialize(this);
    }

    public bool CanEnemyTraverse(EnemyController enemy, Vector2Int cell, bool ignoreBombs = false)
    {
        if (ignoreBombs && map != null)
        {
            CellKind kind = map.GetCellKind(cell);
            return kind == CellKind.Empty || (enemy.CanPassDestructibleWalls && kind == CellKind.Destructible);
        }
        return map != null && (enemy.CanPassDestructibleWalls
            ? IsWalkableIncludingDestructible(cell) : IsWalkable(cell));
    }

    // Read-only forecast of the current explosion geometry, without fuse or chain prediction.
    public bool IsCellThreatenedByBomb(Vector2Int target)
    {
        if (map == null) return false;
        foreach (Bomb bomb in bombs.Values)
        {
            if (bomb == null) continue;
            if (bomb.Cell == target) return true;
            foreach (Vector2Int direction in Directions)
                for (int step = 1; step <= blastRange; step++)
                {
                    Vector2Int cell = bomb.Cell + direction * step;
                    CellKind kind = map.GetCellKind(cell);
                    if (kind == CellKind.None || kind == CellKind.Solid) break;
                    if (cell == target) return true;
                    if (kind == CellKind.Destructible || IsRevealedExit(cell)) break;
                }
        }
        return false;
    }

    private bool IsRevealedExit(Vector2Int cell)
    {
        return map.ExitDoor != null && map.ExitDoor.IsRevealed && map.ExitDoor.Cell == cell;
    }

    // Original RAND_COORDS rule with a safety radius around the current player position.
    private Vector2Int FindOriginalSpawnCell(int minColumn)
    {
        for (int attempts = 0; attempts < 200; attempts++)
        {
            Vector2Int cell = new(Random.Range(Mathf.Min(minColumn, width - 2), width - 1), Random.Range(1, height - 1));
            if (IsOriginalSpawnCell(cell))
            {
                return cell;
            }
        }

        // Small or crowded maps may use any remaining safe cell outside the starting corner.
        for (int x = width - 2; x >= 1; x--)
        {
            for (int y = height - 2; y >= 1; y--)
            {
                Vector2Int cell = new(x, y);
                if (IsOriginalSpawnCell(cell))
                {
                    return cell;
                }
            }
        }

        return new Vector2Int(-1, -1);
    }

    private bool IsOriginalSpawnCell(Vector2Int cell)
    {
        return IsSafeEnemySpawnCell(cell) && (cell.x >= 3 || cell.y >= 3);
    }
    private Vector2Int FindEnemySpawnCell()
    {
        for (int attempts = 0; attempts < 200; attempts++)
        {
            Vector2Int cell = new(Random.Range(width / 2, width - 2), Random.Range(1, height - 1));
            if (IsSafeEnemySpawnCell(cell) && Vector2Int.Distance(cell, new Vector2Int(1, 1)) > 8f)
            {
                return cell;
            }
        }

        for (int x = width - 2; x >= 1; x--)
        {
            for (int y = height - 2; y >= 1; y--)
            {
                Vector2Int cell = new(x, y);
                if (IsSafeEnemySpawnCell(cell)) return cell;
            }
        }

        return new Vector2Int(-1, -1);
    }

    private bool IsSafeEnemySpawnCell(Vector2Int cell)
    {
        if (!IsWalkable(cell) || IsActorAt(cell)) return false;
        Vector2Int playerCell = player != null ? player.Cell : new Vector2Int(1, 1);
        int distance = Mathf.Abs(cell.x - playerCell.x) + Mathf.Abs(cell.y - playerCell.y);
        return distance > 5;
    }

    private bool TryFindSafeEnemySpawnCell(Vector2Int preferredCell, out Vector2Int result)
    {
        if (IsSafeEnemySpawnCell(preferredCell))
        {
            result = preferredCell;
            return true;
        }

        List<Vector2Int> candidates = new();
        for (int x = 1; x < width - 1; x++)
        {
            for (int y = 1; y < height - 1; y++)
            {
                Vector2Int cell = new(x, y);
                if (IsSafeEnemySpawnCell(cell)) candidates.Add(cell);
            }
        }

        if (candidates.Count == 0)
        {
            result = default;
            return false;
        }

        result = candidates[Random.Range(0, candidates.Count)];
        return true;
    }

    public void TryMoveActor(GridController controller, Vector2Int direction)
    {
        if (paused || controller == null || controller.IsDead || controller.IsMoving)
        {
            return;
        }

        if (Mathf.Abs(direction.x) + Mathf.Abs(direction.y) != 1) return;
        Vector2Int target = controller.Cell + direction;
        bool walkable = controller is EnemyController enemy ? CanEnemyTraverse(enemy, target) : controller.CanPassDestructibleWalls
            ? IsWalkableIncludingDestructible(target)
            : IsWalkable(target);

        if (walkable)
        {
            controller.StartMove(target, map.CellToWorld(target));
        }
    }

    private bool IsWalkable(Vector2Int cell)
    {
        return map.IsWalkable(cell)
            && !bombs.ContainsKey(cell);
    }

    private bool IsWalkableIncludingDestructible(Vector2Int cell)
    {
        CellKind kind = map.GetCellKind(cell);
        return (kind == CellKind.Empty || kind == CellKind.Destructible)
            && !bombs.ContainsKey(cell);
    }

    private bool IsActorAt(Vector2Int cell)
    {
        foreach (GridController actor in actors)
        {
            if (actor != null && !actor.IsDead && actor.Cell == cell)
            {
                return true;
            }
        }

        return false;
    }

    private static bool WasPressed(Key key)
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard[key].wasPressedThisFrame;
    }

    public void TryDropBomb()
    {
        if (IsPaused || stageClearing || player == null || player.IsDead)
        {
            return;
        }

        if (TryEnterExit())
        {
            return;
        }

        Vector2Int bombCell = map.WorldToCell(player.transform.position);
        if (activeBombs >= maxBombs || bombs.ContainsKey(bombCell))
        {
            return;
        }

        if (!map.IsWalkable(bombCell))
        {
            return;
        }

        GameObject bombObject = bombModelPrefab != null
            ? Instantiate(bombModelPrefab)
            : GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bombObject.transform.SetParent(stageRoot.transform, true);
        bombObject.name = $"Bomb {bombCell.x},{bombCell.y}";
        bombObject.transform.position = map.CellToWorld(bombCell) + new Vector3(0f, 0.35f, 0f);
        if (bombModelPrefab == null)
        {
            bombObject.transform.localScale = new Vector3(0.62f, 0.62f, 0.62f);
            bombObject.GetComponent<Renderer>().material = materials.Bomb;
        }

        Bomb bomb = bombObject.GetComponent<Bomb>();
        if (bomb == null)
        {
            bomb = bombObject.AddComponent<Bomb>();
        }
        bomb.Initialize(bombCell, player.transform, playerColliderRadius + bombReleasePadding);
        bomb.PlacedOrder = ++bombSequence;
        bombs[bomb.Cell] = bomb;
        activeBombs++;
        SoundManager.Instance?.PlayPlaceBomb();
        StartCoroutine(ExplodeAfterFuse(bomb));
    }

    private IEnumerator ExplodeAfterFuse(Bomb bomb)
    {
        // Without the Detonator the fuse burns for bombFuseTime. With it, a bomb waits for the detonator key.
        float fuseElapsed = 0f;
        while (bomb != null && !bomb.DetonateRequested && (powerUpState.detonator || fuseElapsed < bombFuseTime))
        {
            if (!IsPaused)
            {
                fuseElapsed += Time.deltaTime;
            }

            yield return null;
        }

        if (bomb == null)
        {
            yield break;
        }

        Vector2Int origin = bomb.Cell;
        bombs.Remove(origin);
        activeBombs = Mathf.Max(0, activeBombs - 1);
        Destroy(bomb.gameObject);
        Explode(origin);
    }

    private void Explode(Vector2Int origin)
    {
        ExitDoor exitDoor = map.ExitDoor;
        HashSet<PowerUp> powerUpsPresentBeforeBlast = new(powerUps);
        // Only an exit that was already visible reacts; the blast that uncovers it does not.
        bool exitWasRevealed = exitDoor != null && exitDoor.IsRevealed;
        List<Vector2Int> blastCells = new() { origin };
        foreach (Vector2Int direction in Directions)
        {
            for (int step = 1; step <= blastRange; step++)
            {
                Vector2Int cell = origin + direction * step;
                CellKind kind = map.GetCellKind(cell);
                if (kind == CellKind.None || kind == CellKind.Solid)
                {
                    break;
                }

                if (!blastCells.Contains(cell))
                {
                    blastCells.Add(cell);
                }
                if (kind == CellKind.Destructible)
                {
                    Material debrisMaterial = map.GetBlockMaterial(cell);
                    if (debrisMaterial == null)
                    {
                        debrisMaterial = materials.Destructible;
                    }

                    map.DestroyDestructibleAt(cell);
                    RevealPowerUp(cell);
                    StartCoroutine(ShowDebris(cell, debrisMaterial, step));
                    break;
                }

                // Like the original game, flames stop at a revealed exit.
                if (IsRevealedExit(cell))
                {
                    break;
                }
            }
        }

        DestroyPowerUpsCaughtInBlast(blastCells, powerUpsPresentBeforeBlast);

        StageTheme.ThemeLighting lighting = StageLighting.Resolve(currentLighting);
        ExplosionFlash.Create(map.CellToWorld(origin) + Vector3.up, lighting.ExplosionColor, lighting.ExplosionIntensity, blastRange + 1.5f, stageRoot.transform);
        SoundManager.Instance?.PlayBombExplode();
        foreach (Vector2Int cell in blastCells)
        {
            StartCoroutine(ShowExplosion(cell, origin));
            DamageActorsAt(cell);
        }

        if (exitDoor != null && !exitWasRevealed && exitDoor.IsRevealed)
        {
            LogSpawnEvent($"Blast from {origin} uncovered the exit at {exitDoor.Cell}. No enemies released.");
        }

        if (exitWasRevealed && exitDoorPopulationCap > 0 && blastCells.Contains(exitDoor.Cell))
        {
            LogSpawnEvent($"Blast from {origin} hit the open exit at {exitDoor.Cell}. Enemies come out in {exitDoorSpawnDelay:0.##}s.");
            StartCoroutine(SpawnEnemyWave(exitDoor.Cell, "Exit"));
        }
    }

    private void DestroyPowerUpsCaughtInBlast(List<Vector2Int> blastCells, HashSet<PowerUp> existedBeforeBlast)
    {
        foreach (PowerUp item in existedBeforeBlast)
        {
            if (item == null || !blastCells.Contains(item.Cell)) continue;
            Vector2Int cell = item.Cell;
            powerUps.Remove(item);
            Destroy(item.gameObject);
            LogSpawnEvent($"Blast destroyed the {item.Type} power-up at {cell}. Enemies come out in {exitDoorSpawnDelay:0.##}s.");
            StartCoroutine(SpawnEnemyWave(cell, "Power-up"));
        }
    }

    private void LogSpawnEvent(string message)
    {
        if (logSpawnEvents)
        {
            Debug.Log($"[Bomberman] {message}", this);
        }
    }

    private string DescribeEnemies()
    {
        Dictionary<string, int> counts = new();
        foreach (GridController actor in actors)
        {
            if (actor == null || actor.IsPlayer || actor.IsDead) continue;
            string enemyName = actor.GetType().Name;
            counts[enemyName] = counts.TryGetValue(enemyName, out int count) ? count + 1 : 1;
        }

        if (counts.Count == 0) return "no enemies";
        List<string> parts = new();
        foreach (KeyValuePair<string, int> entry in counts) parts.Add($"{entry.Value} {entry.Key}");
        return string.Join(", ", parts);
    }

    private IEnumerator SpawnEnemyWave(Vector2Int spawnCell, string source)
    {
        // Any pending enemy wave keeps the exit locked until its enemies exist.
        pendingExitWaves++;
        yield return WaitForGameplaySeconds(exitDoorSpawnDelay);
        pendingExitWaves--;
        if (restarting || map == null)
        {
            yield break;
        }

        EnemyController prefab = FindEnemyPrefab(exitWaveEnemyType);
        if (prefab == null)
        {
            Debug.LogError($"Assign the {exitWaveEnemyType.Name} prefab to Enemy Prefabs on {name}.", this);
            yield break;
        }

        int populationCap = source == "Exit"
            ? Mathf.Clamp(exitDoorPopulationCap, 0, OriginalStageEnemies.MaxEnemies)
            : OriginalStageEnemies.MaxEnemies;
        int released = 0;
        for (int alive = CountLivingEnemies(); alive < populationCap; alive++)
        {
            if (!TryFindSafeEnemySpawnCell(spawnCell, out Vector2Int cell))
            {
                Debug.LogWarning($"{source} could only spawn {released} enemies more than five tiles from the player.", this);
                break;
            }

            Vector3 position = map.CellToWorld(cell);
            EnemyController enemy = Instantiate(prefab, position, Quaternion.identity);
            enemy.name = $"{prefab.name} ({source})";
            RegisterEnemy(enemy);
            released++;
        }

        LogSpawnEvent($"{source} at {spawnCell} released {released} {prefab.name}.");
    }

    private IEnumerator ShowExplosion(Vector2Int cell, Vector2Int origin)
    {
        int step = Mathf.Abs(cell.x - origin.x) + Mathf.Abs(cell.y - origin.y);
        GameObject blast = BlastEffects.CreateFire($"Explosion {cell.x},{cell.y}", map.CellToWorld(cell), step);
        blast.transform.SetParent(stageRoot.transform, true);
        yield return WaitForGameplaySeconds(BlastEffects.Duration + step * BlastEffects.SpreadDelay);
        Destroy(blast);
    }

    private IEnumerator ShowDebris(Vector2Int cell, Material blockMaterial, int step)
    {
        GameObject debris = BlastEffects.CreateDebris($"Debris {cell.x},{cell.y}", map.CellToWorld(cell), blockMaterial, step);
        debris.transform.SetParent(stageRoot.transform, true);
        yield return WaitForGameplaySeconds(BlastEffects.Duration + step * BlastEffects.SpreadDelay);
        Destroy(debris);
    }

    private void DamageActorsAt(Vector2Int cell)
    {
        foreach (GridController actor in actors)
        {
            if (actor != null && !actor.IsDead && actor.Cell == cell)
            {
                if (actor.IsPlayer && (!playerCanDieFromBomb || powerUpState.flamePass))
                {
                    continue;
                }

                if (actor.IsPlayer)
                {
                    StartPlayerDeath(PlayerDeathCause.Bomb);
                    continue;
                }

                AddScore(GetEnemyScore(actor));
                SpawnActorDeathParticles(actor, "Enemy Death Particles", playerDeathParticleTime);
                actor.Die();
            }
        }
    }

    private void SpawnActorDeathParticles(GridController actor, string objectName, float lifetime)
    {
        actor.GetParticleColors(out Color primaryColor, out Color secondaryColor);
        GameObject particle = ExplosionParticleFactory.CreateColorBurst(objectName, actor.transform.position, primaryColor, secondaryColor);
        particle.transform.SetParent(stageRoot.transform, true);
        Destroy(particle, lifetime);
    }

    // ---- Power-ups -------------------------------------------------------------------------------------------

    // Hides this stage's power-up under a random breakable block (never the one hiding the exit). The cell
    // comes from its own random sequence, so adding power-ups never changes the layout or the exit.
    private void PlanHiddenPowerUp(int stage, int layoutSeed)
    {
        hasHiddenPowerUp = false;
        if (!spawnPowerUps || map == null) return;

        Vector2Int exitCell = map.ExitDoor != null ? map.ExitDoor.Cell : new Vector2Int(-1, -1);
        List<Vector2Int> hidingCells = new();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2Int cell = new(x, y);
                if (cell != exitCell && map.GetCellKind(cell) == CellKind.Destructible)
                {
                    hidingCells.Add(cell);
                }
            }
        }

        if (hidingCells.Count == 0) return;

        System.Random cellRandom = new(unchecked(layoutSeed ^ 0x1F2E3D4C));
        hiddenPowerUpCell = hidingCells[cellRandom.Next(hidingCells.Count)];
        hiddenPowerUpType = PickPowerUpType(stage);
        hasHiddenPowerUp = true;
        LogSpawnEvent($"Stage {stage} hides a {hiddenPowerUpType} power-up under the block at {hiddenPowerUpCell}.");
    }

    // The same stage always hides the same power-up, whatever the layout or session.
    private PowerUpType PickPowerUpType(int stage)
    {
        if (stagePowerUps != null && stagePowerUps.Length > 0)
        {
            return stagePowerUps[(Mathf.Max(1, stage) - 1) % stagePowerUps.Length];
        }

        return OriginalStagePowerUps.GetForStage(stage, stageManager != null ? stageManager.TotalStages : 0);
    }

    private void RevealPowerUp(Vector2Int cell)
    {
        if (!hasHiddenPowerUp || cell != hiddenPowerUpCell) return;

        hasHiddenPowerUp = false;
        int index = (int)hiddenPowerUpType;
        GameObject prefab = powerUpPrefabs != null && index < powerUpPrefabs.Length ? powerUpPrefabs[index] : null;
        map.HideGrassAt(cell);
        powerUps.Add(PowerUp.Create(hiddenPowerUpType, cell, map.CellToWorld(cell), stageRoot.transform, prefab));
        LogSpawnEvent($"A {hiddenPowerUpType} power-up appeared at {cell}.");
    }

    private void CollectPowerUps()
    {
        if (powerUps.Count == 0 || IsPaused || restarting || stageClearing || player == null || player.IsDead) return;

        Vector3 playerPosition = player.transform.position;
        for (int i = powerUps.Count - 1; i >= 0; i--)
        {
            PowerUp item = powerUps[i];
            if (item == null)
            {
                powerUps.RemoveAt(i);
                continue;
            }

            Vector3 offset = item.transform.position - playerPosition;
            offset.y = 0f;
            if (offset.sqrMagnitude > powerUpPickupDistance * powerUpPickupDistance) continue;

            powerUps.RemoveAt(i);
            map.ShowGrassAt(item.Cell);
            ApplyPowerUp(item.Type);
            Destroy(item.gameObject);
        }
    }

    private void ApplyPowerUp(PowerUpType type, bool rewardPickup = true)
    {
        string result;
        switch (type)
        {
            case PowerUpType.BombUp:
                if (maxBombs < maxBombLimit)
                {
                    maxBombs++;
                    powerUpState.bombUps++;
                }

                result = $"bombs at once: {maxBombs}";
                break;
            case PowerUpType.FireUp:
                if (blastRange < blastRangeLimit)
                {
                    blastRange++;
                    powerUpState.fireUps++;
                }

                result = $"blast range: {blastRange}";
                break;
            case PowerUpType.SpeedUp:
                if (powerUpState.speedUps < speedUpLimit)
                {
                    powerUpState.speedUps++;
                    player.MoveSpeed = GetPlayerMoveSpeed();
                    player.FreeMoveAcceleration = GetPlayerMoveAcceleration();
                    player.FreeMoveDeceleration = GetPlayerMoveDeceleration();
                }

                result = $"speed level: {powerUpState.speedUps}";
                break;
            case PowerUpType.Detonator:
                powerUpState.detonator = true;
                result = "bombs wait for B, X, or Left Ctrl; press once per bomb";
                break;
            case PowerUpType.BombPass:
                powerUpState.bombPass = true;
                result = "walks through bombs";
                break;
            case PowerUpType.WallPass:
                powerUpState.wallPass = true;
                player.WallPass = true;
                result = "walks through breakable blocks";
                break;
            case PowerUpType.FlamePass:
                powerUpState.flamePass = true;
                result = "blasts no longer hurt";
                break;
            default:
                mysteryTimeLeft = mysteryDuration;
                result = $"invincible for {mysteryDuration:0.#}s";
                break;
        }

        if (rewardPickup)
        {
            SoundManager.Instance?.PlayPowerUpPickup();
            AddScore(powerUpScore);
        }
        LogSpawnEvent($"Picked up {type}: {result}.");
    }

    // The detonator key and the Mystery countdown. Both follow gameplay time, so a pause freezes them.
    private void UpdatePowerUps()
    {
        if (powerUpState.detonator && HasLivingPlayer && !IsPaused && !restarting && !stageClearing
            && (WasPressed(Key.B) || WasPressed(Key.X) || WasPressed(Key.LeftCtrl)))
        {
            DetonateOldestBomb();
        }

        if (mysteryTimeLeft > 0f)
        {
            if (!IsPaused)
            {
                mysteryTimeLeft = Mathf.Max(0f, mysteryTimeLeft - Time.deltaTime);
            }

            if (player != null)
            {
                player.SetInvincibleMaterials(mysteryTimeLeft > 0f);
            }
        }
        else if (player != null)
        {
            player.SetInvincibleMaterials(false);
        }
    }

    private void DetonateOldestBomb()
    {
        Bomb oldest = null;
        foreach (Bomb bomb in bombs.Values)
        {
            if (bomb == null || bomb.DetonateRequested) continue;
            if (oldest == null || bomb.PlacedOrder < oldest.PlacedOrder)
            {
                oldest = bomb;
            }
        }

        if (oldest != null)
        {
            oldest.DetonateRequested = true;
        }
    }

    private void ResetPowerUps()
    {
        maxBombs = Mathf.Max(1, maxBombs - powerUpState.bombUps);
        blastRange = Mathf.Max(1, blastRange - powerUpState.fireUps);
        powerUpState = new PowerUpState();
        mysteryTimeLeft = 0f;
        if (player != null)
        {
            player.MoveSpeed = GetPlayerMoveSpeed();
            player.FreeMoveAcceleration = GetPlayerMoveAcceleration();
            player.FreeMoveDeceleration = GetPlayerMoveDeceleration();
            player.WallPass = false;
            player.SetInvincibleMaterials(false);
        }
    }

    private void ResetTemporaryPowerUpsOnDeath()
    {
        int cheatBombUps = Mathf.Clamp(powerUpState.cheatBombUps, 0, powerUpState.bombUps);
        int cheatFireUps = Mathf.Clamp(powerUpState.cheatFireUps, 0, powerUpState.fireUps);
        int cheatSpeedUps = Mathf.Clamp(powerUpState.cheatSpeedUps, 0, powerUpState.speedUps);
        maxBombs = Mathf.Max(baseMaxBombs, maxBombs - cheatBombUps);
        blastRange = Mathf.Max(baseBlastRange, blastRange - cheatFireUps);
        powerUpState.bombUps -= cheatBombUps;
        powerUpState.fireUps -= cheatFireUps;
        powerUpState.speedUps -= cheatSpeedUps;
        powerUpState.cheatBombUps = 0;
        powerUpState.cheatFireUps = 0;
        powerUpState.cheatSpeedUps = 0;
        powerUpState.detonator = false;
        powerUpState.bombPass = false;
        powerUpState.wallPass = false;
        powerUpState.flamePass = false;
        mysteryTimeLeft = 0f;
    }

    // Continue: the title screen asks for the saved stage, and the run saved at that stage's start comes back with it.
    // A new game clears the save first, and a saved stage number without run data starts that stage fresh.
    private void RestoreSavedRun()
    {
        if (GameProgress.RequestedStage < 1 || !GameProgress.TryLoadRun(GameProgress.RequestedStage, out GameProgress.RunData run))
        {
            return;
        }

        PowerUpState saved = run.powerUps ?? new PowerUpState();
        saved.bombUps = Mathf.Clamp(saved.bombUps, 0, Mathf.Max(0, maxBombLimit - baseMaxBombs));
        saved.fireUps = Mathf.Clamp(saved.fireUps, 0, Mathf.Max(0, blastRangeLimit - baseBlastRange));
        saved.speedUps = Mathf.Clamp(saved.speedUps, 0, speedUpLimit);
        saved.cheatBombUps = Mathf.Clamp(saved.cheatBombUps, 0, saved.bombUps);
        saved.cheatFireUps = Mathf.Clamp(saved.cheatFireUps, 0, saved.fireUps);
        saved.cheatSpeedUps = Mathf.Clamp(saved.cheatSpeedUps, 0, saved.speedUps);
        powerUpState = saved;
        maxBombs = baseMaxBombs + saved.bombUps;
        blastRange = baseBlastRange + saved.fireUps;
        score = Mathf.Max(0, run.score);
        livesLeft = Mathf.Max(0, run.lives);
        LogSpawnEvent($"Continue: stage {run.stage}, score {score}, lives {livesLeft}, bombs {maxBombs}, range {blastRange}, speed level {saved.speedUps}.");
    }

    public void SetGodMode(bool enabled) => godMode = enabled;

    public void ToggleGodMode() => SetGodMode(!godMode);

    public void OpenCheatMenu()
    {
        if (!showCheatMenuButton || cheatMenu == null || transitionActive || restarting || stageClearing) return;
        if (!cheatMenuOpen)
        {
            wasPausedBeforeCheatMenu = paused;
            cheatMenuOpen = true;
        }
        SetPaused(true);
        cheatMenu.SetVisible(true);
    }

    public void CloseCheatMenu()
    {
        if (!cheatMenuOpen) return;
        cheatMenuOpen = false;
        cheatMenu.SetVisible(false);
        SetPaused(wasPausedBeforeCheatMenu);
    }

    public void CheatSkipToNextStage()
    {
        if (stageManager == null || stageManager.OnLastStage || transitionActive || restarting || stageClearing) return;
        CloseCheatMenu();
        stageManager.NextStage();
    }

    public bool CheatGoToStage(int stage, out string error)
    {
        error = null;
        if (stageManager == null || !HasLivingPlayer || transitionActive || restarting || stageClearing)
        {
            error = "Stage change is unavailable right now.";
            return false;
        }

        if (stage < 1)
        {
            error = "Stage number must be 1 or higher.";
            return false;
        }

        int stageLimit = stageManager.TotalStages;
        if (stageLimit > 0 && stage > stageLimit)
        {
            error = $"Enter a stage from 1 to {stageLimit}.";
            return false;
        }

        CloseCheatMenu();
        stageManager.LoadStage(stage);
        if (stageManager.CurrentStage == stage) return true;

        error = $"Stage {stage} is not available in this sequence.";
        return false;
    }

    public void CheatGrantPowerUp(PowerUpType type)
    {
        if (!HasLivingPlayer || transitionActive || restarting || stageClearing) return;
        int previousBombs = maxBombs;
        int previousRange = blastRange;
        int previousSpeedUps = powerUpState.speedUps;
        ApplyPowerUp(type, rewardPickup: false);
        powerUpState.cheatBombUps += Mathf.Max(0, maxBombs - previousBombs);
        powerUpState.cheatFireUps += Mathf.Max(0, blastRange - previousRange);
        powerUpState.cheatSpeedUps += Mathf.Max(0, powerUpState.speedUps - previousSpeedUps);
    }

    public void CheatGrantAllPowerUps()
    {
        if (!HasLivingPlayer || transitionActive || restarting || stageClearing) return;

        powerUpState.cheatBombUps += Mathf.Max(0, maxBombLimit - maxBombs);
        powerUpState.cheatFireUps += Mathf.Max(0, blastRangeLimit - blastRange);
        powerUpState.cheatSpeedUps += Mathf.Max(0, speedUpLimit - powerUpState.speedUps);
        maxBombs = maxBombLimit;
        blastRange = blastRangeLimit;
        powerUpState.bombUps = maxBombs - baseMaxBombs;
        powerUpState.fireUps = blastRange - baseBlastRange;
        powerUpState.speedUps = speedUpLimit;
        powerUpState.detonator = true;
        powerUpState.bombPass = true;
        powerUpState.wallPass = true;
        powerUpState.flamePass = true;
        mysteryTimeLeft = mysteryDuration;
        player.MoveSpeed = GetPlayerMoveSpeed();
        player.FreeMoveAcceleration = GetPlayerMoveAcceleration();
        player.FreeMoveDeceleration = GetPlayerMoveDeceleration();
        player.WallPass = true;
        LogSpawnEvent("Cheat granted all power-ups at their limits.");
    }

    // Score for one kill, by enemy type. Unknown types give 0.
    private int GetEnemyScore(GridController actor)
    {
        return actor switch
        {
            ValcomEnemy => valcomScore,
            OnealEnemy => onealScore,
            DahlEnemy => dahlScore,
            MinuoEnemy => minuoScore,
            DoriaEnemy => doriaScore,
            OvapeEnemy => ovapeScore,
            PassEnemy => passScore,
            PontanEnemy => pontanScore,
            _ => 0
        };
    }

    // Adds to the running score and refreshes the HUD. Called for enemy kills (DamageActorsAt, per-type score),
    // the stage-clear time bonus (ClearStage), and PowerUp pickups.
    public void AddScore(int amount)
    {
        if (amount == 0) return;
        score += amount;
        stageHud.SetScore(score);
    }

    private void StartPlayerDeath(PlayerDeathCause cause)
    {
        if (IsInvincible || IsPaused || restarting || stageClearing || player == null || player.IsDead)
        {
            return;
        }

        StartCoroutine(PlayerDeathSequence(cause));
    }

    private void KillPlayerIfEnemyTouches()
    {
        if (IsPaused || restarting || player == null || player.IsDead)
        {
            return;
        }

        foreach (GridController actor in actors)
        {
            if (actor == null || actor.IsPlayer || actor.IsDead)
            {
                continue;
            }

            float distance = Vector3.Distance(actor.transform.position, player.transform.position);
            if (actor.Cell == player.Cell || distance <= enemyPlayerHitDistance)
            {
                StartPlayerDeath(PlayerDeathCause.Enemy);
                return;
            }
        }
    }

    // The exit only opens once every enemy is dead. Space enters it when the player is nearby.
    private void UpdateExitDoor()
    {
        ExitDoor exitDoor = map != null ? map.ExitDoor : null;
        if (exitDoor == null || stageClearing)
        {
            return;
        }

        bool shouldOpen = pendingExitWaves == 0 && CountLivingEnemies() == 0;
        if (shouldOpen && !exitDoor.IsOpen)
        {
            SoundManager.Instance?.PlayExitOpen();
        }
        exitDoor.SetOpen(shouldOpen);
    }

    private bool TryEnterExit()
    {
        ExitDoor exitDoor = map != null ? map.ExitDoor : null;
        if (exitDoor == null || !exitDoor.IsRevealed || !exitDoor.IsOpen || IsPaused || restarting || stageClearing || player == null || player.IsDead)
        {
            return false;
        }

        Vector3 offset = player.transform.position - map.CellToWorld(exitDoor.Cell);
        offset.y = 0f;
        if (offset.sqrMagnitude > exitDoorEnterDistance * exitDoorEnterDistance)
        {
            return false;
        }

        ClearStage();
        return true;
    }

    private void ClearStage()
    {
        stageClearing = true;

        // Time bonus: whole seconds left on the clock times the per-second reward. Stages without
        // a timer (stageTimeLimit == 0) award none, since timeLeft has no meaning there.
        if (stageTimeLimit > 0f && timeBonusPerSecond > 0)
        {
            AddScore(Mathf.RoundToInt(timeLeft) * timeBonusPerSecond);
        }

        // Clearing a stage grants an extra life, the "Left" system's reward for finishing a stage.
        if (stageClearLifeBonus > 0)
        {
            livesLeft += stageClearLifeBonus;
            stageHud.SetLives(livesLeft);
        }

        // A sequence ends after its last stage; without one the stages keep coming.
        if (stageManager.OnLastStage)
        {
            StartCoroutine(GameClearSequence());
            return;
        }

        stageManager.NextStage();
    }

    // The run is won, so the ending reads like GAME OVER without being one.
    private IEnumerator GameClearSequence()
    {
        GameProgress.Clear();
        GameProgress.RequestedStage = 0;
        LogSpawnEvent($"Stage {stageManager.CurrentStage} of {stageManager.TotalStages} cleared. Returning to the title screen.");
        yield return ShowEndingCard(gameClearText, gameClearDelay);

        // The smoke tests build scenes without the title, so those keep playing instead.
        if (Application.CanStreamedLevelBeLoaded(titleSceneName))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(titleSceneName);
            yield break;
        }

        transitionOverlay.Hide();
        transitionActive = false;
        Time.timeScale = 1f;
        livesLeft = playerLives;
        stageHud.SetLives(livesLeft);
        score = 0;
        stageHud.SetScore(score);
        ResetPowerUps();
        stageManager.LoadStage(1);
    }

    private IEnumerator PlayerDeathSequence(PlayerDeathCause cause)
    {
        restarting = true;
        SetPaused(false);
        ResetTemporaryPowerUpsOnDeath();
        player.BeginDeathAnimation(cause == PlayerDeathCause.Bomb);
        SoundManager.Instance?.PlayPlayerDeath();

        yield return new WaitForSeconds(playerDeathAnimationTime);

        if (player != null)
        {
            player.GetParticleColors(out Color primaryColor, out Color secondaryColor);
            GameObject particle = ExplosionParticleFactory.CreateColorBurst(
                cause == PlayerDeathCause.Bomb ? "Player Bomb Death Particles" : "Player Enemy Death Particles",
                player.transform.position,
                primaryColor,
                secondaryColor);
            particle.transform.SetParent(stageRoot.transform, true);
            Destroy(player.gameObject);
            Destroy(particle, playerDeathParticleTime);
        }

        yield return new WaitForSeconds(playerDeathParticleTime);
        yield return new WaitForSeconds(playerRestartDelay);
        Time.timeScale = 1f;
        livesLeft--;
        stageHud.SetLives(livesLeft);
        if (livesLeft > -1)
        {
            stageManager.RestartStage(randomizeLayout: true);
            yield break;
        }

        yield return GameOverSequence();
    }

    private IEnumerator GameOverSequence()
    {
        GameProgress.Clear();
        GameProgress.RequestedStage = 0;
        LogSpawnEvent("Out of lives. Returning to the title screen.");
        yield return ShowEndingCard("GAME OVER", gameOverDelay);

        // The smoke tests build scenes without the title, so those keep playing instead.
        if (Application.CanStreamedLevelBeLoaded(titleSceneName))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(titleSceneName);
            yield break;
        }

        transitionOverlay.Hide();
        transitionActive = false;
        Time.timeScale = 1f;
        livesLeft = playerLives;
        stageHud.SetLives(livesLeft);
        score = 0;
        stageHud.SetScore(score);
        ResetPowerUps();
        stageManager.RestartStage();
    }

    private IEnumerator StageIntroSequence(int stage)
    {
        transitionActive = true;
        Time.timeScale = 0f;
        transitionOverlay.Show($"STAGE {stage}", 1f);
        yield return new WaitForSecondsRealtime(StageCardHoldSeconds);
        yield return FadeTransition(1f, 0f);
        transitionOverlay.Hide();
        transitionActive = false;
        Time.timeScale = paused ? 0f : 1f;
    }

    private IEnumerator ShowEndingCard(string message, float holdDuration)
    {
        transitionActive = true;
        Time.timeScale = 0f;
        transitionOverlay.Show(message, 0f);
        yield return FadeTransition(0f, 1f);
        yield return new WaitForSecondsRealtime(holdDuration);
    }

    private IEnumerator FadeTransition(float startAlpha, float targetAlpha)
    {
        float elapsed = 0f;
        while (elapsed < TransitionFadeSeconds)
        {
            elapsed = Mathf.Min(TransitionFadeSeconds, elapsed + Time.unscaledDeltaTime);
            float amount = elapsed / TransitionFadeSeconds;
            transitionOverlay.SetAlpha(Mathf.Lerp(startAlpha, targetAlpha, amount));
            yield return null;
        }

        transitionOverlay.SetAlpha(targetAlpha);
    }

    private IEnumerator WaitForGameplaySeconds(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (!IsPaused)
            {
                elapsed += Time.deltaTime;
            }
            yield return null;
        }
    }

    private void SetPaused(bool value)
    {
        paused = value;
        Time.timeScale = paused || transitionActive ? 0f : 1f;
        if (pauseMenu != null)
        {
            pauseMenu.SetVisible(paused);
        }
    }
}
