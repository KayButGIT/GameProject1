// Draws a see-through silhouette of a character only where a block hides it.
// XrayVision adds a copy of the character's meshes with this shader while the character walks through breakable blocks.
Shader "Bomberman/Actor Xray"
{
    Properties
    {
        _XrayColor ("Xray Color", Color) = (0.25, 0.95, 1, 1)
        _Intensity ("Intensity", Range(0, 1)) = 0
        _Opacity ("Opacity", Range(0, 1)) = 0.75
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
            "Queue" = "Transparent+20"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Xray"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            // Only pixels that sit behind something already drawn, such as the block around the character.
            ZTest Greater
            Cull Back
            // Bit 1 is set by Xray Occluder over the middle pillars and the border, so nothing is drawn there.
            // Bit 2 is flipped by the silhouette itself, so overlapping parts of the same character do not stack up.
            Stencil
            {
                Ref 0
                ReadMask 3
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
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(position.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                half3 viewDir = normalize(input.viewDirWS);
                half rim = pow(saturate(1.0h - saturate(dot(normal, viewDir))), _RimPower);
                half3 color = _XrayColor.rgb * (0.35h + rim * 1.4h);
                half alpha = saturate(_Opacity * _Intensity * (0.12h + 0.88h * rim));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
