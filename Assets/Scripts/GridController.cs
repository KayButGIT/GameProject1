using UnityEngine;

public abstract class GridController : MonoBehaviour
{
    public Vector2Int Cell;
    public float MoveSpeed;
    public Renderer BodyRenderer;
    public Material DeadMaterial;
    public float FreeMoveAcceleration = 28f;
    public float FreeMoveDeceleration = 36f;
    public float FreeMoveTurnSpeed = 14f;
    public bool IsMoving { get; protected set; }
    public bool IsDead { get; protected set; }
    public virtual bool IsPlayer => false;
    public float CurrentFreeMoveSpeed => freeMoveVelocity.magnitude;

    private const float BodyIdleHeight = 0.55f;

    private Rigidbody body;
    private Vector3 moveStart;
    private Vector3 moveTarget;
    private float moveProgress;
    private Vector3 freeMoveVelocity;
    private float freeMoveWalkTime;
    private float freeMoveRadius;

    protected static TActor CreateActor<TActor>(
        string actorName,
        Vector2Int cell,
        BombermanMap map,
        Material normalMaterial,
        Material deadMaterial,
        bool usePlayerFaceNames,
        float scale)
        where TActor : GridController
    {
        GameObject actorObject = new(actorName);
        actorObject.transform.position = map.CellToWorld(cell);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(actorObject.transform);
        body.transform.localPosition = new Vector3(0f, BodyIdleHeight * scale, 0f);
        body.transform.localScale = new Vector3(0.65f * scale, 0.8f * scale, 0.65f * scale);
        body.GetComponent<Renderer>().material = normalMaterial;
        Destroy(body.GetComponent<Collider>());

        GameObject face = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        face.name = usePlayerFaceNames ? "Face Normal Blink Wink Dead Dead Burnt" : "Face";
        face.transform.SetParent(actorObject.transform);
        face.transform.localPosition = new Vector3(0f, 1.13f * scale, 0.29f * scale);
        face.transform.localScale = new Vector3(0.28f * scale, 0.18f * scale, 0.08f * scale);
        face.GetComponent<Renderer>().material = BombermanMaterials.Make($"{actorName} Face", Color.white);
        Destroy(face.GetComponent<Collider>());

        TActor actor = actorObject.AddComponent<TActor>();
        actor.Cell = cell;
        actor.DeadMaterial = deadMaterial;
        actor.BodyRenderer = body.GetComponent<Renderer>();
        return actor;
    }

    protected void ConfigureFreeMovementPhysics(float radius, float height)
    {
        freeMoveRadius = radius;

        CapsuleCollider collider = gameObject.AddComponent<CapsuleCollider>();
        collider.center = new Vector3(0f, height * 0.5f, 0f);
        collider.radius = radius;
        collider.height = height;

        body = gameObject.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.constraints = RigidbodyConstraints.FreezePositionY
            | RigidbodyConstraints.FreezeRotationX
            | RigidbodyConstraints.FreezeRotationY
            | RigidbodyConstraints.FreezeRotationZ;
    }

    protected void MoveFreely(Vector2 input, BombermanMap map, System.Func<Vector2Int, bool> isCellBlocked = null)
    {
        if (body == null || IsDead)
        {
            return;
        }

        ClearPhysicsDrift();

        Vector3 direction = new(input.x, 0f, input.y);
        if (direction.sqrMagnitude > 1f)
        {
            direction.Normalize();
        }

        Vector3 targetVelocity = direction * MoveSpeed;
        float acceleration = direction.sqrMagnitude > 0.001f ? FreeMoveAcceleration : FreeMoveDeceleration;
        freeMoveVelocity = Vector3.MoveTowards(
            freeMoveVelocity,
            targetVelocity,
            acceleration * Time.fixedDeltaTime);

        Vector3 nextPosition = body.position;
        Vector3 xPosition = nextPosition + new Vector3(freeMoveVelocity.x * Time.fixedDeltaTime, 0f, 0f);
        if (IsFootprintWalkable(xPosition, map, isCellBlocked))
        {
            nextPosition = xPosition;
        }
        else
        {
            freeMoveVelocity.x = 0f;
        }

        Vector3 zPosition = nextPosition + new Vector3(0f, 0f, freeMoveVelocity.z * Time.fixedDeltaTime);
        if (IsFootprintWalkable(zPosition, map, isCellBlocked))
        {
            nextPosition = zPosition;
        }
        else
        {
            freeMoveVelocity.z = 0f;
        }

        body.MovePosition(nextPosition);
        ClearPhysicsDrift();

        if (freeMoveVelocity.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(freeMoveVelocity.normalized, Vector3.up);
            body.MoveRotation(Quaternion.Slerp(body.rotation, targetRotation, FreeMoveTurnSpeed * Time.fixedDeltaTime));
            freeMoveWalkTime += freeMoveVelocity.magnitude * Time.fixedDeltaTime;
            AnimateWalkBob(freeMoveWalkTime);
        }
        else
        {
            freeMoveWalkTime = 0f;
            ResetBodyHeight();
        }

        Cell = map.WorldToCell(nextPosition);
    }

