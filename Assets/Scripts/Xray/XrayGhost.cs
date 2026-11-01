using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class XrayGhost : MonoBehaviour
{
}

[DisallowMultipleComponent]
public sealed class XrayVision : MonoBehaviour
{
    private const int MaxOverlapBounds = 9;
    private const string ShaderResourceName = "ActorXray";
    private const string ShaderName = "Bomberman/Actor Xray";
    private const float FadeSeconds = 0.3f;
    private static readonly int ColorId = Shader.PropertyToID("_XrayColor");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
    private static readonly int OverlapBoundsMinId = Shader.PropertyToID("_OverlapBoundsMin");
    private static readonly int OverlapBoundsMaxId = Shader.PropertyToID("_OverlapBoundsMax");
    private static readonly int OverlapBoundsCountId = Shader.PropertyToID("_OverlapBoundsCount");

    private GridController actor;
    private BombermanPrototype game;
    private Material material;
    private readonly List<GameObject> ghosts = new();
    private bool built;
    private bool ghostsVisible;
    private float blend;
    private readonly Bounds[] overlapBounds = new Bounds[MaxOverlapBounds];
    private readonly Vector4[] overlapBoundsMin = new Vector4[MaxOverlapBounds];
    private readonly Vector4[] overlapBoundsMax = new Vector4[MaxOverlapBounds];
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

        int overlapBoundsCount = !actor.IsDead && actor.CanPassDestructibleWalls
            ? game.GetNearbyDestructibleBlockBounds(actor.transform.position, overlapBounds)
            : 0;
        bool wanted = overlapBoundsCount > 0;
        if (wanted && !built) Build();
        if (!built || material == null) return;

        if (overlapBoundsCount > 0)
            SetOverlapBounds(overlapBoundsCount);

        material.SetColor(ColorId, game.GetXrayColor(actor.IsPlayer));
        material.SetFloat(OpacityId, game.XrayOpacity);
        blend = Mathf.MoveTowards(blend, wanted ? 1f : 0f, Time.unscaledDeltaTime / FadeSeconds);
        material.SetFloat(IntensityId, blend);
        if (!wanted && blend <= 0.001f)
            material.SetFloat(OverlapBoundsCountId, 0f);
        SetGhostsVisible(blend > 0.001f);
    }

    private void SetOverlapBounds(int count)
    {
        for (int i = 0; i < MaxOverlapBounds; i++)
        {
            if (i < count)
            {
                Vector3 min = overlapBounds[i].min;
                Vector3 max = overlapBounds[i].max;
                overlapBoundsMin[i] = new Vector4(min.x, min.y, min.z, 0f);
                overlapBoundsMax[i] = new Vector4(max.x, max.y, max.z, 0f);
            }
            else
            {
                overlapBoundsMin[i] = Vector4.zero;
                overlapBoundsMax[i] = Vector4.zero;
            }
        }

        material.SetVectorArray(OverlapBoundsMinId, overlapBoundsMin);
        material.SetVectorArray(OverlapBoundsMaxId, overlapBoundsMax);
        material.SetFloat(OverlapBoundsCountId, count);
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
        material.SetColor(ColorId, game.GetXrayColor(actor.IsPlayer));
        material.SetFloat(OpacityId, game.XrayOpacity);
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
