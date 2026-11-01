// Draws a screen-space outer outline for the clipped X-ray silhouette.
Shader "Bomberman/Actor Xray Outline"
{
    Properties
    {
        _XrayColor ("Xray Color", Color) = (0.25, 0.95, 1, 1)
        _Intensity ("Intensity", Range(0, 1)) = 0
        _Opacity ("Opacity", Range(0, 1)) = 0.22
        _OutlinePixels ("Outline Width (Pixels)", Range(1, 8)) = 2
        _OutlineBrightness ("Outline Brightness", Range(0, 3)) = 1
        [HideInInspector] _MainTex ("Unused", 2D) = "white" {}
        [HideInInspector] _Color ("Unused", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+20"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "XrayOutline"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Front
            Stencil
            {
                // Require a visible destructible-block pixel, skip solid blockers
                // and the filled X-ray body, and avoid drawing the outline twice.
                Ref 4
                ReadMask 15
                WriteMask 8
                Comp Equal
                Pass Invert
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _XrayColor;
                half _Intensity;
                half _Opacity;
                half _OutlinePixels;
                half _OutlineBrightness;
                float4 _OverlapBoundsMin[9];
                float4 _OverlapBoundsMax[9];
                float _OverlapBoundsCount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float2 normalVS = TransformWorldToViewDir(normalWS).xy;
                float normalLength = max(length(normalVS), 0.0001);
                float2 direction = normalVS / normalLength;
                output.positionCS = position.positionCS;
                output.positionCS.xy += direction * (2.0 * _OutlinePixels / _ScreenParams.xy) * output.positionCS.w;
                output.positionWS = position.positionWS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float insideOverlap = 0.0;
                [unroll]
                for (int i = 0; i < 9; i++)
                {
                    if (i < _OverlapBoundsCount)
                    {
                        float3 insideMin = step(_OverlapBoundsMin[i].xyz, input.positionWS);
                        float3 insideMax = step(input.positionWS, _OverlapBoundsMax[i].xyz);
                        insideOverlap = max(insideOverlap, insideMin.x * insideMin.y * insideMin.z
                            * insideMax.x * insideMax.y * insideMax.z);
                    }
                }
                clip(insideOverlap - 0.5);
                return half4(_XrayColor.rgb * _OutlineBrightness, saturate(_Opacity * _Intensity));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
