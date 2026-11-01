Shader "Bomberman/Player Burn"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _NoiseScale ("Noise Scale", Float) = 7
        _BurnProgress ("Burn Progress", Range(0,1)) = 0
        _EdgeWidth ("Edge Width", Range(0.001,0.3)) = 0.18
        _EdgeColor ("Fire Edge", Color) = (5,0.65,0.035,1)
        _CoreColor ("Hot Edge Core", Color) = (8,3.2,0.35,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _EdgeColor;
                float4 _CoreColor;
                float _NoiseScale;
                float _BurnProgress;
                float _EdgeWidth;
                float _Cutoff;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; float3 positionOS:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.positionOS = input.positionOS.xyz;
                return output;
            }
            float HashNoise(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }
            half4 Frag(Varyings input):SV_Target
            {
                float noise = HashNoise(floor(input.positionOS * _NoiseScale));
                float threshold = saturate(_BurnProgress) * 1.35 - 0.18;
                float normalizedHeight = saturate(input.uv.y);
                float burn = normalizedHeight + (noise - 0.5) * 0.24;
                clip(burn - threshold);
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                clip(color.a - _Cutoff);
                float edgeDistance = max(0.0, burn - threshold);
                float edge = 1.0 - smoothstep(0.0, _EdgeWidth, edgeDistance);
                float hotCore = 1.0 - smoothstep(0.0, _EdgeWidth * 0.28, edgeDistance);
                color.rgb *= 1.0 - edge * 0.35;
                color.rgb += _EdgeColor.rgb * edge * 1.4;
                color.rgb += _CoreColor.rgb * hotCore * 0.85;
                return color;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Simple Lit"
}