    private void ClearPhysicsDrift()
    {
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }

    private bool IsFootprintWalkable(Vector3 position, BombermanMap map, System.Func<Vector2Int, bool> isCellBlocked)
    {
        Vector2Int centerCell = map.WorldToCell(position);
        Vector3 center = map.CellToWorld(centerCell);
        Vector2 offset = new(position.x - center.x, position.z - center.z);
        float edgeLimit = 0.5f - freeMoveRadius;

        if (IsMovementBlocked(centerCell, map, isCellBlocked))
        {
            return false;
        }

        if (offset.x > edgeLimit && IsMovementBlocked(centerCell + Vector2Int.right, map, isCellBlocked))
        {
            return false;
        }

        if (offset.x < -edgeLimit && IsMovementBlocked(centerCell + Vector2Int.left, map, isCellBlocked))
        {
            return false;
        }

        if (offset.y > edgeLimit && IsMovementBlocked(centerCell + Vector2Int.up, map, isCellBlocked))
        {
            return false;
        }

        if (offset.y < -edgeLimit && IsMovementBlocked(centerCell + Vector2Int.down, map, isCellBlocked))
        {
            return false;
        }

        if (offset.x > edgeLimit
            && offset.y > edgeLimit
            && IsMovementBlocked(centerCell + Vector2Int.right + Vector2Int.up, map, isCellBlocked))
        {
            return false;
        }

        if (offset.x > edgeLimit
            && offset.y < -edgeLimit
            && IsMovementBlocked(centerCell + Vector2Int.right + Vector2Int.down, map, isCellBlocked))
        {
            return false;
        }

        if (offset.x < -edgeLimit
            && offset.y > edgeLimit
            && IsMovementBlocked(centerCell + Vector2Int.left + Vector2Int.up, map, isCellBlocked))
        {
            return false;
        }

        if (offset.x < -edgeLimit
            && offset.y < -edgeLimit
            && IsMovementBlocked(centerCell + Vector2Int.left + Vector2Int.down, map, isCellBlocked))
        {
            return false;
        }

        return true;
    }

    private static bool IsMovementBlocked(Vector2Int cell, BombermanMap map, System.Func<Vector2Int, bool> isCellBlocked)
    {
        return !map.IsWalkable(cell)
            || (isCellBlocked != null && isCellBlocked(cell));
    }

    protected virtual void Update()
    {
        if (IsDead || !IsMoving)
        {
            return;
        }

        moveProgress += Time.deltaTime * MoveSpeed;
        transform.position = Vector3.Lerp(moveStart, moveTarget, moveProgress);
        transform.rotation = Quaternion.LookRotation((moveTarget - moveStart).normalized, Vector3.up);
        AnimateWalkBob(moveProgress);

        if (moveProgress >= 1f)
        {
            transform.position = moveTarget;
            IsMoving = false;
            ResetBodyHeight();
        }
    }

    public void StartMove(Vector2Int targetCell, Vector3 targetPosition)
    {
        Cell = targetCell;
        moveStart = transform.position;
        moveTarget = targetPosition;
        moveProgress = 0f;
        IsMoving = true;
    }

    public virtual void Die()
    {
        IsDead = true;
        IsMoving = false;
        freeMoveVelocity = Vector3.zero;
        Destroy(gameObject);
    }

    private void AnimateWalkBob(float animationTime)
    {
        Transform body = transform.Find("Body");
        if (body == null)
        {
            return;
        }

        float bob = Mathf.Sin(animationTime * Mathf.PI * 4f) * 0.08f;
        body.localPosition = new Vector3(0f, BodyIdleHeight + bob, 0f);
    }

    private void ResetBodyHeight()
    {
        Transform body = transform.Find("Body");
        if (body != null)
        {
            body.localPosition = new Vector3(0f, BodyIdleHeight, 0f);
        }
    }
}
