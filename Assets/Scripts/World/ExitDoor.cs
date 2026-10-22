using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class ExitDoor : MonoBehaviour
{
    private const float FadeSeconds = 0.5f;
    private const float HighlightPulseSeconds = 2.5f;
    private const float FramePadding = 0.015f;
    private const float FrameBeamWidth = 0.018f;
    private const float OutlineMinimumIntensity = 0.25f;
    // Keep the frame comfortably above both stage Bloom thresholds (0.75 and 0.9).
    private const float OutlineMaximumIntensity = 3f;
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineIntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

    private sealed class LockBarVisual
    {
        public Renderer Renderer;
        public Material[] OriginalMaterials;
        public Material[] FadeMaterials;
        public Color[] OriginalColors;
        public int[] ColorProperties;
        public bool OriginallyEnabled;
    }

    private Renderer placeholder;
    private GameObject highlight;
    private Material highlightMaterial;
    private Material frameMaterial;
    private Material lockedMaterial;
    private Material openMaterial;
    private readonly List<LockBarVisual> lockBars = new();
    private float fadeTime;
    private float highlightPulseTime;

    public Vector2Int Cell { get; private set; }
    public bool IsRevealed { get; private set; }
    public bool IsOpen { get; private set; }

    public static ExitDoor Create(Vector2Int cell, Vector3 position, Transform parent, BombermanMaterials materials, StageTheme.VisualSlot themeVisual, System.Random random, bool revealed)
    {
        return Create(cell, position, parent, materials, themeVisual, random, revealed, Color.white);
    }

    internal static ExitDoor Create(Vector2Int cell, Vector3 position, Transform parent, BombermanMaterials materials, StageTheme.VisualSlot themeVisual, System.Random random, bool revealed, Color outlineColor)
    {
        GameObject root = new($"Exit Door {cell.x},{cell.y}");
        root.transform.SetParent(parent);
        root.transform.position = position;

        GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        slab.name = "Door";
        slab.transform.SetParent(root.transform, false);
        slab.transform.localPosition = new Vector3(0f, 0.035f, 0f);
        slab.transform.localScale = new Vector3(0.78f, 0.06f, 0.78f);
        BombermanMap.DestroyGenerated(slab.GetComponent<Collider>());

        ExitDoor door = root.AddComponent<ExitDoor>();
        door.Cell = cell;
        door.lockedMaterial = materials.ExitDoorLocked;
        door.openMaterial = materials.ExitDoorOpen;
        door.placeholder = slab.GetComponent<Renderer>();
        door.placeholder.sharedMaterial = door.lockedMaterial;
        // The position supplied by the map is the floor surface at this cell.
        GameObject holder = new("Door Visual");
        holder.transform.SetParent(root.transform, false);
        GameObject highlightSource = slab;
        if (themeVisual != null && themeVisual.Attach(holder.transform, random))
        {
            door.placeholder.enabled = false;
            Transform model = holder.transform.Find("Visual");
            if (model != null && VisualBounds.TryMeasure(root.transform, model.gameObject, out Bounds bounds))
            {
                float yOffset = 0.005f - bounds.min.y;
                holder.transform.localPosition = new Vector3(0f, yOffset, 0f);
            }
            if (model != null) highlightSource = model.gameObject;
            door.InitializeLockBars(model);
        }
        door.CreateHighlight(root.transform, highlightSource, outlineColor);

        door.IsRevealed = revealed;
        root.SetActive(revealed);
        return door;
    }

    public void Reveal()
    {
        IsRevealed = true;
        gameObject.SetActive(true);
        RefreshHighlightVisibility();
    }

    public void SetOpen(bool open)
    {
        if (IsOpen == open)
        {
            return;
        }

        IsOpen = open;
        placeholder.sharedMaterial = open ? openMaterial : lockedMaterial;
        if (open) highlightPulseTime = 0f;
        RefreshHighlightVisibility();
        fadeTime = 0f;
        foreach (LockBarVisual bar in lockBars)
        {
            for (int i = 0; i < bar.FadeMaterials.Length; i++)
                if (bar.FadeMaterials[i] != null && bar.ColorProperties[i] != -1)
                    bar.FadeMaterials[i].SetColor(bar.ColorProperties[i], bar.OriginalColors[i]);
            bar.Renderer.sharedMaterials = open ? bar.FadeMaterials : bar.OriginalMaterials;
            bar.Renderer.enabled = bar.OriginallyEnabled;
        }
    }

    private void Update()
    {
        // Scaled time makes the fade freeze while the game is paused.
        float deltaTime = Time.deltaTime;
        AdvanceLockBarFade(deltaTime);
        AdvanceHighlightPulse(deltaTime);
    }

    private void CreateHighlight(Transform parent, GameObject source, Color outlineColor)
    {
        highlight = new GameObject("Exit Highlight");
        highlight.transform.SetParent(parent, false);

        Shader outlineShader = Resources.Load<Shader>("ExitOutline");
        if (outlineShader == null) outlineShader = Shader.Find("Bomberman/Exit Outline");
        Shader frameShader = Resources.Load<Shader>("ExitGlowFrame");
        if (frameShader == null) frameShader = Shader.Find("Bomberman/Exit Glow Frame");
        if (outlineShader == null || frameShader == null)
        {
            Debug.LogError("Exit outline or glow frame shader is missing from Resources.", this);
            highlight.SetActive(false);
            return;
        }
        highlightMaterial = new Material(outlineShader) { name = "Exit Door Outline Glow" };
        highlightMaterial.SetColor(OutlineColorId, outlineColor);
        highlightMaterial.SetFloat(OutlineWidthId, 0.025f);
        highlightMaterial.SetFloat(OutlineIntensityId, OutlineMinimumIntensity);
        CreateOutlineShell(source, highlightMaterial);

        frameMaterial = new Material(frameShader) { name = "Exit Door 3D Glow Frame" };
        frameMaterial.SetColor(OutlineColorId, outlineColor);
        frameMaterial.SetFloat(OutlineIntensityId, OutlineMinimumIntensity);
        CreateGlowFrame(parent, source);
        highlight.SetActive(false);
    }

    private void CreateOutlineShell(GameObject source, Material material)
    {
        GameObject shell = Instantiate(source, highlight.transform);
        shell.name = "Outline Shell";
        foreach (Collider collider in shell.GetComponentsInChildren<Collider>(true))
            BombermanMap.DestroyGenerated(collider);
        foreach (Rigidbody body in shell.GetComponentsInChildren<Rigidbody>(true))
            BombermanMap.DestroyGenerated(body);
        foreach (MonoBehaviour behaviour in shell.GetComponentsInChildren<MonoBehaviour>(true))
            BombermanMap.DestroyGenerated(behaviour);

        foreach (Renderer renderer in shell.GetComponentsInChildren<Renderer>(true))
        {
            Material[] outlineMaterials = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < outlineMaterials.Length; i++) outlineMaterials[i] = material;
            renderer.sharedMaterials = outlineMaterials;
            renderer.enabled = true;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private void CreateGlowFrame(Transform parent, GameObject source)
    {
        if (!VisualBounds.TryMeasure(parent, source, out Bounds bounds))
            bounds = new Bounds(new Vector3(0f, 0.3f, 0f), new Vector3(0.78f, 0.6f, 0.78f));
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

        // Twelve glowing edges create a 3D rectangular frame around the door.
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
            AddFrameBeam("Horizontal Frame", center + new Vector3(0f, halfY * y, halfZ * z), new Vector3(size.x + beam, beam, beam));

        for (int x = -1; x <= 1; x += 2)
        for (int z = -1; z <= 1; z += 2)
            AddFrameBeam("Vertical Frame", center + new Vector3(halfX * x, 0f, halfZ * z), new Vector3(beam, size.y + beam, beam));

        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
            AddFrameBeam("Depth Frame", center + new Vector3(halfX * x, halfY * y, 0f), new Vector3(beam, beam, size.z + beam));
    }

    private void AddFrameBeam(string beamName, Vector3 localPosition, Vector3 localScale)
    {
        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beam.name = beamName;
        beam.transform.SetParent(highlight.transform, false);
        beam.transform.localPosition = localPosition;
        beam.transform.localScale = localScale;
        BombermanMap.DestroyGenerated(beam.GetComponent<Collider>());
        Renderer renderer = beam.GetComponent<Renderer>();
        renderer.sharedMaterial = frameMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private void RefreshHighlightVisibility()
    {
        if (highlight != null)
            highlight.SetActive(IsRevealed && IsOpen);
    }

    private void AdvanceHighlightPulse(float deltaTime)
    {
        if (highlight == null || highlightMaterial == null || !highlight.activeSelf) return;

        highlightPulseTime += deltaTime;
        float phase = highlightPulseTime / HighlightPulseSeconds * Mathf.PI * 2f;
        float pulse = (Mathf.Sin(phase) + 1f) * 0.5f;
        float intensity = Mathf.Lerp(OutlineMinimumIntensity, OutlineMaximumIntensity, pulse);
        highlightMaterial.SetFloat(OutlineIntensityId, intensity);
        frameMaterial.SetFloat(OutlineIntensityId, intensity);
    }

    private void AdvanceLockBarFade(float deltaTime)
    {
        if (!IsOpen || fadeTime >= FadeSeconds) return;
        fadeTime = Mathf.Min(FadeSeconds, fadeTime + deltaTime);
        float opacity = 1f - fadeTime / FadeSeconds;
        foreach (LockBarVisual bar in lockBars)
        {
            for (int i = 0; i < bar.FadeMaterials.Length; i++)
            {
                if (bar.FadeMaterials[i] == null || bar.ColorProperties[i] == -1) continue;
                Color color = bar.OriginalColors[i];
                color.a *= opacity;
                bar.FadeMaterials[i].SetColor(bar.ColorProperties[i], color);
            }
            if (fadeTime >= FadeSeconds) bar.Renderer.enabled = false;
        }
    }

    private void InitializeLockBars(Transform model)
    {
        if (model == null) return;
        foreach (Transform part in model.GetComponentsInChildren<Transform>(true))
        {
            if (part.name != "DoorBar") continue;
            foreach (Renderer renderer in part.GetComponentsInChildren<Renderer>(true))
            {
                Material[] originals = renderer.sharedMaterials;
                LockBarVisual bar = new()
                {
                    Renderer = renderer,
                    OriginalMaterials = originals,
                    FadeMaterials = new Material[originals.Length],
                    OriginalColors = new Color[originals.Length],
                    ColorProperties = new int[originals.Length],
                    OriginallyEnabled = renderer.enabled
                };
                for (int i = 0; i < originals.Length; i++)
                {
                    bar.ColorProperties[i] = -1;
                    if (originals[i] == null) continue;
                    Material material = new(originals[i]) { name = originals[i].name + " (" + part.name + " Fade)" };
                    bar.FadeMaterials[i] = material;
                    int colorProperty = material.HasProperty("_BaseColor") ? Shader.PropertyToID("_BaseColor")
                        : material.HasProperty("_Color") ? Shader.PropertyToID("_Color") : -1;
                    bar.ColorProperties[i] = colorProperty;
                    if (colorProperty != -1) bar.OriginalColors[i] = material.GetColor(colorProperty);
                    material.SetFloat("_Surface", 1f);
                    material.SetFloat("_Blend", 0f);
                    material.SetFloat("_BlendModePreserveSpecular", 0f);
                    material.SetFloat("_AlphaClip", 0f);
                    material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                    material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_ZWrite", 0f);
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.DisableKeyword("_ALPHATEST_ON");
                    material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    material.DisableKeyword("_ALPHAMODULATE_ON");
                    material.SetShaderPassEnabled("ShadowCaster", false);
                    material.renderQueue = (int)RenderQueue.Transparent;
                }
                lockBars.Add(bar);
            }
        }
    }

    private void OnDestroy()
    {
        if (highlightMaterial != null) BombermanMap.DestroyGenerated(highlightMaterial);
        if (frameMaterial != null) BombermanMap.DestroyGenerated(frameMaterial);

        foreach (LockBarVisual bar in lockBars)
            foreach (Material material in bar.FadeMaterials)
                if (material != null) BombermanMap.DestroyGenerated(material);
    }
}
