using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Authoring settings for a saved grass mesh. Wind animation runs entirely in the shader.</summary>
[DisallowMultipleComponent]
public sealed class GrassBlock : MonoBehaviour, ISerializationCallbackReceiver
{
    [Range(1, 2000)] public int BladeCount = 160;
    [Min(0.01f)] public float MinimumHeight = 0.15f;
    [Min(0.01f)] public float MaximumHeight = 0.30f;
    [Tooltip("Width of a blade as a percent of the block, so 100 is a blade as wide as the cell.")]
    [Range(0.5f, 100f)] public float BladeWidthPercent = 3f;
    public int RandomSeed = 87597;
    [HideInInspector] public MeshFilter GrassMesh;

    /// <summary>The width in cell units the mesh is built with.</summary>
    public float BladeWidth => BladeWidthPercent * 0.01f;

    // Widths saved before the percent slider existed.
    [SerializeField, HideInInspector, FormerlySerializedAs("BladeWidth")] private float legacyBladeWidth;

    public void OnBeforeSerialize()
    {
    }

    public void OnAfterDeserialize()
    {
        if (legacyBladeWidth <= 0f) return;
        BladeWidthPercent = legacyBladeWidth * 100f;
        legacyBladeWidth = 0f;
    }
}
