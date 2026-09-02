using UnityEngine;

public abstract class GridController : MonoBehaviour
{
    public Vector2Int Cell;
    public float MoveSpeed;
    public Renderer BodyRenderer;
    public Material DeadMaterial;
    public bool IsMoving { get; private set; }
    public bool IsDead { get; private set; }
    public virtual bool IsPlayer => false;

    private const float BodyIdleHeight = 0.55f;

    private Vector3 moveStart;
    private Vector3 moveTarget;
    private float moveProgress;

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

        GameObject face = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        face.name = usePlayerFaceNames ? "Face Normal Blink Wink Dead Dead Burnt" : "Face";
        face.transform.SetParent(actorObject.transform);
        face.transform.localPosition = new Vector3(0f, 1.13f * scale, 0.29f * scale);
        face.transform.localScale = new Vector3(0.28f * scale, 0.18f * scale, 0.08f * scale);
        face.GetComponent<Renderer>().material = BombermanMaterials.Make($"{actorName} Face", Color.white);

        TActor actor = actorObject.AddComponent<TActor>();
        actor.Cell = cell;
        actor.DeadMaterial = deadMaterial;
        actor.BodyRenderer = body.GetComponent<Renderer>();
        return actor;
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
        AnimateWalkBob();

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

    public void Die()
    {
        IsDead = true;
        IsMoving = false;
        Destroy(gameObject);
    }

    private void AnimateWalkBob()
    {
        Transform body = transform.Find("Body");
        if (body == null)
        {
            return;
        }

        float bob = Mathf.Sin(moveProgress * Mathf.PI * 4f) * 0.08f;
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
