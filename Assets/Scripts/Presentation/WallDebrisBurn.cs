using System.Collections.Generic;
using UnityEngine;

/// <summary>Gives one wall burst independent Fire Dissolve material instances.</summary>
public sealed class WallDebrisBurn : MonoBehaviour
{
    private sealed class RendererMaterials
    {
        public Renderer Renderer;
        public Material[] Original;
        public Material[] Burn;
        public float StartDelay;
        public float Duration;
    }

    private readonly List<Material> burnMaterials = new();
    private readonly List<RendererMaterials> rendererMaterials = new();
    private float startDelay;
    private float duration;
    private float staggerFraction;
    private float clippingStart;
    private float clippingEnd;
    private float elapsed;
    private bool finished;

    public static WallDebrisBurn Apply(GameObject root, Material template, float delay, float burnDuration, float startClipping, float endClipping, float stagger = 0f)
    {
        if (root == null || template == null || !template.HasProperty("_Clipping")) return null;

        WallDebrisBurn effect = root.AddComponent<WallDebrisBurn>();
        effect.Initialize(template, delay, burnDuration, startClipping, endClipping, stagger);
        return effect;
    }

    private void Initialize(Material template, float delay, float burnDuration, float startClipping, float endClipping, float stagger)
    {
        startDelay = Mathf.Max(0f, delay);
        duration = Mathf.Max(0.01f, burnDuration);
        staggerFraction = Mathf.Clamp01(stagger);
        clippingStart = Mathf.Clamp01(startClipping);
        clippingEnd = Mathf.Clamp01(endClipping);

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || !renderer.enabled || !IsBurnableDebris(renderer)) continue;

            Material[] originals = renderer.sharedMaterials;
            Material[] replacements = new Material[originals.Length];
            Material[] rendererBurnMaterials = new Material[originals.Length];
            bool replacedAny = false;
            for (int i = 0; i < originals.Length; i++)
            {
                Material source = originals[i];
                if (source == null) continue;

                Material burn = new Material(template) { name = $"{source.name} Wall Debris Burn" };
                CopyAppearance(source, burn);
                burn.SetFloat("_Clipping", clippingStart);
                burnMaterials.Add(burn);
                replacements[i] = burn;
                rendererBurnMaterials[i] = burn;
                replacedAny = true;
            }

            if (!replacedAny) continue;
            float rendererDelay = duration * Random.Range(0f, staggerFraction);
            float rendererDuration = Random.Range(duration - rendererDelay, duration);
            rendererMaterials.Add(new RendererMaterials
            {
                Renderer = renderer,
                Original = originals,
                Burn = rendererBurnMaterials,
                StartDelay = startDelay + rendererDelay,
                Duration = Mathf.Max(0.01f, rendererDuration)
            });
            renderer.sharedMaterials = replacements;
        }
    }

    private static bool IsBurnableDebris(Renderer renderer)
    {
        // FireDie's alpha-clip shader can cull mesh particles entirely. Keep the
        // particle renderer's block material so the chunk burst remains visible;
        // the particle system shrinks those chunks away over its lifetime instead.
        if (renderer is ParticleSystemRenderer) return false;

        return renderer is MeshRenderer || renderer is SkinnedMeshRenderer;
    }

    private static void CopyAppearance(Material source, Material destination)
    {
        Texture albedo = GetTexture(source, "_BaseMap", "_MainTex", "_BaseColorMap", "_ColorMap");
        if (destination.HasProperty("_MainTex"))
        {
            destination.SetTexture("_MainTex", albedo != null ? albedo : Texture2D.whiteTexture);
            if (albedo != null)
                CopyTextureTransform(source, destination, "_MainTex", "_BaseMap", "_MainTex", "_BaseColorMap", "_ColorMap");
        }

        if (destination.HasProperty("_BaseColor"))
        {
            Color tint = source.HasProperty("_BaseColor")
                ? source.GetColor("_BaseColor")
                : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
            destination.SetColor("_BaseColor", tint);
        }

        Texture normal = GetTexture(source, "_Normal", "_BumpMap", "_NormalMap");
        if (destination.HasProperty("_Normal"))
        {
            destination.SetTexture("_Normal", normal != null ? normal : Texture2D.normalTexture);
            if (normal != null)
                CopyTextureTransform(source, destination, "_Normal", "_Normal", "_BumpMap", "_NormalMap");
        }
    }

    private static Texture GetTexture(Material material, params string[] properties)
    {
        foreach (string property in properties)
            if (material.HasProperty(property) && material.GetTexture(property) != null)
                return material.GetTexture(property);
        return null;
    }

    private static void CopyTextureTransform(Material source, Material destination, string target, params string[] candidates)
    {
        foreach (string candidate in candidates)
        {
            if (!source.HasProperty(candidate) || source.GetTexture(candidate) == null) continue;
            destination.SetTextureScale(target, source.GetTextureScale(candidate));
            destination.SetTextureOffset(target, source.GetTextureOffset(candidate));
            return;
        }
    }

    private void Update()
    {
        if (finished) return;
        elapsed += Time.deltaTime;
        if (elapsed < startDelay) return;

        finished = true;
        foreach (RendererMaterials set in rendererMaterials)
        {
            float progress = Mathf.Clamp01((elapsed - set.StartDelay) / set.Duration);
            float clipping = Mathf.Lerp(clippingStart, clippingEnd, progress);
            foreach (Material material in set.Burn)
                if (material != null)
                    material.SetFloat("_Clipping", clipping);
            if (progress < 1f) finished = false;
        }
    }

    private void OnDestroy()
    {
        foreach (RendererMaterials set in rendererMaterials)
            if (set.Renderer != null)
                set.Renderer.sharedMaterials = set.Original;

        foreach (Material material in burnMaterials)
            if (material != null)
                Destroy(material);
    }
}
