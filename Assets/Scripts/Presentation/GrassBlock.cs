using UnityEngine;

/// <summary>Authoring settings for a saved grass mesh. Wind animation runs entirely in the shader.</summary>
[DisallowMultipleComponent]
public sealed class GrassBlock : MonoBehaviour
{
    [Range(1, 2000)] public int BladeCount = 160;
    [Min(0.01f)] public float MinimumHeight = 0.15f;
    [Min(0.01f)] public float MaximumHeight = 0.30f;
    [Range(0.005f, 0.2f)] public float BladeWidth = 0.065f;
    public int RandomSeed = 87597;
    [HideInInspector] public MeshFilter GrassMesh;
}
