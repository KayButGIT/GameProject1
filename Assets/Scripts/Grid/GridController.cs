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
    public virtual bool CanPassDestructibleWalls => false;
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
    private Renderer[] wallPassSilhouetteRenderers;
    private Material wallPassOutlineMaterial;
    private readonly System.Collections.Generic.List<GameObject> wallPassOutlineObjects = new();
    private readonly System.Collections.Generic.List<GameObject> wallPassGhostObjects = new();
    private readonly System.Collections.Generic.List<Material> wallPassGhostMaterials = new();
    private readonly System.Collections.Generic.List<SkinnedOutlinePair> wallPassSkinnedOutlines = new();
    private bool wallPassSilhouetteVisible;

    private sealed class SkinnedOutlinePair
    {
        public SkinnedMeshRenderer Source;
        public SkinnedMeshRenderer Outline;
        public SkinnedMeshRenderer Ghost;
    }
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
        // Wall Pass: a mover that can pass destructible walls also walks through breakable blocks.
        bool walkable = freeMoveMap.IsWalkable(cell)
            || (CanPassDestructibleWalls && freeMoveMap.GetCellKind(cell) == CellKind.Destructible);
        return !walkable
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

    protected virtual void LateUpdate()
    {
        UpdateWallPassSilhouette();
        RefreshOutlineShells();
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
        SetWallPassSilhouetteVisible(false);
        IsDead = true;
        IsMoving = false;
        freeMoveVelocity = Vector3.zero;
        actualFreeMoveVelocity = Vector3.zero;
        visualFreeMoveVelocity = Vector3.zero;
        Destroy(gameObject);
    }

    internal void BindMovementMap(BombermanMap map) => freeMoveMap = map;

    private void UpdateWallPassSilhouette()
    {
        bool shouldShow = !IsDead && CanPassDestructibleWalls && freeMoveMap != null;
        Vector2Int cell = shouldShow ? freeMoveMap.WorldToCell(transform.position) : Vector2Int.zero;
        shouldShow = shouldShow && freeMoveMap.GetCellKind(cell) == CellKind.Destructible;

        SetWallPassSilhouetteVisible(shouldShow);
    }

    private void SetWallPassSilhouetteVisible(bool visible)
    {
        if (wallPassSilhouetteVisible == visible) return;
        wallPassSilhouetteVisible = visible;
        if (!visible)
        {
            DestroyOutlineShells();
            return;
        }

        Shader shader = Resources.Load<Shader>("WallPassOutline");
        if (shader == null)
        {
            Debug.LogWarning("Wall Pass outline shader is missing from Resources/WallPassOutline.shader.", this);
            wallPassSilhouetteVisible = false;
            return;
        }

        wallPassSilhouetteRenderers = GetComponentsInChildren<Renderer>(true);
        CreateOutlineShells();
    }

    private void CreateOutlineShells()
    {
        Shader shader = Resources.Load<Shader>("WallPassOutline");
        if (shader == null) return;
        wallPassOutlineMaterial = new Material(shader) { name = "Wall Pass Colored Outline (Runtime)" };
        wallPassOutlineMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 30;

        foreach (Renderer source in wallPassSilhouetteRenderers)
        {
            if (source == null || !source.enabled || IsAuxiliaryRenderer(source)) continue;
            if (source is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
            {
                GameObject outlineObject = CreateOutlineObject(source);
                SkinnedMeshRenderer outline = outlineObject.AddComponent<SkinnedMeshRenderer>();
                outline.sharedMesh = skinned.sharedMesh;
                outline.bones = skinned.bones;
                outline.rootBone = skinned.rootBone;
                outline.localBounds = skinned.localBounds;
                outline.updateWhenOffscreen = true;
                outline.quality = skinned.quality;
                outline.sharedMaterials = OutlineMaterials(skinned.sharedMesh.subMeshCount);
                outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                outline.receiveShadows = false;
                SkinnedMeshRenderer ghost = CreateGhostSkinnedRenderer(source, skinned);
                wallPassSkinnedOutlines.Add(new SkinnedOutlinePair { Source = skinned, Outline = outline, Ghost = ghost });
                continue;
            }

            MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null) continue;
            GameObject meshOutlineObject = CreateOutlineObject(source);
            meshOutlineObject.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
            MeshRenderer outlineRenderer = meshOutlineObject.AddComponent<MeshRenderer>();
            outlineRenderer.sharedMaterials = OutlineMaterials(sourceFilter.sharedMesh.subMeshCount);
            outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outlineRenderer.receiveShadows = false;
            outlineRenderer.sortingLayerID = source.sortingLayerID;
            outlineRenderer.sortingOrder = source.sortingOrder;
            CreateGhostMeshRenderer(source, sourceFilter.sharedMesh);
        }
    }

    private SkinnedMeshRenderer CreateGhostSkinnedRenderer(Renderer source, SkinnedMeshRenderer sourceSkinned)
    {
        GameObject ghostObject = CreateGhostObject(source);
        SkinnedMeshRenderer ghost = ghostObject.AddComponent<SkinnedMeshRenderer>();
        ghost.sharedMesh = sourceSkinned.sharedMesh;
        ghost.bones = sourceSkinned.bones;
        ghost.rootBone = sourceSkinned.rootBone;
        ghost.localBounds = sourceSkinned.localBounds;
        ghost.updateWhenOffscreen = true;
        ghost.quality = sourceSkinned.quality;
        ghost.sharedMaterials = CreateGhostMaterials(source.sharedMaterials);
        ghost.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ghost.receiveShadows = false;
        return ghost;
    }

    private void CreateGhostMeshRenderer(Renderer source, Mesh mesh)
    {
        GameObject ghostObject = CreateGhostObject(source);
        ghostObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer ghost = ghostObject.AddComponent<MeshRenderer>();
        ghost.sharedMaterials = CreateGhostMaterials(source.sharedMaterials);
        ghost.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ghost.receiveShadows = false;
        ghost.sortingLayerID = source.sortingLayerID;
        ghost.sortingOrder = source.sortingOrder;
    }

    private GameObject CreateGhostObject(Renderer source)
    {
        GameObject ghostObject = new(source.name + " Wall Pass Ghost");
        ghostObject.layer = source.gameObject.layer;
        ghostObject.transform.SetParent(source.transform.parent, false);
        ghostObject.transform.localPosition = source.transform.localPosition;
        ghostObject.transform.localRotation = source.transform.localRotation;
        ghostObject.transform.localScale = source.transform.localScale;
        wallPassGhostObjects.Add(ghostObject);
        return ghostObject;
    }

    private Material[] CreateGhostMaterials(Material[] sources)
    {
        Shader shader = Resources.Load<Shader>("WallPassSilhouette");
        if (shader == null) return System.Array.Empty<Material>();
        Material[] ghosts = new Material[sources.Length];
        for (int i = 0; i < sources.Length; i++)
        {
            Material mask = new(shader) { name = "Wall Pass Silhouette Mask (Runtime)" };
            mask.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 20;
            wallPassGhostMaterials.Add(mask);
            ghosts[i] = mask;
        }
        return ghosts;
    }

    private GameObject CreateOutlineObject(Renderer source)
    {
        GameObject outlineObject = new(source.name + " Wall Pass Outline");
        outlineObject.layer = source.gameObject.layer;
        outlineObject.transform.SetParent(source.transform.parent, false);
        outlineObject.transform.localPosition = source.transform.localPosition;
        outlineObject.transform.localRotation = source.transform.localRotation;
        outlineObject.transform.localScale = source.transform.localScale;
        wallPassOutlineObjects.Add(outlineObject);
        return outlineObject;
    }

    private Material[] OutlineMaterials(int submeshCount)
    {
        Material[] materials = new Material[Mathf.Max(1, submeshCount)];
        for (int i = 0; i < materials.Length; i++) materials[i] = wallPassOutlineMaterial;
        return materials;
    }

    private void DestroyOutlineShells()
    {
        foreach (GameObject outlineObject in wallPassOutlineObjects)
            if (outlineObject != null) Destroy(outlineObject);
        wallPassOutlineObjects.Clear();
        foreach (GameObject ghostObject in wallPassGhostObjects)
            if (ghostObject != null) Destroy(ghostObject);
        wallPassGhostObjects.Clear();
        wallPassSkinnedOutlines.Clear();
        foreach (Material ghostMaterial in wallPassGhostMaterials)
            if (ghostMaterial != null) Destroy(ghostMaterial);
        wallPassGhostMaterials.Clear();
        if (wallPassOutlineMaterial != null) Destroy(wallPassOutlineMaterial);
        wallPassOutlineMaterial = null;
    }

    private static bool IsAuxiliaryRenderer(Renderer renderer)
    {
        return renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer;
    }

    private void RefreshOutlineShells()
    {
        if (!wallPassSilhouetteVisible || wallPassSilhouetteRenderers == null) return;
        Color outlineColor = GetCurrentBlockContrastColor();
        if (wallPassOutlineMaterial != null)
        {
            wallPassOutlineMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
            wallPassOutlineMaterial.SetFloat("_OutlineWidth", 0.045f);
            const float glowIntensity = 2.2f;
            // Keep enough HDR energy for Bloom without washing the block-specific hue toward white.
            wallPassOutlineMaterial.SetColor("_OutlineColor", new Color(
                outlineColor.r * glowIntensity,
                outlineColor.g * glowIntensity,
                outlineColor.b * glowIntensity,
                outlineColor.a));
        }
        foreach (GameObject outlineObject in wallPassOutlineObjects)
            if (outlineObject != null)
                outlineObject.SetActive(true);
        foreach (GameObject ghostObject in wallPassGhostObjects)
            if (ghostObject != null)
                ghostObject.SetActive(true);

        foreach (SkinnedOutlinePair pair in wallPassSkinnedOutlines)
        {
            if (pair.Source == null || pair.Outline == null || pair.Source.sharedMesh == null) continue;
            int count = Mathf.Min(pair.Source.sharedMesh.blendShapeCount, pair.Outline.sharedMesh.blendShapeCount);
            for (int i = 0; i < count; i++)
            {
                pair.Outline.SetBlendShapeWeight(i, pair.Source.GetBlendShapeWeight(i));
                if (pair.Ghost != null && i < pair.Ghost.sharedMesh.blendShapeCount)
                    pair.Ghost.SetBlendShapeWeight(i, pair.Source.GetBlendShapeWeight(i));
            }
        }
    }

    private Color GetCurrentBlockContrastColor()
    {
        if (freeMoveMap == null) return new Color(0.08f, 0.92f, 1f, 1f);
        Vector2Int cell = freeMoveMap.WorldToCell(transform.position);
        Material blockMaterial = freeMoveMap.GetBlockMaterial(cell);
        Color blockColor = GetMaterialColor(blockMaterial);
        Color.RGBToHSV(blockColor, out float hue, out float saturation, out _);
        const float outlineValue = 1f;
        if (saturation < 0.25f)
        {
            string materialName = blockMaterial != null ? blockMaterial.name.ToLowerInvariant() : string.Empty;
            string textureName = blockMaterial != null && blockMaterial.HasProperty("_BaseMap")
                ? blockMaterial.GetTexture("_BaseMap")?.name.ToLowerInvariant() ?? string.Empty
                : string.Empty;
            if (materialName.Contains("s2destruct") || textureName.Contains("s2des"))
                return Color.HSVToRGB(0.53f, 0.96f, outlineValue);

            return Color.HSVToRGB(0.075f, 0.96f, outlineValue);
        }

        return Color.HSVToRGB(Mathf.Repeat(hue + 0.5f, 1f), Mathf.Max(0.9f, saturation), outlineValue);
    }

    protected virtual void OnDestroy()
    {
        DestroyOutlineShells();
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

                if (!TryGetMaterialColor(material, out Color color)) continue;
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

    private static Color GetMaterialColor(Material material)
    {
        return TryGetMaterialColor(material, out Color color) ? color : Color.white;
    }

    private static bool TryGetMaterialColor(Material material, out Color color)
    {
        if (material != null && material.HasProperty("_BaseColor"))
        {
            color = material.GetColor("_BaseColor");
            return true;
        }
        if (material != null && material.HasProperty("_Color"))
        {
            color = material.GetColor("_Color");
            return true;
        }
        color = Color.white;
        return false;
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
