using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Marks the see-through copies, so burn effects that swap every renderer's materials leave them alone.
public sealed class XrayGhost : MonoBehaviour
{
}

// Shows a see-through silhouette of a character while it walks through breakable blocks.
// It works for the player with Wall Pass and for enemies that can pass breakable blocks.
// The silhouette is a copy of the character's meshes drawn with the Bomberman/Actor Xray shader,
// which only paints pixels hidden behind something else. Copies are built the first time they are needed.
[DisallowMultipleComponent]
public sealed class XrayVision : MonoBehaviour
{
    private const string ShaderResourceName = "ActorXray";
    private const string ShaderName = "Bomberman/Actor Xray";
    // How close to a breakable block the character's center must be before the silhouette fades in.
    private const float EdgeMargin = 0.3f;
    private const float FadeSeconds = 0.15f;
    private static readonly int ColorId = Shader.PropertyToID("_XrayColor");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly Color PlayerColor = new(0.25f, 0.95f, 1f, 1f);
    private static readonly Color EnemyColor = new(1f, 0.4f, 0.3f, 1f);

    private GridController actor;
    private BombermanPrototype game;
    private Material material;
    private readonly List<GameObject> ghosts = new();
    private bool built;
    private bool ghostsVisible;
    private float blend;

    // How many characters are showing a silhouette right now. BombermanPrototype switches the solid-block masks on while this is above zero.
    public static int VisibleCount { get; private set; }

    public static XrayVision Attach(GridController actor, BombermanPrototype game)
    {
        if (actor == null) return null;
        XrayVision vision = actor.GetComponent<XrayVision>();
        if (vision == null) vision = actor.gameObject.AddComponent<XrayVision>();
        vision.actor = actor;
        vision.game = game;
        return vision;
    }

    public static bool IsGhost(Renderer renderer)
    {
        return renderer != null && renderer.GetComponent<XrayGhost>() != null;
    }

    private void LateUpdate()
    {
        if (actor == null || game == null) return;

        if (actor.IsDead)
        {
            blend = 0f;
            SetGhostsVisible(false);
            return;
        }

        bool wanted = actor.CanPassDestructibleWalls && game.IsNearBreakableBlock(actor.transform.position, EdgeMargin);
        if (wanted && !built) Build();
        if (!built || material == null) return;

        blend = Mathf.MoveTowards(blend, wanted ? 1f : 0f, Time.unscaledDeltaTime / FadeSeconds);
        material.SetFloat(IntensityId, blend);
        SetGhostsVisible(blend > 0.001f);
    }

    private void SetGhostsVisible(bool visible)
    {
        if (ghostsVisible == visible) return;
        ghostsVisible = visible;
        VisibleCount = Mathf.Max(0, VisibleCount + (visible ? 1 : -1));
        foreach (GameObject ghost in ghosts)
            if (ghost != null) ghost.SetActive(visible);
    }

    private void Build()
    {
        built = true;
        Shader shader = Resources.Load<Shader>(ShaderResourceName);
        if (shader == null) shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError("Xray shader is missing. Put ActorXray.shader in Assets/Resources.", this);
            return;
        }

        material = new Material(shader) { name = "Actor Xray" };
        material.SetColor(ColorId, actor.IsPlayer ? PlayerColor : EnemyColor);
        material.SetFloat(IntensityId, 0f);

        foreach (Renderer source in actor.GetComponentsInChildren<Renderer>(true))
        {
            if (source == null || IsGhost(source) || !source.enabled || !source.gameObject.activeInHierarchy) continue;
            if (source is SkinnedMeshRenderer skinned) AddSkinnedGhost(skinned);
            else if (source is MeshRenderer && source.TryGetComponent(out MeshFilter filter)) AddMeshGhost(source, filter.sharedMesh);
        }
    }

    private void AddSkinnedGhost(SkinnedMeshRenderer source)
    {
        Mesh mesh = source.sharedMesh;
        if (mesh == null) return;

        GameObject ghost = CreateGhostObject(source);
        SkinnedMeshRenderer copy = ghost.AddComponent<SkinnedMeshRenderer>();
        copy.sharedMesh = mesh;
        // Sharing the bones makes the copy follow the animation without any extra work.
        copy.bones = source.bones;
        copy.rootBone = source.rootBone;
        copy.localBounds = source.localBounds;
        copy.updateWhenOffscreen = source.updateWhenOffscreen;
        copy.quality = source.quality;
        ConfigureGhost(copy, mesh);
    }

    private void AddMeshGhost(Renderer source, Mesh mesh)
    {
        if (mesh == null) return;

        GameObject ghost = CreateGhostObject(source);
        ghost.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer copy = ghost.AddComponent<MeshRenderer>();
        ConfigureGhost(copy, mesh);
    }

    private GameObject CreateGhostObject(Renderer source)
    {
        GameObject ghost = new("Xray Ghost");
        ghost.layer = source.gameObject.layer;
        ghost.transform.SetParent(source.transform, false);
        ghost.AddComponent<XrayGhost>();
        ghost.SetActive(false);
        ghosts.Add(ghost);
        return ghost;
    }

    private void ConfigureGhost(Renderer copy, Mesh mesh)
    {
        // One xray material per submesh, so every part of a multi-material model is covered.
        Material[] materials = new Material[Mathf.Max(1, mesh.subMeshCount)];
        for (int i = 0; i < materials.Length; i++) materials[i] = material;
        copy.sharedMaterials = materials;
        copy.shadowCastingMode = ShadowCastingMode.Off;
        copy.receiveShadows = false;
        copy.lightProbeUsage = LightProbeUsage.Off;
        copy.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    private void OnDisable()
    {
        blend = 0f;
        SetGhostsVisible(false);
    }

    private void OnDestroy()
    {
        SetGhostsVisible(false);
        if (material != null) Destroy(material);
    }
}