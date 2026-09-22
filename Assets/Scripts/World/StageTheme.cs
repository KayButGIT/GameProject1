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
        [Tooltip("Models for this slot, picked at random per cell. A new entry starts at Position Y -0.5, Scale 1 and Weight 1, which fits the block models; floor and exit door models usually want Y 0.")]
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
            // The cell's primitive is scaled for its collider and fallback cube. Undoing that scale
            // keeps the model in the shape the team built it, with one cell counting as one unit.
            Vector3 cell = Inverse(parent.localScale);
            // Turning the offset with the model keeps off-center pivots centered in the cell.
            visual.transform.localPosition = Vector3.Scale(turn * variant.Position, cell);
            visual.transform.localRotation = turn * Quaternion.Euler(variant.Rotation) * variant.Prefab.transform.localRotation;
            visual.transform.localScale = Vector3.Scale(Vector3.Scale(variant.Prefab.transform.localScale, variant.Scale), cell);
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

        private static Vector3 Inverse(Vector3 scale)
        {
            return new Vector3(
                Mathf.Approximately(scale.x, 0f) ? 1f : 1f / scale.x,
                Mathf.Approximately(scale.y, 0f) ? 1f : 1f / scale.y,
                Mathf.Approximately(scale.z, 0f) ? 1f : 1f / scale.z);
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
            Variants ??= new List<VisualVariant>();
            if (legacyPrefab != null)
            {
                if (Variants.Count == 0)
                    Variants.Add(new VisualVariant { Prefab = legacyPrefab, Position = legacyPosition, Rotation = legacyRotation, Scale = legacyScale });
                legacyPrefab = null;
            }

            // Unity fills a new list element with zeros, which can neither draw nor be picked.
            // Such an entry gets the defaults a block model needs.
            foreach (VisualVariant variant in Variants)
            {
                if (variant != null && variant.Scale == Vector3.zero && variant.Weight == 0f)
                {
                    variant.Position = new Vector3(0f, -0.5f, 0f);
                    variant.Scale = Vector3.one;
                    variant.Weight = 1f;
                }
            }
        }
    }

    // Defaults give a bright daylight look with shadows falling toward the camera.
    [Serializable]
    public sealed class ThemeLighting
    {
        [Tooltip("Apply this lighting when the theme's stages load. Off keeps the scene's own lighting.")]
        public bool Enabled = true;
        [Tooltip("The team's own Directional Light prefab. Empty builds a light from the Sun settings below.")]
        public GameObject SunPrefab;
        [Tooltip("Used only while Sun Prefab is empty. A prefab keeps the values the team set on it.")]
        public Color SunColor = new(1f, 0.94f, 0.82f);
        [Tooltip("Used only while Sun Prefab is empty.")]
        [Min(0f)] public float SunIntensity = 1.35f;
        [Tooltip("Sun direction in degrees, used only while Sun Prefab is empty. The default shines from the upper left, so shadows fall down and to the right.")]
        public Vector3 SunRotation = new(50f, 150f, 0f);
        [Tooltip("Used only while Sun Prefab is empty.")]
        [Range(0f, 1f)] public float ShadowStrength = 0.8f;
        [Tooltip("Ambient light from above. Cool colors tint the shadows.")]
        public Color AmbientSky = new(0.55f, 0.65f, 0.8f);
        [Tooltip("Ambient light from the sides.")]
        public Color AmbientEquator = new(0.42f, 0.44f, 0.45f);
        [Tooltip("Ambient light from below.")]
        public Color AmbientGround = new(0.18f, 0.2f, 0.16f);
        [Tooltip("Color around the arena, used only while Skybox is empty.")]
        public Color Background = new(0.08f, 0.1f, 0.13f);
        [Tooltip("Sky drawn behind the arena. Empty keeps the flat Background color.")]
        public Material Skybox;
        [Tooltip("Light the stage from the sky image. Off uses the three ambient colors. Ignored while Skybox is empty.")]
        public bool AmbientFromSkybox = true;
        [Tooltip("How strong the light from the sky is.")]
        [Min(0f)] public float SkyboxAmbientIntensity = 1f;
        [Tooltip("Post-processing for these stages. Empty uses the built-in day profile.")]
        public VolumeProfile PostProcessing;
        [Tooltip("Color of the light flash when a bomb explodes.")]
        public Color ExplosionColor = new(1f, 0.6f, 0.2f);
        [Min(0f)] public float ExplosionIntensity = 6f;
    }

    [Serializable]
    public sealed class BorderVisual
    {
        [Tooltip("Model placed around the arena. Empty keeps the built-in rails.")]
        public GameObject Prefab;
        [Tooltip("Nudge from the arena center. The current border sits 0.04 below the floor.")]
        public Vector3 Offset;
        public Vector3 Rotation;
        [Tooltip("Stretch the model across the map, so it follows the Width and Height settings.")]
        public bool AutoFit = true;
        [Tooltip("Multiplies the fitted size. Y is always taken from here.")]
        public Vector3 ExtraScale = Vector3.one;
    }

    [Serializable]
    public sealed class SceneryVisual
    {
        [Tooltip("Background placed around the arena, such as ground and water. Empty leaves the scene's own background alone.")]
        public GameObject Prefab;
        [Tooltip("Nudge from the arena center. The prefab's own position in a scene is ignored; its pivot lands on the center.")]
        public Vector3 Offset;
        public Vector3 Rotation;
        [Tooltip("Multiplies the prefab's own scale.")]
        public Vector3 Scale = Vector3.one;
        [Tooltip("Put the middle of the model over the arena. Off uses the prefab's own pivot, which sits in a corner for terrain.")]
        public bool CenterOnArena = true;

        // Colliders stay as the team built them: scenery sits under the arena floor and never blocks the player.
        public GameObject Attach(Transform parent, Vector3 arenaCenter)
        {
            if (Prefab == null) return null;
            GameObject holder = new(SceneryName);
            holder.transform.SetParent(parent);
            holder.transform.position = arenaCenter + Offset;

            GameObject visual = Instantiate(Prefab, holder.transform);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Prefab.transform.localRotation;
            visual.transform.localScale = Vector3.Scale(Prefab.transform.localScale, Scale);

            // Measured while the holder is still unturned, so the numbers are in the map's own axes.
            if (CenterOnArena && VisualBounds.TryMeasure(holder.transform, visual, out Bounds bounds))
                visual.transform.localPosition -= new Vector3(bounds.center.x, 0f, bounds.center.z);

            holder.transform.localRotation = Quaternion.Euler(Rotation);
            return holder;
        }

        public bool Matches(SceneryVisual other)
        {
            return other != null && Prefab == other.Prefab && Offset == other.Offset && Rotation == other.Rotation
                && Scale == other.Scale && CenterOnArena == other.CenterOnArena;
        }

        public SceneryVisual Copy()
        {
            return new SceneryVisual { Prefab = Prefab, Offset = Offset, Rotation = Rotation, Scale = Scale, CenterOnArena = CenterOnArena };
        }
    }

    public const string SceneryName = "Stage Scenery";

    [Tooltip("Indestructible blocks on the outer ring. Leave empty to use Solid Block.")]
    public VisualSlot BorderBlock = new();
    [Tooltip("Indestructible pillars inside the arena, and the outer ring when Border Block is empty.")]
    public VisualSlot SolidBlock = new();
    public VisualSlot BreakableBlock = new();
    public VisualSlot Floor = new();
    public VisualSlot ExitDoor = new();
    [Tooltip("Model framing the arena. Leave the prefab empty to keep the built-in rails.")]
    public BorderVisual Border = new();
    [Tooltip("Background around the arena, centered on it. Built once and kept until a stage uses different scenery.")]
    public SceneryVisual Scenery = new();
    [Tooltip("Sun, ambient light, background, post-processing, and explosion flashes for this theme's stages.")]
    public ThemeLighting Lighting = new();
}
