Shader "VFX/Slime_Lit_Matcap_Triplanar_Top"
{
    Properties
    {
        [Header(Blend)]
        [Toggle] _ObjectSpace ("World (off) or Vertex Object (on)", Float) = 0
        _TopFalloffErosion    ("Top Falloff Erosion", Range(-1, 1)) = -0.51
        _TopFalloffSmoothness ("Top Falloff Smoothness", Range(0.01, 2)) = 1
        _TopOffset            ("Top Offset", Range(-1, 1)) = 0.04
        _TopEdgeNoise         ("Top Edge Noise", Range(0, 1)) = 0.25

        [Header(Colors)]
        _ColorDeep ("Deep Color (core)", Color) = (0.08, 0.38, 0.85, 1)
        _ColorBase ("Base Color (sides)", Color) = (0.30, 0.75, 1.00, 1)
        _ColorTop  ("Top Color", Color) = (0.65, 0.92, 1.00, 1)

        [Header(General)]
        _EmissionColor        ("Emission Color", Color) = (0.25, 0.65, 1.00, 1)
        _EmissionMultiply     ("Emission Multiply", Range(0, 5)) = 1
        _EmissionMultiplyBase ("Emission Multiply Base", Range(0, 2)) = 0
        _Spec                 ("Spec", Range(0, 4)) = 0.6
        _Smooth               ("Smooth", Range(0, 1)) = 0.85
        _OverallTilesMultiply ("Overall Tiles Multiply", Range(0.01, 4)) = 0.3
        _Opacity              ("Opacity", Range(0, 1)) = 0.85
        _Translucency         ("Translucency", Range(0, 2)) = 0.6
        [Toggle] _ReceiveShadows ("Receive Shadows", Float) = 1

        [Header(Base Texture Optional)]
        _BaseTex ("Base Texture", 2D) = "white" {}
        _BaseNormalTex ("Base Normal Texture", 2D) = "bump" {}
        _BaseNormalIntensity ("Base Normal Intensity", Range(0, 3)) = 1

        [Header(Procedural Normal Wiggle)]
        _WiggleNormalStrength ("Wiggle Normal Strength", Range(0, 4)) = 1.0
        _WiggleScale ("Wiggle Scale", Range(0.5, 40)) = 12
        _WiggleSpeed ("Wiggle Speed", Range(0, 10)) = 1.5

        [Header(Matcap)]
        [Toggle(_MATCAP_TEX)] _UseMatcapTex ("Use Matcap Texture (off = procedural)", Float) = 0
        _MatcapTex ("Matcap", 2D) = "white" {}
        _MatcapBlend ("Matcap Blend", Range(0, 1)) = 0.8
        _MatcapDesaturate ("Matcap Desaturate", Range(0, 1)) = 0
        _MatcapTint ("Matcap Tint", Color) = (1, 1, 1, 1)
        _HueShift ("Hue Shift", Range(0, 1)) = 0
        _HueShiftSpeed ("Hue Shift Speed", Range(-2, 2)) = 0

        [Header(Fresnel)]
        _FresnelColor ("Fresnel Color", Color) = (0.85, 0.97, 1.00, 1)
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 3
        _FresnelIntensity ("Fresnel Intensity", Range(0, 4)) = 1

        [Header(Shape)]
        _Roundness   ("Roundness (cube to blob)", Range(0, 1)) = 0
        _RoundRadius ("Round Radius", Range(0.05, 2)) = 0.5

        [Header(Vertex Wave and Wiggle)]
        [Toggle(_VERTEX_WIGGLE)] _VertexWiggle ("Enable Vertex Wave / Wiggle", Float) = 1
        _WaveAmplitude ("Wave Amplitude", Range(0, 0.3)) = 0.04
        _WaveFrequency ("Wave Frequency", Range(0, 30)) = 6
        _WaveSpeed     ("Wave Speed", Range(0, 10)) = 3
        _VertexStrength ("Noise Wiggle Strength", Range(0, 0.5)) = 0.03
        _VertexScale    ("Noise Wiggle Scale", Range(0.1, 10)) = 2
        _VertexSpeed    ("Noise Wiggle Speed", Range(0, 10)) = 1.5

        [Header(Rendering)]
        [Toggle] _PrepassZWrite ("Depth Prepass (clean self sorting)", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float  _ObjectSpace;
            float  _TopFalloffErosion;
            float  _TopFalloffSmoothness;
            float  _TopOffset;
            float  _TopEdgeNoise;
            half4  _ColorDeep;
            half4  _ColorBase;
            half4  _ColorTop;
            half4  _EmissionColor;
            half   _EmissionMultiply;
            half   _EmissionMultiplyBase;
            half   _Spec;
            half   _Smooth;
            float  _OverallTilesMultiply;
            half   _Opacity;
            half   _Translucency;
            half   _ReceiveShadows;
            float4 _BaseTex_ST;
            float4 _BaseNormalTex_ST;
            half   _BaseNormalIntensity;
            float  _WiggleNormalStrength;
            float  _WiggleScale;
            float  _WiggleSpeed;
            half   _MatcapBlend;
            half   _MatcapDesaturate;
            half4  _MatcapTint;
            float  _HueShift;
            float  _HueShiftSpeed;
            half4  _FresnelColor;
            half   _FresnelPower;
            half   _FresnelIntensity;
            float  _VertexStrength;
            float  _VertexScale;
            float  _VertexSpeed;
            float  _WaveAmplitude;
            float  _WaveFrequency;
            float  _WaveSpeed;
            float  _Roundness;
            float  _RoundRadius;
        CBUFFER_END

        TEXTURE2D(_BaseTex);       SAMPLER(sampler_BaseTex);
        TEXTURE2D(_BaseNormalTex); SAMPLER(sampler_BaseNormalTex);
        TEXTURE2D(_MatcapTex);     SAMPLER(sampler_MatcapTex);

        // ---------- 3D value noise ----------
        float hash31(float3 p)
        {
            p = frac(p * 0.1031);
            p += dot(p, p.zyx + 31.32);
            return frac((p.x + p.y) * p.z);
        }

        float noise3(float3 p)
        {
            float3 i = floor(p);
            float3 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float n000 = hash31(i + float3(0,0,0));
            float n100 = hash31(i + float3(1,0,0));
            float n010 = hash31(i + float3(0,1,0));
            float n110 = hash31(i + float3(1,1,0));
            float n001 = hash31(i + float3(0,0,1));
            float n101 = hash31(i + float3(1,0,1));
            float n011 = hash31(i + float3(0,1,1));
            float n111 = hash31(i + float3(1,1,1));
            return lerp(lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y),
                        lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y), f.z);
        }

        // Round a subdivided cube into a blob (0 = cube, 1 = sphere).
        // Uses position only, so duplicated hard-edge vertices stay joined.
        float3 RoundOS(float3 posOS)
        {
            return lerp(posOS, normalize(posOS + 1e-5) * _RoundRadius, _Roundness);
        }

        // Vertex wave + noise wiggle. Vertices move away from the pivot (not along face normals),
        // so duplicated vertices at cube edges move together and never split.
        float3 DisplaceOS(float3 posOS)
        {
            posOS = RoundOS(posOS);
        #if defined(_VERTEX_WIGGLE)
            float3 dir = normalize(posOS + 1e-5);

            // noise wiggle
            float tn = _Time.y * _VertexSpeed;
            float n1 = noise3(posOS * _VertexScale + float3(0, tn, 0));
            float n2 = noise3(posOS * _VertexScale * 2.1 + float3(tn * 0.7, 0, -tn));
            float n  = (n1 * 0.65 + n2 * 0.35) * 2.0 - 1.0;

            // sine waves (three directions so it doesn't look mechanical)
            float tw = _Time.y * _WaveSpeed;
            float wv = sin(posOS.y * _WaveFrequency + tw) * 0.5
                     + sin(posOS.x * _WaveFrequency * 1.3 + tw * 1.2 + 1.7) * 0.3
                     + sin(posOS.z * _WaveFrequency * 0.9 - tw * 0.8 + 3.1) * 0.2;

            posOS += dir * (n * _VertexStrength + wv * _WaveAmplitude);
        #endif
            return posOS;
        }
        ENDHLSL

        // ---------- Depth prepass: slime sorts against itself ----------
        Pass
        {
            Name "DepthPrepass"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZWrite [_PrepassZWrite]
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma shader_feature_local _VERTEX_WIGGLE

            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; };

            V vertDepth(A i)
            {
                V o;
                o.positionCS = TransformObjectToHClip(DisplaceOS(i.positionOS.xyz));
                return o;
            }
            half4 fragDepth(V i) : SV_Target { return 0; }
            ENDHLSL
        }

        // ---------- Main lit pass ----------
        Pass
        {
            Name "SlimeForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _VERTEX_WIGGLE
            #pragma shader_feature_local _MATCAP_TEX
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float3 normalOS   : TEXCOORD3;
                half   fogFactor  : TEXCOORD4;
            };

            float3 HueShift(float3 c, float a)
            {
                const float3 k = 0.57735026;
                float ca = cos(a), sa = sin(a);
                return c * ca + cross(k, c) * sa + k * dot(k, c) * (1.0 - ca);
            }

            // Procedural jelly matcap (same math as Slime_Matcap_SkyBlue.png), built from the material colors
            half3 ProcMatcap(float3 vn)
            {
                float3 L1 = normalize(float3(-0.45, 0.60, 0.66));   // key light, top-left
                float3 L2 = normalize(float3( 0.60, -0.50, 0.62));  // soft bounce, bottom-right
                float3 Vv = float3(0, 0, 1);
                float d = dot(vn, L1);
                half3 c = lerp(_ColorDeep.rgb, _ColorBase.rgb, smoothstep(-0.35, 0.85, d));
                c += _ColorTop.rgb * pow(saturate(dot(vn, L2)), 3.0) * 0.25;
                c = lerp(c, _FresnelColor.rgb, pow(1.0 - saturate(vn.z), 2.5) * 0.55);
                float3 H1 = normalize(L1 + Vv);
                float3 H2 = normalize(L2 + Vv);
                c += pow(saturate(dot(vn, H1)), 28.0)  * 0.55;
                c += pow(saturate(dot(vn, H1)), 220.0) * 1.6;
                c += pow(saturate(dot(vn, H2)), 60.0)  * 0.35;
                return c;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 posOS = DisplaceOS(IN.positionOS.xyz);
                VertexPositionInputs vp = GetVertexPositionInputs(posOS);
                OUT.positionCS = vp.positionCS;
                OUT.positionWS = vp.positionWS;
                OUT.positionOS = IN.positionOS.xyz;
                float3 nOS = normalize(lerp(IN.normalOS, normalize(IN.positionOS.xyz + 1e-5), _Roundness));
                OUT.normalWS   = TransformObjectToWorldNormal(nOS);
                OUT.normalOS   = nOS;
                OUT.fogFactor  = ComputeFogFactor(vp.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 V    = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float  os   = _ObjectSpace;
                float  tile = _OverallTilesMultiply;
                float  t    = _Time.y * _WiggleSpeed;

                // Mapping space: world (off) or object (on)
                float3 pS = lerp(IN.positionWS, IN.positionOS, os);
                float3 nS = normalize(lerp(normalize(IN.normalWS), normalize(IN.normalOS), os));

                // ---- Top blend mask ----
                float edgeNoise = (noise3(pS * tile * 6.0 + float3(0, t * 0.3, 0)) - 0.5) * _TopEdgeNoise;
                float top = smoothstep(_TopFalloffErosion,
                                       _TopFalloffErosion + _TopFalloffSmoothness,
                                       nS.y + _TopOffset + edgeNoise);

                // ---- Triplanar weights ----
                float3 w = pow(abs(nS), 4.0);
                w /= (w.x + w.y + w.z + 1e-5);

                // ---- Triplanar base texture ----
                float2 bx = pS.zy * _BaseTex_ST.xy * tile + _BaseTex_ST.zw;
                float2 by = pS.xz * _BaseTex_ST.xy * tile + _BaseTex_ST.zw;
                float2 bz = pS.xy * _BaseTex_ST.xy * tile + _BaseTex_ST.zw;
                half3 texCol = SAMPLE_TEXTURE2D(_BaseTex, sampler_BaseTex, bx).rgb * w.x
                             + SAMPLE_TEXTURE2D(_BaseTex, sampler_BaseTex, by).rgb * w.y
                             + SAMPLE_TEXTURE2D(_BaseTex, sampler_BaseTex, bz).rgb * w.z;

                // ---- Triplanar normal texture (UDN blend) ----
                float2 nx = pS.zy * _BaseNormalTex_ST.xy * tile + _BaseNormalTex_ST.zw;
                float2 ny = pS.xz * _BaseNormalTex_ST.xy * tile + _BaseNormalTex_ST.zw;
                float2 nz = pS.xy * _BaseNormalTex_ST.xy * tile + _BaseNormalTex_ST.zw;
                float3 tx = UnpackNormalScale(SAMPLE_TEXTURE2D(_BaseNormalTex, sampler_BaseNormalTex, nx), _BaseNormalIntensity);
                float3 ty = UnpackNormalScale(SAMPLE_TEXTURE2D(_BaseNormalTex, sampler_BaseNormalTex, ny), _BaseNormalIntensity);
                float3 tz = UnpackNormalScale(SAMPLE_TEXTURE2D(_BaseNormalTex, sampler_BaseNormalTex, nz), _BaseNormalIntensity);
                tx = float3(tx.xy + nS.zy, nS.x);
                ty = float3(ty.xy + nS.xz, nS.y);
                tz = float3(tz.xy + nS.xy, nS.z);
                float3 nTex = normalize(tx.zyx * w.x + ty.xzy * w.y + tz.xyz * w.z);

                // ---- Procedural animated normal wiggle (two noise layers) ----
                const float e = 0.12;
                float3 grad = 0;
                {
                    float3 p = pS * _WiggleScale * tile + float3(0, -t, t * 0.5);
                    float c = noise3(p);
                    grad += float3(noise3(p + float3(e,0,0)) - c,
                                   noise3(p + float3(0,e,0)) - c,
                                   noise3(p + float3(0,0,e)) - c) / e;
                }
                {
                    float3 p = pS * _WiggleScale * tile * 2.3 + float3(t * 0.8, t * 0.4, -t);
                    float c = noise3(p);
                    grad += 0.5 * float3(noise3(p + float3(e,0,0)) - c,
                                         noise3(p + float3(0,e,0)) - c,
                                         noise3(p + float3(0,0,e)) - c) / e;
                }
                grad -= nTex * dot(grad, nTex);
                float3 nFinalS = normalize(nTex - grad * _WiggleNormalStrength * 0.25);
                float3 N = normalize(lerp(nFinalS, TransformObjectToWorldNormal(nFinalS), os));

                // ---- Lighting ----
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half atten = lerp(1.0h, mainLight.shadowAttenuation, _ReceiveShadows) * mainLight.distanceAttenuation;

                float NdotV = saturate(dot(N, V));
                float NdotL = dot(N, mainLight.direction) * 0.5 + 0.5;
                float fres  = pow(1.0 - NdotV, _FresnelPower);

                half3 sideCol = lerp(_ColorBase.rgb, _ColorTop.rgb, top);
                half3 body = lerp(_ColorDeep.rgb, sideCol, saturate(1.0 - NdotV * 0.6 + NdotL * 0.3)) * texCol;

                half3 lit = body * (mainLight.color * NdotL * atten + SampleSH(N) * 0.6);

                // Translucency (light passing through the goo)
                float3 Lt = normalize(mainLight.direction + N * 0.5);
                float sss = pow(saturate(dot(V, -Lt)), 3.0) * _Translucency;
                lit += sideCol * mainLight.color * sss * atten;

                // Specular (top is glossier)
                float3 H = normalize(mainLight.direction + V);
                float expo = exp2(lerp(2.0, 11.0, _Smooth));
                float specTerm = pow(saturate(dot(N, H)), expo) * _Spec * (1.0 + top);
                lit += mainLight.color * specTerm * atten;

                // ---- Matcap ----
                float3 vn = TransformWorldToViewDir(N, true);
            #if defined(_MATCAP_TEX)
                half3 mc = SAMPLE_TEXTURE2D(_MatcapTex, sampler_MatcapTex, vn.xy * 0.5 + 0.5).rgb;
            #else
                half3 mc = ProcMatcap(vn);
            #endif
                mc = lerp(mc, dot(mc, half3(0.299, 0.587, 0.114)).xxx, _MatcapDesaturate);
                mc *= _MatcapTint.rgb;
                mc = HueShift(mc, (_HueShift + _Time.y * _HueShiftSpeed) * 6.2831853);
                mc *= (0.4 + 0.6 * atten);

                half3 col = lerp(lit, mc, _MatcapBlend);

                // ---- Emission (glowing veins) ----
                float vein = smoothstep(0.55, 0.95, noise3(pS * _WiggleScale * tile * 0.6 + float3(t * 0.2, 0, t * 0.3)));
                col += _EmissionColor.rgb * (_EmissionMultiplyBase + _EmissionMultiply * vein);

                // ---- Fresnel ----
                col += _FresnelColor.rgb * fres * _FresnelIntensity;

                half alpha = saturate(lerp(_Opacity, 1.0, saturate(fres)) + specTerm * 0.5);

                col = MixFog(col, IN.fogFactor);
                return half4(col, alpha);
            }
            ENDHLSL
        }

        // ---------- Shadow caster (blob shadow like the reference) ----------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma shader_feature_local _VERTEX_WIGGLE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            float3 _LightDirection;
            float3 _LightPosition;

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; };

            V vertShadow(A i)
            {
                V o;
                float3 posWS = TransformObjectToWorld(DisplaceOS(i.positionOS.xyz));
                float3 nWS   = TransformObjectToWorldNormal(i.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - posWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                float4 pCS = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, lightDir));
            #if UNITY_REVERSED_Z
                pCS.z = min(pCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                pCS.z = max(pCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                o.positionCS = pCS;
                return o;
            }
            half4 fragShadow(V i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
