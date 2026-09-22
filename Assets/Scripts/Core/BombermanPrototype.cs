using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-100)]
public sealed class BombermanPrototype : MonoBehaviour
{
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
    [SerializeField] private Material playerFaceNormalMaterial;
    [SerializeField] private Material playerFaceBlinkMaterial;
    [SerializeField] private Material playerFaceDeadMaterial;
    [SerializeField] private Material playerFaceDeadBurntMaterial;
    [SerializeField] private GameObject bombModelPrefab;
    [SerializeField] private int maxBombs = 1;
    [SerializeField] private int blastRange = 1;
    [SerializeField] private float bombFuseTime = 2f;
    [SerializeField, Min(0f)] private float bombReleasePadding = 0.65f;
    [SerializeField] private bool playerCanDieFromBomb = false;
    [Tooltip("God mode: the player survives bombs, enemies, and the time-out Pontans.")]
    [SerializeField] private bool godMode;
    [Tooltip("Deaths allowed before the game ends, as in the original game.")]
    [SerializeField, Min(1)] private int playerLives = 3;
    [Tooltip("Scene loaded after GAME OVER. It must be in Build Settings.")]
    [SerializeField] private string titleSceneName = "Title";
    [Tooltip("Seconds the GAME OVER banner stays up before the title screen loads.")]
    [SerializeField, Min(0f)] private float gameOverDelay = 2.5f;
    [Tooltip("Banner shown once the sequence's last stage is cleared.")]
    [SerializeField] private string gameClearText = "ALL STAGES CLEAR";
    [Tooltip("Seconds that banner stays up before the title screen loads.")]
    [SerializeField, Min(0f)] private float gameClearDelay = 4f;
    [SerializeField, Min(0f)] private float playerDeathAnimationTime = 0.85f;
    [SerializeField, Min(0f)] private float playerDeathParticleTime = 0.6f;
    [SerializeField] private float playerRestartDelay = 0.15f;

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
    [Tooltip("How close the player must stand to the door center for Space to enter an open exit.")]
    [SerializeField, Min(0.05f)] private float exitDoorEnterDistance = 0.4f;
    [Tooltip("A blast on the revealed exit releases the stage's exit enemy from the original table until this many are alive. Zero disables it.")]
    [SerializeField, Min(0)] private int exitDoorPopulationCap = 10;
    [SerializeField, Min(0f)] private float exitDoorSpawnDelay = 0.5f;
    [SerializeField, Min(0f)] private float stageClearDelay = 2f;
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
    private Camera mainCamera;
    private Vector3 cameraBasePosition;
    private Vector3 cameraFollowVelocity;
    private Quaternion cameraRotation;
    private PlayerController player;
    private int activeBombs;
    private bool paused;
    private bool restarting;
    private StageManager stageManager;
    private GameObject stageRoot;
    private GameObject sceneryRoot;
    private StageTheme.SceneryVisual builtScenery;
    private GameObject sceneEnemyTemplates;
    private int sessionSeed;
    private bool firstStage = true;
    private PauseMenu stageClearBanner;
    private System.Type exitWaveEnemyType;
    private int pendingExitWaves;
    private bool stageClearing;
    private StageHud stageHud;
    private float timeLeft;
    private bool timeUp;
    private StageLighting stageLighting;
    private StageTheme.ThemeLighting currentLighting;
    private PauseMenu gameOverBanner;
    private PauseMenu gameClearBanner;
    private int livesLeft;

    public bool IsPaused => paused;
    public bool IsStageClearing => stageClearing;
    public bool HasLivingPlayer => player != null && !player.IsDead;
    public bool IsReady => map != null && player != null;
    public Vector2Int PlayerCell => player != null ? player.Cell : Vector2Int.zero;

    public bool IsBombBlockingCell(Vector2Int cell)
    {
        return bombs.TryGetValue(cell, out Bomb bomb)
            && bomb != null
            && bomb.BlocksMovement;
    }

