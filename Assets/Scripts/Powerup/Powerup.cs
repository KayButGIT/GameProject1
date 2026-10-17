using UnityEngine;

// A collectible revealed when the breakable block hiding it is destroyed.
// BombermanPrototype checks the pickup distance and applies the effect, so this component only draws the item.
// Give BombermanPrototype your own models in Power Up Prefabs; without one, a colored marker with a letter is used.
public sealed class PowerUp : MonoBehaviour
{
    private const float GlowPulseSeconds = 2.5f;
    private const float FramePadding = 0.015f;
    private const float FrameBeamWidth = 0.018f;
    private const float GlowMinimumIntensity = 0.25f;
    private const float GlowMaximumIntensity = 3f;
    private const float RestHeight = 0.45f;
    private const float BobHeight = 0.08f;
    private const float BobSpeed = 3f;
    private const float SpinDegreesPerSecond = 90f;
    private static readonly int GlowColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int GlowIntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

    // Your own models (Power Up Prefabs) lie on the floor. Switch these on if you want them to hover or spin instead.
    private const float ModelHeight = 0.02f;
    private static readonly bool ModelBobs = false;
    private static readonly bool ModelSpins = false;

    private static readonly Material[] MarkerMaterials = new Material[8];
    private GameObject outlineSource;
    private Material outlineMaterial;
    private Material frameMaterial;
    private float glowPulseTime;

    public PowerUpType Type;
    public Vector2Int Cell;

    private Transform visual;
    private Transform label;
    private Camera labelCamera;
    private float bobPhase;
    private bool usesModel;

    public static PowerUp Create(PowerUpType type, Vector2Int cell, Vector3 groundPosition, Transform parent, GameObject prefab)
    {
        GameObject root = new($"Power Up {type}");
        root.transform.SetParent(parent, true);
        root.transform.position = groundPosition;

        PowerUp item = root.AddComponent<PowerUp>();
        item.Type = type;
        item.Cell = cell;
        item.bobPhase = Random.value * Mathf.PI * 2f;
        item.BuildVisual(prefab);
        item.CreateGlow();
        return item;
    }

    private void Update()
    {
        if (visual == null) return;

        // Scaled time, so the item holds still while the game is paused.
        float height = usesModel ? ModelHeight : RestHeight;
        if (!usesModel || ModelBobs)
        {
            bobPhase += Time.deltaTime * BobSpeed;
            height += Mathf.Sin(bobPhase) * BobHeight;
        }

        visual.localPosition = new Vector3(0f, height, 0f);
        if (!usesModel || ModelSpins)
        {
            visual.Rotate(0f, SpinDegreesPerSecond * Time.deltaTime, 0f, Space.World);
        }

        AdvanceGlow(Time.deltaTime);

        if (label == null) return;
        if (labelCamera == null) labelCamera = Camera.main;
        if (labelCamera != null) label.rotation = labelCamera.transform.rotation;
    }

    private void BuildVisual(GameObject prefab)
    {
        usesModel = prefab != null;
        visual = new GameObject("Visual").transform;
        visual.SetParent(transform, false);
        visual.localPosition = new Vector3(0f, usesModel ? ModelHeight : RestHeight, 0f);

        if (prefab != null)
        {
            GameObject model = Instantiate(prefab, visual);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            foreach (Collider modelCollider in model.GetComponentsInChildren<Collider>(true)) modelCollider.enabled = false;
            outlineSource = model;
            return;
        }

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = "Marker";
        marker.transform.SetParent(visual, false);
        marker.transform.localScale = Vector3.one * 0.42f;
        marker.GetComponent<Renderer>().sharedMaterial = GetMarkerMaterial(Type);
        Destroy(marker.GetComponent<Collider>());
        outlineSource = marker;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject labelObject = new("Label");
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = new Vector3(0f, RestHeight + 0.5f, 0f);
        TextMesh text = labelObject.AddComponent<TextMesh>();
        text.text = LabelFor(Type);
        text.font = font;
        text.fontSize = 64;
        text.characterSize = 0.07f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = Color.white;
        MeshRenderer labelRenderer = labelObject.GetComponent<MeshRenderer>();
        labelRenderer.sharedMaterial = font.material;
        labelRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        labelRenderer.receiveShadows = false;
        label = labelObject.transform;
    }

