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
    public float CurrentFreeMoveSpeed => visualFreeMoveVelocity.magnitude;

    private const float BodyIdleHeight = 0.55f;

    private Rigidbody body;
    private CapsuleCollider movementCollider;
    private Vector3 moveStart;
    private Vector3 moveTarget;
    private float moveProgress;
    private Vector3 freeMoveVelocity;
    private Vector3 actualFreeMoveVelocity;
    private Vector3 visualFreeMoveVelocity;
    private float freeMoveWalkTime;
    private BombermanMap freeMoveMap;
    private System.Func<Vector2Int, bool> extraMovementBlocker;
    private System.Func<Vector2Int, bool> movementBlocker;
    private const float BlockedMoveEpsilon = 0.00001f;
    private const float VisualVelocitySharpness = 16f;
    private const float VisualMoveStopSpeed = 0.08f;

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
        radius = Mathf.Max(0.05f, radius);
        height = Mathf.Max(radius * 2f, height);

        movementCollider = gameObject.AddComponent<CapsuleCollider>();
        movementCollider.center = new Vector3(0f, height * 0.5f, 0f);
        movementCollider.radius = radius;
        movementCollider.height = height;
        movementCollider.isTrigger = false;

        body = gameObject.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
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

        Vector3 currentPosition = body.position;
        Vector3 desiredDelta = freeMoveVelocity * Time.fixedDeltaTime;
        freeMoveMap = map;
        extraMovementBlocker = isCellBlocked;
        movementBlocker ??= IsMovementBlocked;
        Vector3 origin = map.CellToWorld(Vector2Int.zero);
        // Read the actual capsule each step so Inspector edits cannot create a second collision size.
        Vector3 scale = transform.lossyScale;
        float collisionRadius = movementCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        Vector3 centerOffset = body.rotation * Vector3.Scale(movementCollider.center, scale);
        Vector2 nextPlanarPosition = GridMovement.Move(
            new Vector2(currentPosition.x + centerOffset.x - origin.x, currentPosition.z + centerOffset.z - origin.z),
            new Vector2(desiredDelta.x, desiredDelta.z),
            collisionRadius,
            movementBlocker);
        Vector3 nextPosition = new(
            nextPlanarPosition.x + origin.x - centerOffset.x,
            currentPosition.y,
            nextPlanarPosition.y + origin.z - centerOffset.z);

        Vector3 actualDelta = nextPosition - currentPosition;
        if (actualDelta.sqrMagnitude < BlockedMoveEpsilon * BlockedMoveEpsilon)
        {
            nextPosition = currentPosition;
            actualDelta = Vector3.zero;
        }

        actualFreeMoveVelocity = Time.fixedDeltaTime > 0f ? actualDelta / Time.fixedDeltaTime : Vector3.zero;
        float visualBlend = 1f - Mathf.Exp(-VisualVelocitySharpness * Time.fixedDeltaTime);
        visualFreeMoveVelocity = Vector3.Lerp(visualFreeMoveVelocity, actualFreeMoveVelocity, visualBlend);
        if (actualFreeMoveVelocity.sqrMagnitude < VisualMoveStopSpeed * VisualMoveStopSpeed
            && visualFreeMoveVelocity.sqrMagnitude < VisualMoveStopSpeed * VisualMoveStopSpeed)
        {
            visualFreeMoveVelocity = Vector3.zero;
        }

        body.MovePosition(nextPosition);
        ClearPhysicsDrift();

        if (visualFreeMoveVelocity.sqrMagnitude > VisualMoveStopSpeed * VisualMoveStopSpeed)
        {
            Quaternion targetRotation = Quaternion.LookRotation(visualFreeMoveVelocity.normalized, Vector3.up);
            float turnAmount = 1f - Mathf.Exp(-FreeMoveTurnSpeed * Time.fixedDeltaTime);
            body.MoveRotation(Quaternion.Slerp(body.rotation, targetRotation, turnAmount));
            freeMoveWalkTime += visualFreeMoveVelocity.magnitude * Time.fixedDeltaTime;
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
        if (body.isKinematic)
        {
            return;
        }

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }

    private bool IsMovementBlocked(Vector2Int cell)
    {
        return !freeMoveMap.IsWalkable(cell)
            || (extraMovementBlocker != null && extraMovementBlocker(cell));
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
        actualFreeMoveVelocity = Vector3.zero;
        visualFreeMoveVelocity = Vector3.zero;
        Destroy(gameObject);
    }

    public void GetParticleColors(out Color primaryColor, out Color secondaryColor)
    {
        primaryColor = Color.white;
        secondaryColor = Color.white;
        bool foundPrimaryColor = false;

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            foreach (Material material in renderer.materials)
            {
                if (material == null)
                {
                    continue;
                }

                Color color = material.color;
                if (!foundPrimaryColor)
                {
                    primaryColor = color;
                    secondaryColor = color;
                    foundPrimaryColor = true;
                    continue;
                }

                if (color != primaryColor)
                {
                    secondaryColor = color;
                    return;
                }
            }
        }
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
