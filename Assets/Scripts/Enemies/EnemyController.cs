using UnityEngine;

public abstract class EnemyController : GridController
{
    public enum ChaseMode { None, Timed, Escape, Memory, PersistentAfterSight, Always }
    [Header("Enemy behavior")]
    [Tooltip("Timed pursues briefly; Escape/Memory can lose the target; PersistentAfterSight locks on after detection; Always tracks across the map.")]
    public ChaseMode Chase;
    [Tooltip("Chance per eligible cell-center encounter. A failed roll is not repeated while waiting at the same center.")]
    [Range(0f, 1f)] public float AcquisitionProbability = 1f;
    [Tooltip("Prefer neighbors outside current bomb blast lanes. Local avoidance only; does not predict fuse times or find escape paths.")]
    public bool AvoidBombLanes;
    [Min(1)] public int PatrolMinCells = 3;
    [Min(1)] public int PatrolMaxCells = 5;
    [Range(0f, 1f)] public float AxisChangeChance = 0.2f;
    [Tooltip("Manhattan distance for aligned row/column detection. Always pursuit ignores this limit.")]
    [Min(0)] public int DetectionRange = 4;
    [Min(0f)] public float ChaseMinSeconds = 3f;
    [Min(0f)] public float ChaseMaxSeconds = 4f;
    [Min(0f)] public float DetectionCooldown = 2f;
    [Tooltip("Unseen time before Escape/Memory pursuit ends. Persistent modes ignore this.")]
    [Min(0f)] public float MemorySeconds = 2f;
    [Min(0)] public int EscapeRange = 7;
    [Min(0f)] public float EscapeSeconds = 2f;
    [Header("Presentation")]
    public Animator ModelAnimator;
    [Min(0f)] public float DeathSeconds = 0.8f;
    public bool IsChasing { get; private set; }
    protected BombermanPrototype Game { get; private set; }
    private Vector2Int heading, lastSeen;
    private int patrolRemaining;
    private float chaseRemaining, cooldown, unseen, outside, deathRemaining;
    private bool initialized, hasSpeed, hasMoving, hasDie, movingAsPatrol;
    private bool acquisitionChecked, selectingSafeMoves;
    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private static readonly int MovingId = Animator.StringToHash("IsMoving");
    private static readonly int DieId = Animator.StringToHash("Die");

    protected virtual void Start()
    {
        if (!initialized)
        {
            BombermanPrototype game = FindFirstObjectByType<BombermanPrototype>();
            if (game != null) game.RegisterEnemy(this);
        }
    }

    public void Initialize(BombermanPrototype game)
    {
        if (initialized) return;
        Game = game;
        initialized = true;
        patrolRemaining = NextPatrolLength();
        if (ModelAnimator == null) ModelAnimator = GetComponentInChildren<Animator>();
        if (ModelAnimator != null)
        {
            ModelAnimator.applyRootMotion = false;
            ModelAnimator.updateMode = AnimatorUpdateMode.Normal;
            if (ModelAnimator.runtimeAnimatorController != null)
                foreach (AnimatorControllerParameter p in ModelAnimator.parameters)
                {
                    hasSpeed |= p.nameHash == SpeedId && p.type == AnimatorControllerParameterType.Float;
                    hasMoving |= p.nameHash == MovingId && p.type == AnimatorControllerParameterType.Bool;
                    hasDie |= p.nameHash == DieId && p.type == AnimatorControllerParameterType.Trigger;
                }
        }
    }

    protected override void Update()
    {
        if (!initialized || Game == null) return;
        if (ModelAnimator != null) ModelAnimator.speed = Game.IsPaused ? 0f : 1f;
        if (Game.IsPaused) return;
        if (IsDead)
        {
            deathRemaining -= Time.deltaTime;
            if (deathRemaining <= 0f) Destroy(gameObject);
            return;
        }
        bool wasMoving = IsMoving;
        base.Update();
        if (wasMoving && !IsMoving && movingAsPatrol) patrolRemaining--;
        if (wasMoving) acquisitionChecked = false;
        UpdateChase(Time.deltaTime);
        if (!IsMoving)
        {
            Vector2Int direction = ChooseDirection();
            if (direction != Vector2Int.zero)
            {
                heading = direction;
                movingAsPatrol = !IsChasing;
                Game.TryMoveActor(this, direction);
            }
        }
        SetAnimation();
    }

