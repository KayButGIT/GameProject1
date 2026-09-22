Shader "Bomberman/Grass Wind"
{
    Properties
    {
        [MainColor] _BaseColor("Root Color", Color) = (0.22, 0.43, 0.015, 1)
        _TipColor("Tip Color", Color) = (0.68, 0.90, 0.055, 1)
        _WindStrength("Wind Strength", Range(0, 0.12)) = 0.045
        _WindSpeed("Wind Speed", Range(0, 5)) = 1.6
        _WindDirection("Wind Direction (World X/Z)", Vector) = (1, 0, 0.4, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _TipColor;
            float _WindStrength;
            float _WindSpeed;
            float4 _WindDirection;
        CBUFFER_END
        struct Attributes
        {
            float3 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            float2 root : TEXCOORD1;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            float2 uv : TEXCOORD2;
            half fog : TEXCOORD3;
            UNITY_VERTEX_OUTPUT_STEREO
        };
        float3 GrassPositionWS(Attributes input)
        {
            float3 rootWS = TransformObjectToWorld(float3(input.root.x, 0.5, input.root.y));
            float phase = dot(rootWS.xz, float2(2.13, 1.67)) + input.uv.x * 3.0;
            float t = _Time.y * _WindSpeed;
            float wave = sin(t + phase) * 0.75 + sin(t * 1.71 + phase * 1.3) * 0.25;
            float3 direction = float3(_WindDirection.x, 0, _WindDirection.z);
            direction /= max(length(direction), 0.0001);
            float3 directionOS = TransformWorldToObjectDir(direction, false);
            directionOS /= max(length(directionOS), 0.0001);
            // Shader clamping matches the conservative bounds baked into the mesh.
            float strength = clamp(_WindStrength, 0, 0.12);
            float bend = input.uv.y * input.uv.y * strength * wave;
            return TransformObjectToWorld(input.positionOS + directionOS * bend);
        }
        Varyings GrassVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionWS = GrassPositionWS(input);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.uv = input.uv;
            output.fog = ComputeFogFactor(output.positionCS.z);
            return output;
        }
        half4 GrassFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
            // Broad, two-sided lighting keeps thin blades readable from the arena camera.
            half diffuse = 0.35h + 0.65h * abs(dot(normalize(input.normalWS), light.direction));
            half3 ambient = max(SampleSH(half3(0, 1, 0)), half3(0.18h, 0.18h, 0.18h));
            half3 color = lerp(_BaseColor.rgb, _TipColor.rgb, smoothstep(0, 1, input.uv.y));
            color *= lerp(0.80h, 1.12h, input.uv.x);
            color *= ambient + light.color * diffuse * lerp(0.4h, 1.0h, light.shadowAttenuation);
            return half4(MixFog(color, input.fog), 1);
        }
        half4 DepthFragment(Varyings input) : SV_Target { return 0; }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex GrassVertex
            #pragma fragment GrassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex GrassShadowVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection;
            float3 _LightPosition;
            Varyings GrassShadowVertex(Attributes input)
            {
                Varyings output = GrassVertex(input);
                float3 lightDirection = _LightDirection;
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    lightDirection = normalize(_LightPosition - output.positionWS);
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(output.positionWS, output.normalWS, lightDirection));
                output.positionCS = ApplyShadowClamping(output.positionCS);
                return output;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex GrassVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex GrassVertex
            #pragma fragment NormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 NormalsFragment(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(normal);
                    return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
                #else
                    return half4(normal, 0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack Off
}
