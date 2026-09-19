using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Bomberman/Stage Theme")]
public sealed class StageTheme : ScriptableObject
{
    [Serializable]
    public sealed class VisualVariant
    {
        public GameObject Prefab;
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 Scale = Vector3.one;
        [Tooltip("Relative chance of picking this variant. Zero never picks it.")]
        [Min(0f)] public float Weight = 1f;
    }

    [Serializable]
    public sealed class VisualSlot : ISerializationCallbackReceiver
    {
        public List<VisualVariant> Variants = new();
        [Tooltip("Also turn each placed model by a random multiple of 90 degrees around Y.")]
        public bool RandomYRotation;

        public bool HasVariants => Variants.Exists(IsUsable);

        // Single-model slots saved before variants existed.
        [SerializeField, HideInInspector, FormerlySerializedAs("Prefab")] private GameObject legacyPrefab;
        [SerializeField, HideInInspector, FormerlySerializedAs("Position")] private Vector3 legacyPosition;
        [SerializeField, HideInInspector, FormerlySerializedAs("Rotation")] private Vector3 legacyRotation;
        [SerializeField, HideInInspector, FormerlySerializedAs("Scale")] private Vector3 legacyScale = Vector3.one;

        public bool Attach(Transform parent, System.Random random)
        {
            VisualVariant variant = Pick(random);
            if (variant == null) return false;
            Quaternion turn = RandomYRotation ? Quaternion.Euler(0f, 90f * random.Next(4), 0f) : Quaternion.identity;
            GameObject visual = Instantiate(variant.Prefab, parent);
            visual.name = "Visual";
            // Turning the offset with the model keeps off-center pivots centered in the cell.
            visual.transform.localPosition = turn * variant.Position;
            visual.transform.localRotation = turn * Quaternion.Euler(variant.Rotation) * variant.Prefab.transform.localRotation;
            visual.transform.localScale = Vector3.Scale(variant.Prefab.transform.localScale, variant.Scale);
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (Rigidbody body in visual.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }
            return true;
        }

        private VisualVariant Pick(System.Random random)
        {
            float totalWeight = 0f;
            foreach (VisualVariant variant in Variants)
                if (IsUsable(variant)) totalWeight += variant.Weight;
            if (totalWeight <= 0f) return null;

            double roll = random.NextDouble() * totalWeight;
            VisualVariant picked = null;
            foreach (VisualVariant variant in Variants)
            {
                if (!IsUsable(variant)) continue;
                picked = variant;
                roll -= variant.Weight;
                if (roll < 0d) break;
            }
            return picked;
        }

        private static bool IsUsable(VisualVariant variant)
        {
            return variant != null && variant.Prefab != null && variant.Weight > 0f;
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            if (legacyPrefab == null) return;
            Variants ??= new List<VisualVariant>();
            if (Variants.Count == 0)
                Variants.Add(new VisualVariant { Prefab = legacyPrefab, Position = legacyPosition, Rotation = legacyRotation, Scale = legacyScale });
            legacyPrefab = null;
        }
    }

    // Defaults give a bright daylight look with shadows falling toward the camera.
    [Serializable]
    public sealed class ThemeLighting
    {
        [Tooltip("Apply this lighting when the theme's stages load. Off keeps the scene's own lighting.")]
        public bool Enabled = true;
        public Color SunColor = new(1f, 0.94f, 0.82f);
        [Min(0f)] public float SunIntensity = 1.35f;
        [Tooltip("Sun direction in degrees. The default shines from the upper left, so shadows fall down and to the right.")]
        public Vector3 SunRotation = new(50f, 150f, 0f);
        [Range(0f, 1f)] public float ShadowStrength = 0.8f;
        [Tooltip("Ambient light from above. Cool colors tint the shadows.")]
        public Color AmbientSky = new(0.55f, 0.65f, 0.8f);
        [Tooltip("Ambient light from the sides.")]
        public Color AmbientEquator = new(0.42f, 0.44f, 0.45f);
        [Tooltip("Ambient light from below.")]
        public Color AmbientGround = new(0.18f, 0.2f, 0.16f);
        [Tooltip("Color around the arena.")]
        public Color Background = new(0.08f, 0.1f, 0.13f);
        [Tooltip("Post-processing for these stages. Empty uses the built-in day profile.")]
        public VolumeProfile PostProcessing;
        [Tooltip("Color of the light flash when a bomb explodes.")]
        public Color ExplosionColor = new(1f, 0.6f, 0.2f);
        [Min(0f)] public float ExplosionIntensity = 6f;
    }

    [Tooltip("Indestructible blocks on the outer ring. Leave empty to use Solid Block.")]
    public VisualSlot BorderBlock = new();
    [Tooltip("Indestructible pillars inside the arena, and the outer ring when Border Block is empty.")]
    public VisualSlot SolidBlock = new();
    public VisualSlot BreakableBlock = new();
    public VisualSlot Floor = new();
    public VisualSlot ExitDoor = new();
    [Tooltip("Material of the thin rails outside the arena.")]
    public Material BorderMaterial;
    [Tooltip("Sun, ambient light, background, post-processing, and explosion flashes for this theme's stages.")]
    public ThemeLighting Lighting = new();
}
