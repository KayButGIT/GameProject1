using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class ExitDoor : MonoBehaviour
{
    private const float FadeSeconds = 0.5f;

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
    private Material lockedMaterial;
    private Material openMaterial;
    private readonly List<LockBarVisual> lockBars = new();
    private float fadeTime;

    public Vector2Int Cell { get; private set; }
    public bool IsRevealed { get; private set; }
    public bool IsOpen { get; private set; }

    public static ExitDoor Create(Vector2Int cell, Vector3 position, Transform parent, BombermanMaterials materials, StageTheme.VisualSlot themeVisual, System.Random random, bool revealed)
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
        if (themeVisual != null && themeVisual.Attach(holder.transform, random))
        {
            door.placeholder.enabled = false;
            Transform model = holder.transform.Find("Visual");
            if (model != null && VisualBounds.TryMeasure(root.transform, model.gameObject, out Bounds bounds))
                holder.transform.localPosition = new Vector3(0f, 0.005f - bounds.min.y, 0f);
            door.InitializeLockBars(model);
        }

        door.IsRevealed = revealed;
        root.SetActive(revealed);
        return door;
    }

    public void Reveal()
    {
        IsRevealed = true;
        gameObject.SetActive(true);
    }

    public void SetOpen(bool open)
    {
        if (IsOpen == open)
        {
            return;
        }

        IsOpen = open;
        placeholder.sharedMaterial = open ? openMaterial : lockedMaterial;
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
        AdvanceLockBarFade(Time.deltaTime);
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
        foreach (LockBarVisual bar in lockBars)
            foreach (Material material in bar.FadeMaterials)
                if (material != null) BombermanMap.DestroyGenerated(material);
    }
}
