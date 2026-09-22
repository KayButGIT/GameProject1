using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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
    [SerializeField, Min(0f)] private float playerDeathAnimationTime = 0.85f;
    [SerializeField, Min(0f)] private float playerDeathParticleTime = 0.6f;
    [SerializeField] private float playerRestartDelay = 0.15f;

    [Header("Enemies")]
    [SerializeField] private bool spawnEnemies = false;
    [SerializeField] private int onealCount = 3;
    [SerializeField] private int dahlCount = 3;
    [SerializeField] private int pontanCount = 3;
    [SerializeField] private int passCount = 3;
    [SerializeField] private int valcomCount = 3;
    [SerializeField] private int ovapeCount = 3;
    [SerializeField] private int doriaCount = 3;
    [SerializeField] private int minuoCount = 3;
    [SerializeField, Min(0.05f)] private float enemyPlayerHitDistance = 0.45f;

    [Tooltip("Enemy prefab settings control movement and behavior.")]
    [SerializeField] private EnemyController[] enemyPrefabs;

    [Header("Exit Door")]
    [Tooltip("Hidden under a random destructible wall until that wall is destroyed. Opens once every enemy in the scene has been killed.")]
    [SerializeField, Min(0.05f)] private float doorReachDistance = 0.45f;

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
    private ExitDoor exitDoor;
    private int activeBombs;
    private bool paused;
    private bool restarting;
    private bool levelCompleted;

    public bool IsPaused => paused;
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
        map = new BombermanMap(width, height, destructibleDensity, randomSeed, materials);

        ConfigureCamera();
        map.Generate();
        SpawnPlayer();
        SpawnExitDoor();
        foreach (EnemyController enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            RegisterEnemy(enemy);
        if (spawnEnemies)
        {
            SpawnEnemies();
        }
        UpdateExitDoorLockState();
        pauseMenu = PauseMenu.Create();
    }

    private void Update()
    {
        if (WasPressed(Key.Escape))
        {
            SetPaused(!paused);
        }
    }

    private void LateUpdate()
    {
        UpdateCameraFollow();
        KillPlayerIfEnemyTouches();
        CheckPlayerReachedExit();
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
        mainCamera.clearFlags = CameraClearFlags.Skybox;

        Light light = FindFirstObjectByType<Light>();
        if (light != null)
        {
            light.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            light.intensity = 1.3f;
        }
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
        actors.Add(player);
    }

    private void SpawnExitDoor()
    {
        Vector2Int doorCell = FindExitDoorCell();
        exitDoor = ExitDoor.Create(doorCell, map, materials.ExitDoorLocked, materials.ExitDoorUnlocked);

        // Edge case: destructibleDensity is 0 (or every destructible cell got filtered out), so there is
        // no wall to hide the door under. Show it immediately instead of leaving it stuck invisible.
        if (map.GetCellKind(doorCell) != CellKind.Destructible)
        {
            exitDoor.Reveal();
        }
    }

    private Vector2Int FindExitDoorCell()
    {
        List<Vector2Int> candidates = new();
        Vector2Int playerStart = new(1, 1);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2Int cell = new(x, y);
                if (map.GetCellKind(cell) == CellKind.Destructible && Vector2Int.Distance(cell, playerStart) > 4f)
                {
                    candidates.Add(cell);
                }
            }
        }

        if (candidates.Count == 0)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Vector2Int cell = new(x, y);
                    if (map.GetCellKind(cell) == CellKind.Destructible)
                    {
                        candidates.Add(cell);
                    }
                }
            }
        }

        if (candidates.Count > 0)
        {
            return candidates[Random.Range(0, candidates.Count)];
        }

        // Ultimate fallback if the map has no destructible walls at all.
        return new Vector2Int(width - 2, height - 2);
    }

    private void SpawnEnemies()
    {
        SpawnEnemyGroup<OnealEnemy>("O'neal", onealCount);
        SpawnEnemyGroup<DahlEnemy>("Dahl", dahlCount);
        SpawnEnemyGroup<PontanEnemy>("Pontan", pontanCount);
        SpawnEnemyGroup<PassEnemy>("Pass", passCount);
        SpawnEnemyGroup<ValcomEnemy>("Valcom", valcomCount);
        SpawnEnemyGroup<OvapeEnemy>("Ovape", ovapeCount);
        SpawnEnemyGroup<DoriaEnemy>("Doria", doriaCount);
        SpawnEnemyGroup<MinuoEnemy>("Minuo", minuoCount);
    }

    private void SpawnEnemyGroup<TEnemy>(string enemyName, int count) where TEnemy : EnemyController
    {
        EnemyController prefab = null;
        if (enemyPrefabs != null)
            foreach (EnemyController candidate in enemyPrefabs)
                if (candidate is TEnemy) { prefab = candidate; break; }
        if (prefab == null)
        {
            Debug.LogError($"Assign the {enemyName} prefab to Enemy Prefabs on {name}.", this);
            return;
        }
        for (int i = 0; i < count; i++)
        {
            EnemyController enemy = Instantiate(prefab, map.CellToWorld(FindEnemySpawnCell()), Quaternion.identity);
            enemy.name = $"{enemyName} {i + 1}";
            RegisterEnemy(enemy);
        }
    }

    public void RegisterEnemy(EnemyController enemy)
    {
        if (!IsReady || enemy == null || actors.Contains(enemy)) return;
        enemy.Cell = map.WorldToCell(enemy.transform.position);
        enemy.transform.position = map.CellToWorld(enemy.Cell);
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
                    if (kind == CellKind.Destructible) break;
                }
        }
        return false;
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
        if (paused || player == null || player.IsDead)
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
                    map.DestroyDestructibleAt(cell);
                    if (exitDoor != null && cell == exitDoor.Cell)
                    {
                        exitDoor.Reveal();
                    }
                    break;
                }
            }
        }

        foreach (Vector2Int cell in blastCells)
        {
            StartCoroutine(ShowExplosion(cell));
            DamageActorsAt(cell);
        }
    }

    private IEnumerator ShowExplosion(Vector2Int cell)
    {
        GameObject particle = ExplosionParticleFactory.CreateCartoonFlame(
            $"Explosion {cell.x},{cell.y}",
            map.CellToWorld(cell));
        yield return new WaitForSeconds(1.1f);
        Destroy(particle);
    }

    private void DamageActorsAt(Vector2Int cell)
    {
        bool enemyDiedThisCall = false;

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
                enemyDiedThisCall = true;
            }
        }

        if (enemyDiedThisCall)
        {
            UpdateExitDoorLockState();
        }
    }

    private void SpawnActorDeathParticles(GridController actor, string objectName, float lifetime)
    {
        actor.GetParticleColors(out Color primaryColor, out Color secondaryColor);
        GameObject particle = ExplosionParticleFactory.CreateColorBurst(objectName, actor.transform.position, primaryColor, secondaryColor);
        Destroy(particle, lifetime);
    }

    private void StartPlayerDeath(PlayerDeathCause cause)
    {
        if (restarting || player == null || player.IsDead)
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

    // Enemies only die inside DamageActorsAt, so that call site keeps the door state accurate;
    // this also runs once at Start to cover scenes with no enemies configured.
    private void UpdateExitDoorLockState()
    {
        if (exitDoor == null || exitDoor.IsUnlocked)
        {
            return;
        }

        if (!AnyEnemyAliveInScene())
        {
            exitDoor.Unlock();
        }
    }

    private bool AnyEnemyAliveInScene()
    {
        foreach (GridController actor in actors)
        {
            if (actor != null && !actor.IsPlayer && !actor.IsDead)
            {
                return true;
            }
        }

        return false;
    }

    private void CheckPlayerReachedExit()
    {
        if (levelCompleted || paused || restarting || exitDoor == null || !exitDoor.IsUnlocked
            || player == null || player.IsDead)
        {
            return;
        }

        float distance = Vector3.Distance(player.transform.position, map.CellToWorld(exitDoor.Cell));
        if (player.Cell == exitDoor.Cell || distance <= doorReachDistance)
        {
            StartCoroutine(WinSequence());
        }
    }

    private IEnumerator WinSequence()
    {
        levelCompleted = true;
        Debug.Log("Stage Clear!");
        // TODO: replace with a real win screen or SceneManager.LoadScene for the next stage.
        yield break;
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
            Destroy(player.gameObject);
            Destroy(particle, playerDeathParticleTime);
        }

        yield return new WaitForSeconds(playerDeathParticleTime);
        yield return new WaitForSeconds(playerRestartDelay);
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
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