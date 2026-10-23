using System;
using UnityEngine;
using UnityEngine.Rendering;

// Particle effects for bomb blasts: fire, smoke, and sparks per blast cell, and debris from destroyed blocks.
// Textures and materials are generated once, so no effect assets are required.
public static class BlastEffects
{
    // Delay per cell from the bomb, so flames run outward along the blast.
    public const float SpreadDelay = 0.04f;
    // Longest time an effect needs before it can be removed, excluding the spread delay.
    public const float Duration = 3f;
    public const float BurnAftermathDuration = 1.05f;
    public const float SettledDebrisDuration = 8f;

    private static Texture2D glowTexture;
    private static Texture2D puffTexture;
    private static Material fireMaterial;
    private static Material glowMaterial;
    private static Material sparkMaterial;
    private static Material smokeMaterial;
    private static Material dustMaterial;

    public static GameObject CreateFire(string objectName, Vector3 groundPosition, int step, Vector4 bounds, Vector3 origin, bool showShockwave)
    {
        GameObject root = new(objectName);
        root.transform.position = groundPosition;
        bool center = step == 0;
        float delay = step * SpreadDelay;
        if (center && showShockwave) CreateShockwave(root.transform, delay);

        ParticleSystem fireball = CreateSystem(root.transform, "Fireball", new Vector3(0f, 0.35f, 0f), FireMaterial, delay);
        ParticleSystem.MainModule main = fireball.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
        main.startSize = center ? new ParticleSystem.MinMaxCurve(1.1f, 1.5f) : new ParticleSystem.MinMaxCurve(0.9f, 1.2f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = -0.15f;
        SetBurst(fireball, center ? 12 : 8);
        SetHemisphere(fireball, 0.25f);
        SetColor(fireball,
            new[] { Key(1f, 0.95f, 0.8f, 0f), Key(1f, 0.62f, 0.18f, 0.25f), Key(0.85f, 0.22f, 0.05f, 0.6f), Key(0.35f, 0.06f, 0.02f, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.3f), new GradientAlphaKey(0.55f, 0.65f), new GradientAlphaKey(0f, 1f) });
        SetSize(fireball, new Keyframe(0f, 0.7f), new Keyframe(0.3f, 1.15f), new Keyframe(1f, 1.3f));
        SetSpin(fireball, 1.5f);

        ParticleSystem smoke = CreateSystem(root.transform, "Smoke", new Vector3(0f, 0.5f, 0f), SmokeMaterial, delay + 0.12f);
        main = smoke.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = -0.08f;
        SetBurst(smoke, center ? 5 : 3);
        SetHemisphere(smoke, 0.3f);
        SetColor(smoke, new[] { Key(1f, 1f, 1f, 0f), Key(1f, 1f, 1f, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.15f), new GradientAlphaKey(0.35f, 0.5f), new GradientAlphaKey(0f, 1f) });
        SetSize(smoke, new Keyframe(0f, 0.6f), new Keyframe(1f, 1.6f));
        SetSpin(smoke, 0.7f);

        if (center)
        {
            ParticleSystem sparks = CreateSystem(root.transform, "Sparks", new Vector3(0f, 0.3f, 0f), SparkMaterial, delay);
            main = sparks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            main.gravityModifier = 1.2f;
            SetBurst(sparks, 24);
            SetHemisphere(sparks, 0.15f);
            SetColor(sparks, new[] { Key(1f, 0.9f, 0.6f, 0f), Key(1f, 0.45f, 0.1f, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            SetSize(sparks, new Keyframe(0f, 1f), new Keyframe(1f, 0.3f));
            ParticleSystemRenderer sparkRenderer = sparks.GetComponent<ParticleSystemRenderer>();
            sparkRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            sparkRenderer.velocityScale = 0.08f;
            sparkRenderer.lengthScale = 2f;
        }

        Shader clippedShader = Resources.Load<Shader>("BlastClipped");
        if (clippedShader != null)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material source = renderer.sharedMaterial;
                Material clipped = new(clippedShader);
                clipped.SetTexture("_BaseMap", source.mainTexture);
                clipped.SetColor("_BaseColor", source.color);
                clipped.SetFloat("_DstBlend", source.GetFloat("_DstBlend"));
                clipped.SetVector("_BlastBounds", bounds);
                clipped.SetVector("_BlastOrigin", new Vector4(origin.x, origin.z, 0f, 0f));
                renderer.sharedMaterial = clipped;
            }
            root.AddComponent<BlastMaterialCleanup>();
        }
        Activate(root.transform);
        return root;
    }

    public static GameObject CreatePlayerBurnAftermath(string objectName, Vector3 groundPosition)
    {
        GameObject root = new(objectName);
        root.transform.position = groundPosition;
        ParticleSystem smoke = CreateSystem(root.transform, "Burn Smoke", new Vector3(0f, 0.45f, 0f), SmokeMaterial, 0f);
        ParticleSystem.MainModule main = smoke.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, 1.05f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.75f);
        main.gravityModifier = -0.04f;
        SetBurst(smoke, 8);
        SetHemisphere(smoke, 0.2f);
        SetColor(smoke, new[] { Key(1f, 1f, 1f, 0f), Key(0.8f, 0.7f, 0.55f, 1f) },
            new[] { new GradientAlphaKey(0.2f, 0f), new GradientAlphaKey(0.45f, 0.2f), new GradientAlphaKey(0f, 1f) });
        SetSize(smoke, new Keyframe(0f, 0.5f), new Keyframe(1f, 1.5f));
        SetSpin(smoke, 0.9f);

        ParticleSystem embers = CreateSystem(root.transform, "Burn Embers", new Vector3(0f, 0.35f, 0f), SparkMaterial, 0f);
        main = embers.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.65f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
        main.gravityModifier = 0.15f;
        SetBurst(embers, 18);
        SetHemisphere(embers, 0.18f);
        SetColor(embers, new[] { Key(1f, 0.95f, 0.55f, 0f), Key(1f, 0.3f, 0.04f, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.6f), new GradientAlphaKey(0f, 1f) });
        SetSize(embers, new Keyframe(0f, 1f), new Keyframe(1f, 0.2f));
        Activate(root.transform);
        return root;
    }

    public static GameObject CreateDebris(string objectName, Vector3 groundPosition, Material blockMaterial, int step, GameObject source = null)
    {
        GameObject root = new(objectName);
        root.transform.position = groundPosition;
        float delay = step * SpreadDelay;

        // Chunks bounce on a plane at floor height.
        Transform floor = new GameObject("Debris Floor").transform;
        floor.SetParent(root.transform, false);

        ParticleSystem chunks = CreateSystem(root.transform, "Chunks", new Vector3(0f, 0.4f, 0f), blockMaterial, delay);
        ParticleSystem.MainModule main = chunks.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6.2f);
        ConfigureBrickChunkSize(main);
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 1.35f;
        main.maxParticles = 80;
        SetBurst(chunks, 20);
        SetHemisphere(chunks, 0.12f);
        SetSize(chunks, new Keyframe(0f, 1.35f), new Keyframe(0.6f, 1.15f), new Keyframe(1.3f, 0f));
        ParticleSystem.RotationOverLifetimeModule rotation = chunks.rotationOverLifetime;
        rotation.enabled = true;
        rotation.separateAxes = true;
        rotation.x = new ParticleSystem.MinMaxCurve(-8f, 8f);
        rotation.y = new ParticleSystem.MinMaxCurve(-8f, 8f);
        rotation.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
        ParticleSystem.CollisionModule collision = chunks.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.Planes;
        collision.SetPlane(0, floor);
        collision.bounce = 0.58f;
        collision.dampen = 0.16f;
        collision.radiusScale = 0.9f;
        ParticleSystemRenderer chunkRenderer = chunks.GetComponent<ParticleSystemRenderer>();
        chunkRenderer.renderMode = ParticleSystemRenderMode.Mesh;
        chunkRenderer.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

        bool emittedMeshFragments = false;
        int meshFragmentCount = 0;
        if (source != null)
        {
            chunks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (MeshFilter filter in source.GetComponentsInChildren<MeshFilter>())
            {
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.enabled || filter.sharedMesh == null) continue;
                Material[] sourceMaterials = renderer.sharedMaterials;
                for (int submesh = 0; submesh < filter.sharedMesh.subMeshCount; submesh++)
                {
                    Material material = sourceMaterials.Length > 0 ? sourceMaterials[Mathf.Min(submesh, sourceMaterials.Length - 1)] : blockMaterial;
                    int emitted = EmitMeshFragments(root.transform, filter.sharedMesh, filter.transform, material, submesh, delay, 24 - meshFragmentCount);
                    meshFragmentCount += emitted;
                    emittedMeshFragments |= emitted > 0;
                }
            }
            UnityEngine.Object.Destroy(source);
        }
        if (!emittedMeshFragments)
        {
            chunks.Play();
        }
        else
        {
            int extraChunks = Mathf.Max(4, 18 - meshFragmentCount);
            CreateChunkBurst("Chunk Accents", root.transform, blockMaterial, delay, extraChunks, 2f, 3.5f, 0.9f);
        }

