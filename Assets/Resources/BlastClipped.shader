Shader "Bomberman/Blast Clipped"
{
 Properties {
  _BaseMap("Texture", 2D) = "white" {}
  _BaseColor("Color", Color) = (1,1,1,1)
  _DstBlend("Destination Blend", Float) = 1
 }
 SubShader {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
  Pass {
   Blend SrcAlpha [_DstBlend]
   ZWrite Off
   Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
   float4 _BaseColor, _BlastBounds, _BlastOrigin;
   struct Attributes { float4 positionOS:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
   struct Varyings { float4 positionCS:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; };
   Varyings vert(Attributes v) {
    Varyings o;
    o.world = TransformObjectToWorld(v.positionOS.xyz);
    o.positionCS = TransformWorldToHClip(o.world);
    o.color = v.color * _BaseColor; o.uv = v.uv; return o;
   }
   half4 frag(Varyings i):SV_Target {
    float2 p = i.world.xz;
    float2 edgeDistance = min(p - _BlastBounds.xy, _BlastBounds.zw - p);
    float laneDistance = 0.48 - min(abs(p.x - _BlastOrigin.x), abs(p.y - _BlastOrigin.y));
    float boundaryDistance = min(min(edgeDistance.x, edgeDistance.y), laneDistance);
    clip(boundaryDistance);
    float heightDistance = 0.72 - i.world.y;
    clip(heightDistance);
    half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * i.color;
    color.a *= smoothstep(0.0, 0.22, boundaryDistance);
    color.a *= smoothstep(0.0, 0.2, heightDistance);
    return color;
   }
   ENDHLSL
  }
 }
}
