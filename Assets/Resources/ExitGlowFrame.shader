Shader "Bomberman/Exit Glow Frame"
{
    Properties
    {
        _OutlineColor ("Glow Color", Color) = (1, 1, 1, 1)
        _Intensity ("Glow Intensity", Range(0, 4)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+20"
        }

        Pass
        {
            Name "ExitGlowFrame"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            ZTest LEqual
            Blend One One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return half4(_OutlineColor.rgb * (_Intensity * _OutlineColor.a), 0.0h);
            }
            ENDHLSL
        }
    }
}
