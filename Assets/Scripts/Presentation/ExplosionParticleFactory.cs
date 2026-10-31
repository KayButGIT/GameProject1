using UnityEngine;

public static class ExplosionParticleFactory
{
    private static Material orangeMaterial;
    private static Material yellowMaterial;
    private static Material redMaterial;
    private static Material whiteMaterial;

    public static GameObject CreatePlayerDeathBurst(string objectName, Vector3 groundPosition, bool fromBomb)
    {
        GameObject root = new(objectName);
        root.transform.position = groundPosition + new Vector3(0f, 0.45f, 0f);

        Material primary = fromBomb ? GetOrangeMaterial() : GetRedMaterial();
        Material secondary = fromBomb ? GetYellowMaterial() : GetWhiteMaterial();
        CreateChunks(root.transform, primary, secondary);
        return root;
    }

    public static GameObject CreateColorBurst(string objectName, Vector3 groundPosition, Color primaryColor, Color secondaryColor)
    {
        GameObject root = new(objectName);
        root.transform.position = groundPosition + new Vector3(0f, 0.45f, 0f);

        CreateChunks(
            root.transform,
            MakeParticleMaterial($"{objectName} Primary", primaryColor),
            MakeParticleMaterial($"{objectName} Secondary", secondaryColor));
        return root;
    }

    private static void CreateChunks(Transform parent, Material primaryMaterial, Material secondaryMaterial)
    {
        for (int i = 0; i < 34; i++)
        {
            GameObject chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chunk.name = "Explosion Particle";
            chunk.transform.SetParent(parent);
            chunk.transform.localPosition = new Vector3(
                Random.Range(-0.42f, 0.42f),
                Random.Range(0.02f, 0.3f),
                Random.Range(-0.42f, 0.42f));
            chunk.transform.localRotation = Random.rotation;

            float size = Random.Range(0.12f, 0.28f);
            chunk.transform.localScale = Vector3.one * size;
            chunk.GetComponent<Renderer>().material = Random.value > 0.35f ? primaryMaterial : secondaryMaterial;
            Object.Destroy(chunk.GetComponent<Collider>());

            Vector3 velocity = new(
                Random.Range(-1.25f, 1.25f),
                Random.Range(0.6f, 1.9f),
                Random.Range(-1.25f, 1.25f));
            Vector3 spin = new(
                Random.Range(-360f, 360f),
                Random.Range(-360f, 360f),
                Random.Range(-360f, 360f));
            float lifetime = Random.Range(0.38f, 0.72f);
            chunk.AddComponent<ExplosionParticlePiece>().Initialize(velocity, spin, lifetime);
        }
    }

    private static Material GetOrangeMaterial()
    {
        if (orangeMaterial == null)
        {
            orangeMaterial = BombermanMaterials.Make("Explosion Particle Orange", new Color(1f, 0.42f, 0.03f));
        }

        return orangeMaterial;
    }

    private static Material GetYellowMaterial()
    {
        if (yellowMaterial == null)
        {
            yellowMaterial = BombermanMaterials.Make("Explosion Particle Yellow", new Color(1f, 0.86f, 0.06f));
        }

        return yellowMaterial;
    }

    private static Material GetRedMaterial()
    {
        if (redMaterial == null)
        {
            redMaterial = BombermanMaterials.Make("Player Death Particle Red", new Color(0.95f, 0.06f, 0.08f));
        }

        return redMaterial;
    }

    private static Material GetWhiteMaterial()
    {
        if (whiteMaterial == null)
        {
            whiteMaterial = BombermanMaterials.Make("Player Death Particle White", new Color(0.95f, 0.95f, 1f));
        }

        return whiteMaterial;
    }

    private static Material MakeParticleMaterial(string materialName, Color color)
    {
        color.a = 1f;
        return BombermanMaterials.Make(materialName, color);
    }
}

public sealed class ExplosionParticlePiece : MonoBehaviour
{
    private Vector3 velocity;
    private Vector3 spin;
    private float lifetime;
    private float age;
    private Vector3 startScale;
    private float startDelay;
    private Mesh ownedMesh;
    private bool shrinkAtEnd;
    private bool collideWithWorld;
    private float collisionRadius;
    private bool settlesOnGround;
    private bool hasLanded;
    private int bounceCount;
    private float groundHeight;
    private float landedAt;
    private float gravityMultiplier = -1f;