    private void Start()
    {
        width = Mathf.Max(5, width | 1);
        height = Mathf.Max(5, height | 1);

        materials = BombermanMaterials.Create();
        sessionSeed = randomSeed == 0 ? System.Environment.TickCount : randomSeed;
        pauseMenu = PauseMenu.Create();
        stageClearBanner = PauseMenu.Create("Stage Clear Canvas", "STAGE CLEAR");
        gameOverBanner = PauseMenu.Create("Game Over Canvas", "GAME OVER");
        gameClearBanner = PauseMenu.Create("Game Clear Canvas", gameClearText);
        stageHud = StageHud.Create();
        livesLeft = playerLives;
        stageLighting = StageLighting.Create();
        sceneEnemyTemplates = new GameObject("Scene Enemy Templates");
        sceneEnemyTemplates.transform.SetParent(transform, false);
        sceneEnemyTemplates.SetActive(false);
        foreach (EnemyController enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            Instantiate(enemy.gameObject, sceneEnemyTemplates.transform, true);
        stageManager = GetComponent<StageManager>();
        if (stageManager == null) stageManager = gameObject.AddComponent<StageManager>();
        stageManager.Initialize(this);
    }

    internal void LoadStage(int stage, StageTheme theme)
    {
        StopAllCoroutines();
        SetPaused(false);
        restarting = false;
        stageClearing = false;
        pendingExitWaves = 0;
        stageClearBanner.SetVisible(false);
        gameOverBanner.SetVisible(false);
        gameClearBanner.SetVisible(false);
        timeLeft = stageTimeLimit;
        timeUp = false;
        stageHud.SetVisible(stageTimeLimit > 0f);
        stageHud.SetTime(timeLeft);
        stageHud.SetLives(livesLeft);
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
        map = new BombermanMap(width, height, destructibleDensity, GetStageSeed(sessionSeed, stage), materials, theme, stageRoot.transform);
        ConfigureCamera();
        currentLighting = theme != null ? theme.Lighting : null;
        stageLighting.Apply(currentLighting, mainCamera);
        map.Generate();
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
        exitWaveEnemyType = OriginalStageEnemies.GetExitEnemy(stage);
        LogSpawnEvent($"Stage {stage} starts with {DescribeEnemies()}. Bombing the open exit releases {exitWaveEnemyType.Name}.");
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

    private void Update()
    {
        if (WasPressed(Key.Escape))
        {
            SetPaused(!paused);
        }

        // Theme lighting edits in the Inspector show immediately while playing in the Editor.
        if (Application.isEditor && mainCamera != null)
        {
            stageLighting.Apply(currentLighting, mainCamera);
        }

        UpdateStageTimer();
    }

    private void UpdateStageTimer()
    {
        if (stageTimeLimit <= 0f || timeUp || paused || restarting || stageClearing || !HasLivingPlayer)
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

    // Like the original game, running out of time removes every enemy and fills the stage with Pontans.
    private void SpawnTimeOutPontans()
    {
        foreach (GridController actor in actors)
        {
            if (actor != null && !actor.IsPlayer)
            {
                Destroy(actor.gameObject);
            }
        }

        actors.RemoveAll(actor => actor == null || !actor.IsPlayer);
        SpawnEnemyGroup(typeof(PontanEnemy), OriginalStageEnemies.MaxEnemies, () => FindOriginalSpawnCell(1));
        LogSpawnEvent($"Time ran out. Every enemy was replaced by {OriginalStageEnemies.MaxEnemies} Pontans.");
    }

    private void LateUpdate()
    {
        UpdateCameraFollow();
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
            playerFaceNormalMaterial,
            playerFaceBlinkMaterial,
            playerFaceDeadMaterial,
            playerFaceDeadBurntMaterial);
        player.MoveSpeed = playerMoveSpeed;
        player.FreeMoveAcceleration = playerAcceleration;
        player.FreeMoveDeceleration = playerDeceleration;
        player.FreeMoveTurnSpeed = playerTurnSpeed;
        player.Initialize(this);
        player.transform.SetParent(stageRoot.transform, true);
        actors.Add(player);
    }

    private void SpawnEnemies(int stage)
    {
        if (useOriginalStageEnemies)
        {
            // Original rule: stage enemies start at least five columns from the player's corner.
            foreach ((System.Type enemyType, int count) in OriginalStageEnemies.GetRoster(stage))
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
        EnemyController prefab = FindEnemyPrefab(enemyType);
        if (prefab == null)
        {
            Debug.LogError($"Assign the {enemyType.Name} prefab to Enemy Prefabs on {name}.", this);
            return;
        }
        for (int i = 0; i < count; i++)
        {
            EnemyController enemy = Instantiate(prefab, map.CellToWorld(findCell()), Quaternion.identity);
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
        enemy.Cell = map.WorldToCell(enemy.transform.position);
        enemy.transform.position = map.CellToWorld(enemy.Cell);
        enemy.transform.SetParent(stageRoot.transform, true);
        actors.Add(enemy);
        enemy.Initialize(this);
    }

    public bool CanEnemyTraverse(EnemyController enemy, Vector2Int cell)
    {
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

    // Original RAND_COORDS rule: a random empty cell outside the player's starting corner.
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

        // Small or crowded maps take any free cell outside the corner.
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

        return new Vector2Int(width - 2, height - 2);
    }

    private bool IsOriginalSpawnCell(Vector2Int cell)
    {
        return IsWalkable(cell) && !IsActorAt(cell) && (cell.x >= 3 || cell.y >= 3);
    }
    private Vector2Int FindEnemySpawnCell()
    {
        for (int attempts = 0; attempts < 200; attempts++)
        {
            Vector2Int cell = new(Random.Range(width / 2, width - 2), Random.Range(1, height - 1));
            if (IsWalkable(cell) && !IsActorAt(cell) && Vector2Int.Distance(cell, new Vector2Int(1, 1)) > 8f)
            {
                return cell;
            }
        }

        return new Vector2Int(width - 2, height - 2);
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
        if (paused || stageClearing || player == null || player.IsDead)
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
        bombs[bomb.Cell] = bomb;
        activeBombs++;
        StartCoroutine(ExplodeAfterFuse(bomb));
    }

    private IEnumerator ExplodeAfterFuse(Bomb bomb)
    {
        yield return new WaitForSeconds(bombFuseTime);
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

        StageTheme.ThemeLighting lighting = StageLighting.Resolve(currentLighting);
        ExplosionFlash.Create(map.CellToWorld(origin) + Vector3.up, lighting.ExplosionColor, lighting.ExplosionIntensity, blastRange + 1.5f, stageRoot.transform);
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
            StartCoroutine(SpawnExitWave());
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

    private IEnumerator SpawnExitWave()
    {
        // A pending wave keeps the exit locked until its enemies exist.
        pendingExitWaves++;
        yield return new WaitForSeconds(exitDoorSpawnDelay);
        pendingExitWaves--;
        if (restarting)
        {
            yield break;
        }

        EnemyController prefab = FindEnemyPrefab(exitWaveEnemyType);
        if (prefab == null)
        {
            Debug.LogError($"Assign the {exitWaveEnemyType.Name} prefab to Enemy Prefabs on {name}.", this);
            yield break;
        }

        Vector3 position = map.CellToWorld(map.ExitDoor.Cell);
        int released = 0;
        for (int alive = CountLivingEnemies(); alive < exitDoorPopulationCap; alive++)
        {
            EnemyController enemy = Instantiate(prefab, position, Quaternion.identity);
            enemy.name = $"{prefab.name} (Exit)";
            RegisterEnemy(enemy);
            released++;
        }

        LogSpawnEvent($"The exit at {map.ExitDoor.Cell} released {released} {prefab.name}.");
    }

    private IEnumerator ShowExplosion(Vector2Int cell, Vector2Int origin)
    {
        int step = Mathf.Abs(cell.x - origin.x) + Mathf.Abs(cell.y - origin.y);
        GameObject blast = BlastEffects.CreateFire($"Explosion {cell.x},{cell.y}", map.CellToWorld(cell), step);
        blast.transform.SetParent(stageRoot.transform, true);
        yield return new WaitForSeconds(BlastEffects.Duration + step * BlastEffects.SpreadDelay);
        Destroy(blast);
    }

    private IEnumerator ShowDebris(Vector2Int cell, Material blockMaterial, int step)
    {
        GameObject debris = BlastEffects.CreateDebris($"Debris {cell.x},{cell.y}", map.CellToWorld(cell), blockMaterial, step);
        debris.transform.SetParent(stageRoot.transform, true);
        yield return new WaitForSeconds(BlastEffects.Duration + step * BlastEffects.SpreadDelay);
        Destroy(debris);
    }

    private void DamageActorsAt(Vector2Int cell)
    {
        foreach (GridController actor in actors)
        {
            if (actor != null && !actor.IsDead && actor.Cell == cell)
            {
                if (actor.IsPlayer && !playerCanDieFromBomb)
                {
                    continue;
                }

                if (actor.IsPlayer)
                {
                    StartPlayerDeath(PlayerDeathCause.Bomb);
                    continue;
                }

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
    
    public void SetGodMode(bool enabled) => godMode = enabled;

    private void StartPlayerDeath(PlayerDeathCause cause)
    {
        if (godMode || restarting || stageClearing || player == null || player.IsDead)
        {
            return;
        }

        StartCoroutine(PlayerDeathSequence(cause));
    }

    private void KillPlayerIfEnemyTouches()
    {
        if (paused || restarting || player == null || player.IsDead)
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

    // The exit only opens once every enemy is dead, and the player clears the stage by stepping onto it.
    private void UpdateExitDoor()
    {
        ExitDoor exitDoor = map != null ? map.ExitDoor : null;
        if (exitDoor == null || stageClearing)
        {
            return;
        }

        exitDoor.SetOpen(pendingExitWaves == 0 && CountLivingEnemies() == 0);
    }

    // Space on an open exit clears the stage instead of dropping a bomb.
    private bool TryEnterExit()
    {
        ExitDoor exitDoor = map != null ? map.ExitDoor : null;
        if (exitDoor == null || !exitDoor.IsRevealed || !exitDoor.IsOpen || restarting || player == null || player.IsDead)
        {
            return false;
        }

        Vector3 offset = player.transform.position - map.CellToWorld(exitDoor.Cell);
        offset.y = 0f;
        if (offset.sqrMagnitude > exitDoorEnterDistance * exitDoorEnterDistance)
        {
            return false;
        }

        StartCoroutine(StageClearSequence());
        return true;
    }

    private IEnumerator StageClearSequence()
    {
        stageClearing = true;
        stageClearBanner.SetVisible(true);
        yield return new WaitForSeconds(stageClearDelay);
        // A sequence ends after its last stage; without one the stages keep coming.
        if (stageManager.OnLastStage)
        {
            stageClearBanner.SetVisible(false);
            yield return GameClearSequence();
            yield break;
        }

        stageManager.NextStage();
    }

    // The run is won, so the ending reads like GAME OVER without being one.
    private IEnumerator GameClearSequence()
    {
        gameClearBanner.SetVisible(true);
        GameProgress.Clear();
        GameProgress.RequestedStage = 0;
        LogSpawnEvent($"Stage {stageManager.CurrentStage} of {stageManager.TotalStages} cleared. Returning to the title screen.");
        yield return new WaitForSeconds(gameClearDelay);

        // The smoke tests build scenes without the title, so those keep playing instead.
        if (Application.CanStreamedLevelBeLoaded(titleSceneName))
        {
            SceneManager.LoadScene(titleSceneName);
            yield break;
        }

        gameClearBanner.SetVisible(false);
        livesLeft = playerLives;
        stageHud.SetLives(livesLeft);
        stageManager.LoadStage(1);
    }

    private IEnumerator PlayerDeathSequence(PlayerDeathCause cause)
    {
        restarting = true;
        SetPaused(false);
        player.BeginDeathAnimation(cause == PlayerDeathCause.Bomb);

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
        if (livesLeft > 0)
        {
            stageManager.RestartStage();
            yield break;
        }

        yield return GameOverSequence();
    }

    private IEnumerator GameOverSequence()
    {
        gameOverBanner.SetVisible(true);
        GameProgress.Clear();
        GameProgress.RequestedStage = 0;
        LogSpawnEvent("Out of lives. Returning to the title screen.");
        yield return new WaitForSeconds(gameOverDelay);

        // The smoke tests build scenes without the title, so those keep playing instead.
        if (Application.CanStreamedLevelBeLoaded(titleSceneName))
        {
            SceneManager.LoadScene(titleSceneName);
            yield break;
        }

        gameOverBanner.SetVisible(false);
        livesLeft = playerLives;
        stageHud.SetLives(livesLeft);
        stageManager.RestartStage();
    }

    private void SetPaused(bool value)
    {
        paused = value;
        Time.timeScale = paused ? 0f : 1f;
        if (pauseMenu != null)
        {
            pauseMenu.SetVisible(paused);
        }
    }
}
