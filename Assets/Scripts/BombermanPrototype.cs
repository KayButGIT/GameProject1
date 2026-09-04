using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public sealed class BombermanPrototype : MonoBehaviour
{
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
    [SerializeField] private float playerAcceleration = 28f;
    [SerializeField] private float playerDeceleration = 36f;
    [SerializeField] private float playerTurnSpeed = 14f;
    [SerializeField] private GameObject playerModelPrefab;
    [SerializeField] private float playerModelScale = 8.91f;
    [SerializeField] private RuntimeAnimatorController playerAnimatorController;
    [SerializeField] private GameObject bombModelPrefab;
    [SerializeField] private int maxBombs = 1;
    [SerializeField] private int blastRange = 1;
    [SerializeField] private float bombFuseTime = 2f;
    [SerializeField] private bool playerCanDieFromBomb = false;
    [SerializeField] private float playerRestartDelay = 1f;

    [Header("Enemies")]
    [SerializeField] private bool spawnEnemies = false;
    [SerializeField] private int onealCount = 3;
    [SerializeField] private int dahlCount = 3;
    [SerializeField] private float enemyMoveSpeed = 3f;

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

    public bool IsPaused => paused;
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
        if (spawnEnemies)
        {
            SpawnEnemies();
        }
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
            playerModelPrefab,
            playerModelScale,
            playerAnimatorController);
        player.MoveSpeed = playerMoveSpeed;
        player.FreeMoveAcceleration = playerAcceleration;
        player.FreeMoveDeceleration = playerDeceleration;
        player.FreeMoveTurnSpeed = playerTurnSpeed;
        player.Initialize(this);
        actors.Add(player);
    }

    private void SpawnEnemies()
    {
        // Enemy spawn is disabled for now; turn on spawnEnemies later when enemy gameplay returns.
        SpawnEnemyGroup<OnealEnemy>("O'neal", onealCount, materials.Oneal, 0.85f);
        SpawnEnemyGroup<DahlEnemy>("Dahl", dahlCount, materials.Dahl, 0.75f);
    }

    private void SpawnEnemyGroup<TEnemy>(string enemyName, int count, Material material, float scale)
        where TEnemy : EnemyController
    {
        for (int i = 0; i < count; i++)
        {
            // Enemy spawn is centralized here so each enemy type can reuse the same placement rules.
            Vector2Int cell = FindEnemySpawnCell();
            TEnemy enemy = EnemyController.Create<TEnemy>($"{enemyName} {i + 1}", cell, map, material, materials.EnemyDead, scale);
            enemy.MoveSpeed = enemyMoveSpeed;
            enemy.Initialize(this);
            actors.Add(enemy);
        }
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

        Vector2Int target = controller.Cell + direction;
        if (IsWalkable(target))
        {
            controller.StartMove(target, map.CellToWorld(target));
        }
    }

    private bool IsWalkable(Vector2Int cell)
    {
        return map.IsWalkable(cell)
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
        bomb.Initialize(bombCell, player.transform, 0.9f);
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
        foreach (GridController actor in actors)
        {
            if (actor != null && !actor.IsDead && actor.Cell == cell)
            {
                if (actor.IsPlayer && !playerCanDieFromBomb)
                {
                    continue;
                }

                actor.Die();
                if (actor.IsPlayer && !restarting)
                {
                    // Player death flow: bomb damage destroys the player model, then restarts the scene.
                    StartCoroutine(RestartAfterPlayerDeath());
                }
            }
        }
    }

    private IEnumerator RestartAfterPlayerDeath()
    {
        restarting = true;
        SetPaused(false);
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
