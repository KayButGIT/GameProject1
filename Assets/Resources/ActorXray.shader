// Draws a see-through silhouette of a character only where a block hides it.
// XrayVision adds a copy of the character's meshes with this shader while the character walks through breakable blocks.
Shader "Bomberman/Actor Xray"
{
    Properties
    {
        _XrayColor ("Xray Color", Color) = (0.25, 0.95, 1, 1)
        _Intensity ("Intensity", Range(0, 1)) = 0
        _Opacity ("Opacity", Range(0, 1)) = 1
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        // Not drawn. Death effects read these two from every material on a character, so they must exist.
        [HideInInspector] _MainTex ("Unused", 2D) = "white" {}
        [HideInInspector] _Color ("Unused", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+19"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Xray"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            // The silhouette marks only the actor part inside a destructible block volume,
            // and the stencil mask restricts it to visible destructible-block pixels.
            // Drawing over that block surface also works when the actor is on the camera-facing side.
            ZTest Always
            Cull Back
            // Bit 1 is set by Xray Occluder over the middle pillars and the border, so nothing is drawn there.
            // Bit 2 is flipped by the silhouette itself, so overlapping parts of the same character do not stack up.
            Stencil
            {
                // Only show fragments over visible destructible-block pixels (bit 4),
                // skip solid occluders (bit 1), and avoid blending the same actor twice (bit 2).
                Ref 4
                ReadMask 7
                WriteMask 2
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
                half _RimPower;
                half4 _Color;
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
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(position.positionWS);
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

                half3 normal = normalize(input.normalWS);
                half3 viewDir = normalize(input.viewDirWS);
                half rim = pow(saturate(1.0h - saturate(dot(normal, viewDir))), _RimPower);
                half3 color = _XrayColor.rgb;
                half alpha = saturate(_Opacity * _Intensity * (0.2h + 0.8h * rim));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