        ParticleSystem dust = CreateSystem(root.transform, "Dust", new Vector3(0f, 0.35f, 0f), DustMaterial, delay);
        main = dust.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        SetBurst(dust, 8);
        SetHemisphere(dust, 0.2f);
        SetColor(dust, new[] { Key(1f, 1f, 1f, 0f), Key(1f, 1f, 1f, 1f) },
            new[] { new GradientAlphaKey(0.08f, 0f), new GradientAlphaKey(0.82f, 0.12f), new GradientAlphaKey(0.5f, 0.6f), new GradientAlphaKey(0f, 1f) });
        SetSize(dust, new Keyframe(0f, 0.7f), new Keyframe(1f, 1.1f));
        SetSpin(dust, 1.1f);

        ParticleSystem sparks = CreateSystem(root.transform, "Debris Sparks", new Vector3(0f, 0.42f, 0f), SparkMaterial, delay);
        main = sparks.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.09f);
        main.gravityModifier = 1.1f;
        SetBurst(sparks, 18);
        SetHemisphere(sparks, 0.12f);
        SetColor(sparks, new[] { Key(1f, 0.98f, 0.68f, 0f), Key(1f, 0.38f, 0.08f, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.45f), new GradientAlphaKey(0f, 1f) });
        SetSize(sparks, new Keyframe(0f, 1f), new Keyframe(1f, 0.25f));

        Activate(root.transform);
        return root;
    }

    public static GameObject CreateBlastWallFragments(string objectName, Vector3 groundPosition, Vector3 blastOrigin, Material material, int blastStep)
    {
        GameObject root = new(objectName);
        root.transform.position = Vector3.zero;
        if (material == null) return root;

        Vector3 blastDirection = groundPosition - blastOrigin;
        blastDirection.y = 0f;
        if (blastDirection.sqrMagnitude < 0.001f) blastDirection = Vector3.forward;
        blastDirection.Normalize();
        Vector3 sideways = new(-blastDirection.z, 0f, blastDirection.x);
        int count = UnityEngine.Random.Range(8 + blastStep, 12 + blastStep * 2);
        for (int i = 0; i < count; i++)
        {
            GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.name = "Blast Thrown Brick Fragment";
            shard.transform.SetParent(root.transform, false);
            float faceSpread = ((i % 7) - 3) * 0.08f + UnityEngine.Random.Range(-0.025f, 0.025f);
            Vector3 launchPosition = groundPosition + Vector3.up * UnityEngine.Random.Range(0.2f, 0.62f)
                + sideways * faceSpread;
            shard.transform.position = launchPosition;
            shard.transform.localRotation = Quaternion.Euler(
                UnityEngine.Random.Range(-8f, 8f),
                UnityEngine.Random.Range(0f, 180f),
                UnityEngine.Random.Range(-8f, 8f));
            shard.transform.localScale = new Vector3(
                UnityEngine.Random.Range(0.12f, 0.26f),
                UnityEngine.Random.Range(0.035f, 0.075f),
                UnityEngine.Random.Range(0.08f, 0.2f));
            shard.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.Destroy(shard.GetComponent<Collider>());
            int lateralSlot = i % 7;
            float lateralSpread = (lateralSlot - 3) * 0.1f + UnityEngine.Random.Range(-0.04f, 0.04f);
            float forwardReach = UnityEngine.Random.Range(0.25f, 0.6f);
            Vector3 launchDirection = blastDirection * forwardReach
                + sideways * lateralSpread
                + Vector3.up * UnityEngine.Random.Range(0.35f, 0.75f);
            shard.AddComponent<ExplosionParticlePiece>().InitializeSettling(
                launchDirection.normalized * UnityEngine.Random.Range(1.8f, 3f),
                new Vector3(UnityEngine.Random.Range(-480f, 480f), UnityEngine.Random.Range(-480f, 480f), UnityEngine.Random.Range(-480f, 480f)),
                groundPosition.y + 0.035f,
                SettledDebrisDuration);
        }
        return root;
    }

    private static int EmitMeshFragments(Transform parent, Mesh mesh, Transform sourceTransform, Material material, int submesh, float delay, int maxFragments)
    {
        if (maxFragments <= 0 || material == null || !mesh.isReadable || mesh.vertexCount == 0) return 0;
        int[] triangles;
        Vector3[] vertices;
        Bounds bounds;
        try
        {
            triangles = mesh.GetTriangles(submesh);
            vertices = mesh.vertices;
            bounds = mesh.bounds;
        }
        catch (UnityException)
        {
            return 0;
        }
        if (triangles.Length < 3) return 0;
        float cellSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) / 5f;
        if (cellSize <= 0.0001f) return 0;

        System.Collections.Generic.Dictionary<Vector3Int, System.Collections.Generic.List<int>> cells = new();
        for (int i = 0; i + 2 < triangles.Length; i += 3)
        {
            int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
            Vector3 center = (vertices[a] + vertices[b] + vertices[c]) / 3f;
            Vector3Int key = new(
                Mathf.FloorToInt((center.x - bounds.min.x) / cellSize),
                Mathf.FloorToInt((center.y - bounds.min.y) / cellSize),
                Mathf.FloorToInt((center.z - bounds.min.z) / cellSize));
            if (!cells.TryGetValue(key, out var bucket)) cells[key] = bucket = new System.Collections.Generic.List<int>();
            bucket.Add(a); bucket.Add(b); bucket.Add(c);
        }

        Vector3[] fragmentVertices = vertices;
        int emitted = 0;
        foreach (var bucket in cells.Values)
        {
            if (emitted >= maxFragments) break;
            System.Collections.Generic.Dictionary<int, int> remap = new();
            System.Collections.Generic.List<Vector3> compactVertices = new();
            int[] compactTriangles = new int[bucket.Count];
            for (int i = 0; i < bucket.Count; i++)
            {
                int sourceIndex = bucket[i];
                if (!remap.TryGetValue(sourceIndex, out int compactIndex))
                {
                    compactIndex = compactVertices.Count;
                    remap.Add(sourceIndex, compactIndex);
                    compactVertices.Add(fragmentVertices[sourceIndex]);
                }
                compactTriangles[i] = compactIndex;
            }
            Mesh fragment = new() { name = "Wall Fragment", vertices = compactVertices.ToArray(), triangles = compactTriangles };
            fragment.RecalculateNormals();
            fragment.RecalculateBounds();
            GameObject piece = new("Wall Chunk");
            piece.transform.SetParent(parent, false);
            piece.transform.position = sourceTransform.TransformPoint(fragment.bounds.center);
            piece.transform.rotation = sourceTransform.rotation;
            piece.transform.localScale = sourceTransform.lossyScale;
            piece.AddComponent<MeshFilter>().sharedMesh = fragment;
            piece.AddComponent<MeshRenderer>().sharedMaterial = material;
            Vector3 direction = (piece.transform.position - parent.position).normalized
                + new Vector3(UnityEngine.Random.Range(-0.25f, 0.25f), UnityEngine.Random.Range(0.5f, 0.9f), UnityEngine.Random.Range(-0.25f, 0.25f));
            piece.AddComponent<ExplosionParticlePiece>().Initialize(direction.normalized * UnityEngine.Random.Range(2f, 4f),
                new Vector3(UnityEngine.Random.Range(-540f, 540f), UnityEngine.Random.Range(-540f, 540f), UnityEngine.Random.Range(-540f, 540f)),
                UnityEngine.Random.Range(1.1f, 2.2f), delay, fragment);
            emitted++;
        }
        return emitted;
    }

    private static GameObject CreateChunkBurst(string name, Transform parent, Material material, float delay, int count, float minSpeed, float maxSpeed, float gravity)
    {
        if (material == null || count <= 0) return null;
        ParticleSystem chunks = CreateSystem(parent, name, new Vector3(0f, 0.4f, 0f), material, delay);
        ParticleSystem.MainModule main = chunks.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
        ConfigureBrickChunkSize(main);
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = gravity;
        SetBurst(chunks, count);
        SetHemisphere(chunks, 0.38f);
        ParticleSystem.CollisionModule collision = chunks.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.Planes;
        Transform floor = parent.Find("Debris Floor");
        if (floor != null) collision.SetPlane(0, floor);
        collision.bounce = 0.5f;
        collision.dampen = 0.25f;
        collision.radiusScale = 0.7f;
        ParticleSystemRenderer renderer = chunks.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        return chunks.gameObject;
    }

    private static void ConfigureBrickChunkSize(ParticleSystem.MainModule main)
    {
        main.startSize3D = true;
        main.startSizeX = new ParticleSystem.MinMaxCurve(0.1f, 0.26f);
        main.startSizeY = new ParticleSystem.MinMaxCurve(0.045f, 0.12f);
        main.startSizeZ = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
    }

    private static void CreateShockwave(Transform parent, float delay)
    {
        GameObject ring = new("Wall Shockwave");
        ring.transform.SetParent(parent, false);
        ring.transform.localPosition = new Vector3(0f, 0.035f, 0f);
        MeshFilter filter = ring.AddComponent<MeshFilter>();
        filter.sharedMesh = ShockwaveMesh;
        MeshRenderer renderer = ring.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = ShockwaveMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingOrder = 100;
        ring.AddComponent<WallShockwave>().Initialize(delay);
    }

    private static Mesh shockwaveMesh;
    private static Material shockwaveMaterial;

    private static Mesh ShockwaveMesh
    {
        get
        {
            if (shockwaveMesh != null) return shockwaveMesh;
            const int segments = 48;
            const float inner = 0.82f;
            const float outer = 1f;
            Vector3[] vertices = new Vector3[segments * 2];
            int[] triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i * 2] = direction * inner;
                vertices[i * 2 + 1] = direction * outer;
                int next = (i + 1) % segments;
                int t = i * 6;
                triangles[t] = i * 2;
                triangles[t + 1] = i * 2 + 1;
                triangles[t + 2] = next * 2 + 1;
                triangles[t + 3] = i * 2;
                triangles[t + 4] = next * 2 + 1;
                triangles[t + 5] = next * 2;
            }
            shockwaveMesh = new Mesh { name = "Wall Shockwave Ring", vertices = vertices, triangles = triangles };
            shockwaveMesh.RecalculateNormals();
            return shockwaveMesh;
        }
    }

    private static Material ShockwaveMaterial
    {
        get
        {
            if (shockwaveMaterial != null) return shockwaveMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            shockwaveMaterial = new Material(shader != null ? shader : Shader.Find("Sprites/Default"))
            {
                name = "Wall Shockwave",
                color = new Color(2.5f, 0.7f, 0.12f, 0.9f),
                renderQueue = (int)RenderQueue.Transparent + 1
            };
            shockwaveMaterial.SetFloat("_Surface", 1f);
            shockwaveMaterial.SetFloat("_Blend", 2f);
            shockwaveMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            shockwaveMaterial.SetFloat("_DstBlend", (float)BlendMode.One);
            shockwaveMaterial.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            shockwaveMaterial.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            shockwaveMaterial.SetFloat("_ZWrite", 0f);
            shockwaveMaterial.SetFloat("_AlphaClip", 0f);
            shockwaveMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return shockwaveMaterial;
        }
    }

    // Built inactive, so every module is configured before the system starts playing.
    private static ParticleSystem CreateSystem(Transform parent, string systemName, Vector3 localPosition, Material material, float delay)
    {
        GameObject systemObject = new(systemName);
        systemObject.SetActive(false);
        systemObject.transform.SetParent(parent, false);
        systemObject.transform.localPosition = localPosition;
        ParticleSystem system = systemObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = system.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = true;
        main.startDelay = delay;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        ParticleSystemRenderer renderer = systemObject.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return system;
    }

    private static void Activate(Transform root)
    {
        foreach (Transform child in root)
        {
            child.gameObject.SetActive(true);
        }
    }

    private static void SetBurst(ParticleSystem system, int count)
    {
        ParticleSystem.MainModule main = system.main;
        main.maxParticles = count;
        system.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
    }

    private static void SetHemisphere(ParticleSystem system, float radius)
    {
        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = radius;
        // The hemisphere opens along local +Z; turn it to face up.
        shape.rotation = new Vector3(-90f, 0f, 0f);
    }

    private static void SetColor(ParticleSystem system, GradientColorKey[] colors, GradientAlphaKey[] alphas)
    {
        Gradient gradient = new();
        gradient.SetKeys(colors, alphas);
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
    }

    private static void SetSize(ParticleSystem system, params Keyframe[] keys)
    {
        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(keys));
    }

    private static void SetSpin(ParticleSystem system, float radiansPerSecond)
    {
        ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-radiansPerSecond, radiansPerSecond);
    }

    private static GradientColorKey Key(float r, float g, float b, float time)
    {
        return new GradientColorKey(new Color(r, g, b), time);
    }

    // HDR base colors let the camera's bloom make fire and sparks glow.
    private static Material FireMaterial => fireMaterial != null ? fireMaterial : fireMaterial = MakeParticleMaterial("Blast Fire", PuffTexture, new Color(2.2f, 1.5f, 0.9f), true);
    private static Material GlowMaterial => glowMaterial != null ? glowMaterial : glowMaterial = MakeParticleMaterial("Blast Glow", GlowTexture, new Color(3f, 2.2f, 1.4f), true);
    private static Material SparkMaterial => sparkMaterial != null ? sparkMaterial : sparkMaterial = MakeParticleMaterial("Blast Sparks", GlowTexture, new Color(3f, 1.8f, 0.6f), true);
    private static Material SmokeMaterial => smokeMaterial != null ? smokeMaterial : smokeMaterial = MakeParticleMaterial("Blast Smoke", PuffTexture, new Color(0.22f, 0.21f, 0.2f), false);
    private static Material DustMaterial => dustMaterial != null ? dustMaterial : dustMaterial = MakeParticleMaterial("Blast Dust", PuffTexture, new Color(0.42f, 0.36f, 0.3f), false);

    private static Texture2D GlowTexture => glowTexture != null ? glowTexture : glowTexture = MakeTexture("Blast Glow", 64, (u, v) =>
    {
        float falloff = Mathf.Clamp01(1f - new Vector2(u, v).magnitude);
        return falloff * falloff;
    });

    private static Texture2D PuffTexture => puffTexture != null ? puffTexture : puffTexture = MakeTexture("Blast Puff", 128, (u, v) =>
    {
        float falloff = Mathf.Clamp01(1f - new Vector2(u, v).magnitude);
        falloff = falloff * falloff * (3f - 2f * falloff);
        float noise = 0f;
        float amplitude = 0.5f;
        float frequency = 3f;
        for (int octave = 0; octave < 4; octave++)
        {
            noise += amplitude * Mathf.PerlinNoise(u * frequency + 11.3f + octave * 7.1f, v * frequency + 5.7f + octave * 3.3f);
            amplitude *= 0.5f;
            frequency *= 2f;
        }

        return falloff * (0.35f + 0.9f * noise);
    });

    private static Material MakeParticleMaterial(string materialName, Texture texture, Color color, bool additive)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        Material material = new(shader != null ? shader : Shader.Find("Sprites/Default"))
        {
            name = materialName,
            mainTexture = texture,
            color = color
        };
        // Same blend setup as the URP material inspector for Transparent Additive or Alpha surfaces.
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", additive ? 2f : 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent + (additive ? 1 : 0);
        return material;
    }

    private static Texture2D MakeTexture(string textureName, int size, Func<float, float, float> alpha)
    {
        Texture2D texture = new(size, size, TextureFormat.RGBA32, true)
        {
            name = textureName,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                byte value = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(u, v)) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, value);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(true, true);
        return texture;
    }
}

public sealed class WallShockwave : MonoBehaviour
{
    private const float Duration = 0.5f;
    private float delay;
    private float elapsed;
    private Material material;
    private Color startColor;

    public void Initialize(float startDelay)
    {
        delay = startDelay;
        material = GetComponent<MeshRenderer>().material;
        startColor = material.color;
        transform.localScale = Vector3.one * 0.35f;
    }

    private void Update()
    {
        if (delay > 0f)
        {
            delay -= Time.deltaTime;
            return;
        }
        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / Duration);
        float eased = 1f - Mathf.Pow(1f - progress, 3f);
        transform.localScale = Vector3.one * Mathf.Lerp(0.22f, 1.15f, eased);
        Color color = startColor;
        color.a *= 1f - progress;
        GetComponent<MeshRenderer>().sharedMaterial.color = color;
        if (progress >= 1f) Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}

public sealed class BlastMaterialCleanup : MonoBehaviour
{
    private void OnDestroy()
    {
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            if (renderer.sharedMaterial != null) Destroy(renderer.sharedMaterial);
    }
}
