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
    public const float Duration = 1.5f;

    private static Texture2D glowTexture;
    private static Texture2D puffTexture;
    private static Material fireMaterial;
    private static Material glowMaterial;
    private static Material sparkMaterial;
    private static Material smokeMaterial;
    private static Material dustMaterial;

    public static GameObject CreateFire(string objectName, Vector3 groundPosition, int step)
    {
        GameObject root = new(objectName);
        root.transform.position = groundPosition;
        bool center = step == 0;
        float delay = step * SpreadDelay;

        ParticleSystem fireball = CreateSystem(root.transform, "Fireball", new Vector3(0f, 0.35f, 0f), FireMaterial, delay);
        ParticleSystem.MainModule main = fireball.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
        main.startSize = center ? new ParticleSystem.MinMaxCurve(0.9f, 1.3f) : new ParticleSystem.MinMaxCurve(0.7f, 1f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = -0.15f;
        SetBurst(fireball, center ? 10 : 6);
        SetHemisphere(fireball, 0.25f);
        SetColor(fireball,
            new[] { Key(1f, 0.95f, 0.8f, 0f), Key(1f, 0.62f, 0.18f, 0.25f), Key(0.85f, 0.22f, 0.05f, 0.6f), Key(0.35f, 0.06f, 0.02f, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.3f), new GradientAlphaKey(0.55f, 0.65f), new GradientAlphaKey(0f, 1f) });
        SetSize(fireball, new Keyframe(0f, 0.5f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1.15f));
        SetSpin(fireball, 1.5f);

        ParticleSystem glow = CreateSystem(root.transform, "Glow", new Vector3(0f, 0.4f, 0f), GlowMaterial, delay);
        main = glow.main;
        main.startLifetime = 0.18f;
        main.startSpeed = 0f;
        main.startSize = center ? 2.2f : 1.2f;
        SetBurst(glow, 1);
        SetColor(glow, new[] { Key(1f, 1f, 1f, 0f), Key(1f, 1f, 1f, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        SetSize(glow, new Keyframe(0f, 1f), new Keyframe(1f, 0.6f));

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

        Activate(root.transform);
        return root;
    }

    public static GameObject CreateDebris(string objectName, Vector3 groundPosition, Material blockMaterial, int step)
    {
        GameObject root = new(objectName);
        root.transform.position = groundPosition;
        float delay = step * SpreadDelay;

        // Chunks bounce on a plane at floor height.
        Transform floor = new GameObject("Debris Floor").transform;
        floor.SetParent(root.transform, false);

        ParticleSystem chunks = CreateSystem(root.transform, "Chunks", new Vector3(0f, 0.4f, 0f), blockMaterial, delay);
        ParticleSystem.MainModule main = chunks.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 2.5f;
        SetBurst(chunks, 12);
        SetHemisphere(chunks, 0.3f);
        SetSize(chunks, new Keyframe(0f, 1f), new Keyframe(0.7f, 1f), new Keyframe(1f, 0f));
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
        collision.bounce = 0.35f;
        collision.dampen = 0.4f;
        collision.radiusScale = 0.5f;
        ParticleSystemRenderer chunkRenderer = chunks.GetComponent<ParticleSystemRenderer>();
        chunkRenderer.renderMode = ParticleSystemRenderMode.Mesh;
        chunkRenderer.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

        ParticleSystem dust = CreateSystem(root.transform, "Dust", new Vector3(0f, 0.35f, 0f), DustMaterial, delay);
        main = dust.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        SetBurst(dust, 4);
        SetHemisphere(dust, 0.35f);
        SetColor(dust, new[] { Key(1f, 1f, 1f, 0f), Key(1f, 1f, 1f, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.5f, 0.2f), new GradientAlphaKey(0f, 1f) });
        SetSize(dust, new Keyframe(0f, 0.7f), new Keyframe(1f, 1.5f));
        SetSpin(dust, 0.6f);

        Activate(root.transform);
        return root;
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