    private void CreateGlow()
    {
        if (outlineSource == null || visual == null) return;

        Shader outlineShader = Resources.Load<Shader>("ExitOutline");
        if (outlineShader == null) outlineShader = Shader.Find("Bomberman/Exit Outline");
        Shader frameShader = Resources.Load<Shader>("ExitGlowFrame");
        if (frameShader == null) frameShader = Shader.Find("Bomberman/Exit Glow Frame");
        if (outlineShader == null || frameShader == null)
        {
            Debug.LogError("Power-up glow shaders are missing from Resources.", this);
            return;
        }

        Color glowColor = ColorFor(Type);
        outlineMaterial = new Material(outlineShader) { name = $"Power Up {Type} Outline" };
        outlineMaterial.SetColor(GlowColorId, glowColor);
        outlineMaterial.SetFloat(OutlineWidthId, 0.025f);
        outlineMaterial.SetFloat(GlowIntensityId, GlowMinimumIntensity);
        frameMaterial = new Material(frameShader) { name = $"Power Up {Type} Frame" };
        frameMaterial.SetColor(GlowColorId, glowColor);
        frameMaterial.SetFloat(GlowIntensityId, GlowMinimumIntensity);

        GameObject shell = Instantiate(outlineSource, visual.transform);
        shell.name = "Outline Shell";
        foreach (Collider collider in shell.GetComponentsInChildren<Collider>(true)) Destroy(collider);
        foreach (Rigidbody body in shell.GetComponentsInChildren<Rigidbody>(true)) Destroy(body);
        foreach (MonoBehaviour behaviour in shell.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(behaviour);
        foreach (Renderer renderer in shell.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < materials.Length; i++) materials[i] = outlineMaterial;
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        CreateGlowFrame();
    }

    private void CreateGlowFrame()
    {
        if (!VisualBounds.TryMeasure(visual, outlineSource, out Bounds bounds)) return;
        float frameHeight = Mathf.Max(bounds.size.y, Mathf.Max(bounds.size.x, bounds.size.z) * 1.15f);
        float frameBottom = bounds.min.y - FramePadding;
        bounds.min = new Vector3(bounds.min.x - FramePadding, frameBottom, bounds.min.z - FramePadding);
        bounds.max = new Vector3(bounds.max.x + FramePadding, frameBottom + frameHeight + FramePadding * 2f, bounds.max.z + FramePadding);

        Vector3 size = bounds.size;
        Vector3 center = bounds.center;
        float halfX = size.x * 0.5f;
        float halfY = size.y * 0.5f;
        float halfZ = size.z * 0.5f;
        float beam = FrameBeamWidth;
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
            AddFrameBeam(center + new Vector3(0f, halfY * y, halfZ * z), new Vector3(size.x + beam, beam, beam));
        for (int x = -1; x <= 1; x += 2)
        for (int z = -1; z <= 1; z += 2)
            AddFrameBeam(center + new Vector3(halfX * x, 0f, halfZ * z), new Vector3(beam, size.y + beam, beam));
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
            AddFrameBeam(center + new Vector3(halfX * x, halfY * y, 0f), new Vector3(beam, beam, size.z + beam));
    }

    private void AddFrameBeam(Vector3 localPosition, Vector3 localScale)
    {
        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beam.name = "Glow Frame Edge";
        beam.transform.SetParent(visual, false);
        beam.transform.localPosition = localPosition;
        beam.transform.localScale = localScale;
        Destroy(beam.GetComponent<Collider>());
        Renderer renderer = beam.GetComponent<Renderer>();
        renderer.sharedMaterial = frameMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private void AdvanceGlow(float deltaTime)
    {
        if (outlineMaterial == null || frameMaterial == null) return;
        glowPulseTime += deltaTime;
        float phase = glowPulseTime / GlowPulseSeconds * Mathf.PI * 2f;
        float pulse = (Mathf.Sin(phase) + 1f) * 0.5f;
        float intensity = Mathf.Lerp(GlowMinimumIntensity, GlowMaximumIntensity, pulse);
        outlineMaterial.SetFloat(GlowIntensityId, intensity);
        frameMaterial.SetFloat(GlowIntensityId, intensity);
    }

    private void OnDestroy()
    {
        if (outlineMaterial != null) Destroy(outlineMaterial);
        if (frameMaterial != null) Destroy(frameMaterial);
    }

    private static Material GetMarkerMaterial(PowerUpType type)
    {
        int index = (int)type;
        if (MarkerMaterials[index] == null)
        {
            MarkerMaterials[index] = BombermanMaterials.Make($"Power Up {type}", ColorFor(type));
        }

        return MarkerMaterials[index];
    }

    private static Color ColorFor(PowerUpType type)
    {
        return type switch
        {
            PowerUpType.BombUp => new Color(0.15f, 0.2f, 0.9f),
            PowerUpType.FireUp => new Color(0.95f, 0.35f, 0.05f),
            PowerUpType.SpeedUp => new Color(0.1f, 0.85f, 0.95f),
            PowerUpType.Detonator => new Color(0.95f, 0.85f, 0.1f),
            PowerUpType.BombPass => new Color(0.6f, 0.2f, 0.85f),
            PowerUpType.WallPass => new Color(0.55f, 0.35f, 0.15f),
            PowerUpType.FlamePass => new Color(0.9f, 0.1f, 0.15f),
            _ => new Color(0.95f, 0.4f, 0.8f)
        };
    }

    private static string LabelFor(PowerUpType type)
    {
        return type switch
        {
            PowerUpType.BombUp => "B+",
            PowerUpType.FireUp => "F+",
            PowerUpType.SpeedUp => "S+",
            PowerUpType.Detonator => "DET",
            PowerUpType.BombPass => "BP",
            PowerUpType.WallPass => "WP",
            PowerUpType.FlamePass => "FP",
            _ => "?"
        };
    }
}