    public void Initialize(Vector3 initialVelocity, Vector3 angularVelocity, float maxLifetime)
    {
        Initialize(initialVelocity, angularVelocity, maxLifetime, 0f, null);
    }

    public void Initialize(Vector3 initialVelocity, Vector3 angularVelocity, float maxLifetime, float delay, Mesh mesh, bool shrinkAtEnd = false, bool collideWithWorld = false, float customGravityMultiplier = -1f)
    {
        velocity = initialVelocity;
        spin = angularVelocity;
        lifetime = maxLifetime;
        startScale = transform.localScale;
        startDelay = delay;
        ownedMesh = mesh;
        this.shrinkAtEnd = shrinkAtEnd;
        this.collideWithWorld = collideWithWorld;
        gravityMultiplier = customGravityMultiplier;
        Renderer sourceRenderer = GetComponent<Renderer>();
        collisionRadius = sourceRenderer != null ? Mathf.Max(0.04f, sourceRenderer.bounds.extents.magnitude * 0.45f) : 0.08f;
    }

    public void InitializeSettling(Vector3 initialVelocity, Vector3 angularVelocity, float floorHeight, float persistAfterLanding)
    {
        velocity = initialVelocity;
        spin = angularVelocity;
        groundHeight = floorHeight;
        lifetime = persistAfterLanding;
        startScale = transform.localScale;
        settlesOnGround = true;
    }

    private void Update()
    {
        if (settlesOnGround)
        {
            UpdateSettlingPiece();
            return;
        }

        age += Time.deltaTime;
        if (age < startDelay) return;
        float effectAge = age - startDelay;
        if (effectAge >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        float gravity = gravityMultiplier >= 0f
            ? gravityMultiplier
            : ownedMesh != null ? 2.5f : 0.35f;
        velocity += Physics.gravity * gravity * Time.deltaTime;
        Vector3 displacement = velocity * Time.deltaTime;
        if (collideWithWorld && displacement.sqrMagnitude > 0.000001f
            && Physics.SphereCast(transform.position, collisionRadius, displacement.normalized, out RaycastHit hit,
                displacement.magnitude + collisionRadius * 0.1f, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            transform.position = hit.point + hit.normal * (collisionRadius + 0.002f);
            velocity = Vector3.Reflect(velocity, hit.normal) * 0.58f;
            spin *= 0.72f;
            if (velocity.sqrMagnitude < 0.36f && hit.normal.y > 0.65f)
                velocity = Vector3.zero;
        }
        else if (collideWithWorld)
        {
            transform.position += displacement;
        }
        else
        {
            transform.localPosition += displacement;
        }
        transform.Rotate(spin * Time.deltaTime, Space.Self);
        float scaleProgress = effectAge / lifetime;
        if (shrinkAtEnd)
            scaleProgress = Mathf.InverseLerp(0.9f, 1f, scaleProgress);
        transform.localScale = startScale * (1f - scaleProgress);
    }

    private void UpdateSettlingPiece()
    {
        age += Time.deltaTime;
        if (!hasLanded)
        {
            velocity += Physics.gravity * 1.7f * Time.deltaTime;
            transform.position += velocity * Time.deltaTime;
            transform.Rotate(spin * Time.deltaTime, Space.Self);
            if (transform.position.y <= groundHeight)
            {
                Vector3 position = transform.position;
                position.y = groundHeight;
                transform.position = position;
                if (bounceCount == 0 && Mathf.Abs(velocity.y) > 1.15f)
                {
                    velocity.y = Mathf.Abs(velocity.y) * 0.28f;
                    velocity.x *= 0.52f;
                    velocity.z *= 0.52f;
                    spin *= 0.55f;
                    bounceCount++;
                }
                else
                {
                    hasLanded = true;
                    landedAt = age;
                    velocity = Vector3.zero;
                    spin = Vector3.zero;
                }
            }
            return;
        }

        float groundedAge = age - landedAt;
        if (groundedAge >= lifetime)
        {
            Destroy(gameObject);
            return;
        }
        if (groundedAge > lifetime - 0.8f)
            transform.localScale = startScale * Mathf.Clamp01((lifetime - groundedAge) / 0.8f);
    }

    private void OnDestroy()
    {
        if (ownedMesh != null) Destroy(ownedMesh);
    }
}
