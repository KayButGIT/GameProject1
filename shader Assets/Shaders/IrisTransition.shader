Shader "UI/IrisTransition"
{
    Properties
    {
        _Color ("Color", Color) = (0, 0, 0, 1)
        _Center ("Center", Vector) = (0.5, 0.5, 0, 0)
        _Radius ("Radius", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float4 _Color;
            float4 _Center;
            float _Radius;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 adjustedUv = input.uv;
                adjustedUv.x *= _ScreenParams.x / _ScreenParams.y;

                float2 adjustedCenter = _Center.xy;
                adjustedCenter.x *= _ScreenParams.x / _ScreenParams.y;

                float distanceFromCenter =
                    distance(adjustedUv, adjustedCenter);

                // Transparent inside the circle, black outside it.
                float alpha = step(_Radius, distanceFromCenter);

                return half4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}