    private void UpdateChase(float delta)
    {
        cooldown = Mathf.Max(0f, cooldown - delta);
        if (Chase == ChaseMode.None || !Game.HasLivingPlayer)
        {
            IsChasing = false;
            return;
        }
        if (Chase == ChaseMode.Always || (Chase == ChaseMode.PersistentAfterSight && IsChasing))
        {
            IsChasing = true;
            lastSeen = Game.PlayerCell;
            return;
        }
        bool visible = CanSeePlayer();
        if (!IsChasing)
        {
            if (IsMoving || !visible || cooldown > 0f || acquisitionChecked) return;
            acquisitionChecked = true;
            if (AcquisitionProbability <= 0f || (AcquisitionProbability < 1f && Random.value >= AcquisitionProbability)) return;
            IsChasing = true;
            chaseRemaining = Random.Range(ChaseMinSeconds, Mathf.Max(ChaseMinSeconds, ChaseMaxSeconds));
            unseen = outside = 0f;
            lastSeen = Game.PlayerCell;
        }
        else
        {
            unseen = visible ? 0f : unseen + delta;
            outside = Distance(Cell, Game.PlayerCell) > EscapeRange ? outside + delta : 0f;
            chaseRemaining -= delta;
            if ((Chase == ChaseMode.Timed && chaseRemaining <= 0f)
                || (Chase == ChaseMode.Escape && (unseen >= MemorySeconds || outside >= EscapeSeconds))
                || (Chase == ChaseMode.Memory && unseen >= MemorySeconds))
            {
                IsChasing = false;
                cooldown = Chase == ChaseMode.Timed ? DetectionCooldown : 0f;
                patrolRemaining = NextPatrolLength();
                acquisitionChecked = false;
                return;
            }
            if (visible) lastSeen = Game.PlayerCell;
        }
    }

    private Vector2Int ChooseDirection()
    {
        if (AvoidBombLanes)
        {
            foreach (Vector2Int direction in BombermanPrototype.Directions)
                if (Game.CanEnemyTraverse(this, Cell + direction) && !Game.IsCellThreatenedByBomb(Cell + direction))
                {
                    selectingSafeMoves = true;
                    break;
                }
            if (!selectingSafeMoves && !Game.IsCellThreatenedByBomb(Cell)) return Vector2Int.zero;
        }
        try { return IsChasing ? ChooseChaseDirection() : ChoosePatrolDirection(); }
        finally { selectingSafeMoves = false; }
    }

    public bool CanSeePlayer()
    {
        if (Game == null || !Game.HasLivingPlayer) return false;
        Vector2Int target = Game.PlayerCell;
        if (Distance(Cell, target) > DetectionRange || (Cell.x != target.x && Cell.y != target.y)) return false;
        Vector2Int direction = new(System.Math.Sign(target.x - Cell.x), System.Math.Sign(target.y - Cell.y));
        for (Vector2Int cell = Cell + direction; cell != target; cell += direction)
            if (!Game.CanEnemyTraverse(this, cell)) return false;
        return Cell == target || Game.CanEnemyTraverse(this, target);
    }

    private Vector2Int ChoosePatrolDirection()
    {
        if (patrolRemaining <= 0)
        {
            patrolRemaining = NextPatrolLength();
            if (Random.value < AxisChangeChance)
            {
                Vector2Int side = new(-heading.y, heading.x);
                if (Random.value < 0.5f) side = -side;
                if (CanMove(side)) return side;
                if (CanMove(-side)) return -side;
            }
        }
        if (CanMove(heading)) return heading;
        if (CanMove(-heading)) return -heading;
        return AnyValidDirection();
    }

    private Vector2Int ChooseChaseDirection()
    {
        Vector2Int best = Vector2Int.zero;
        int bestDistance = Distance(Cell, lastSeen);
        // Continuing forward wins ties between reducing directions.
        if (CanMove(heading) && Distance(Cell + heading, lastSeen) < bestDistance)
        {
            best = heading;
            bestDistance = Distance(Cell + heading, lastSeen);
        }
        foreach (Vector2Int direction in BombermanPrototype.Directions)
        {
            int distance = Distance(Cell + direction, lastSeen);
            if (CanMove(direction) && (distance < bestDistance
                || (best != Vector2Int.zero && distance == bestDistance && best == -heading && direction != -heading)))
            {
                best = direction;
                bestDistance = distance;
            }
        }
        return best != Vector2Int.zero ? best : AnyValidDirection();
    }

    private Vector2Int AnyValidDirection()
    {
        if (CanMove(heading)) return heading;
        int start = Random.Range(0, 4);
        for (int i = 0; i < 4; i++)
        {
            Vector2Int direction = BombermanPrototype.Directions[(start + i) % 4];
            if (direction != -heading && CanMove(direction)) return direction;
        }
        return CanMove(-heading) ? -heading : Vector2Int.zero;
    }

    private bool CanMove(Vector2Int direction) => direction != Vector2Int.zero
        && Game.CanEnemyTraverse(this, Cell + direction)
        && (!selectingSafeMoves || !Game.IsCellThreatenedByBomb(Cell + direction));
    private int NextPatrolLength() => Random.Range(Mathf.Max(1, PatrolMinCells), Mathf.Max(1, Mathf.Max(PatrolMinCells, PatrolMaxCells)) + 1);
    private static int Distance(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    private void SetAnimation()
    {
        if (ModelAnimator == null) return;
        if (hasSpeed) ModelAnimator.SetFloat(SpeedId, IsMoving ? MoveSpeed : 0f);
        if (hasMoving) ModelAnimator.SetBool(MovingId, IsMoving && !IsDead);
    }
    public override void Die()
    {
        if (IsDead) return;
        IsDead = true;
        IsMoving = false;
        IsChasing = false;
        foreach (Collider collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
        SetAnimation();
        if (ModelAnimator != null && hasDie) ModelAnimator.SetTrigger(DieId);
        deathRemaining = DeathSeconds;
    }
}
