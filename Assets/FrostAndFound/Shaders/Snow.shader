// The snow layer laid over the detected floor. A grid mesh spanning the room bounds is displaced by
//   _MaskMap  R = inside-the-room alpha (soft edge), G = drift (snow piles up against walls)
//   _TrailMap R = depression (footprints, scoop divots, snowball craters), written by SnowSurface on the CPU
// plus soft procedural dunes. Lighting is soft and painterly rather than banded: sunlit snow is a warm white,
// shade and cast shadows are a luminous sky blue (never grey), prints get cool soft occlusion, grazing angles
// glow and glints sparkle. Edges fade to alpha 0 so the passthrough floor shows through; alpha blending writes
// destination alpha correctly for Meta passthrough (separate alpha blend).
Shader "FrostAndFound/Snow"
{
    Properties
    {
        _SnowColor ("Snow (sunlit)", Color) = (0.957,0.973,1,1)
        _ShadeColor ("Snow Shadow", Color) = (0.576,0.706,0.918,1)
        _DeepColor ("Footprint", Color) = (0.486,0.62,0.859,1)
        _MaskMap ("Room Mask", 2D) = "black" {}
        _TrailMap ("Trail Map", 2D) = "black" {}
        _Thickness ("Snow Thickness (m)", Float) = 0.035
        _DriftHeight ("Drift Height (m)", Float) = 0.07
        _DuneHeight ("Dune Height (m)", Float) = 0.022
        _MoundHeight ("Mound Height (m)", Float) = 0.035
        _HollowTint ("Hollow Tint", Color) = (0.8,0.86,0.95,1)
        _GrainStrength ("Grain", Range(0,0.3)) = 0.07
        _EnvironmentDepthBias ("Environment Depth Bias", Float) = 0.06
        _Sparkle ("Sparkle", Range(0,1)) = 0.65
        _SparkleScale ("Sparkle Density", Float) = 140
        _TexelSize ("Trail Texel (uv)", Float) = 0.002
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-100" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_instancing
            // Meta Depth API: real things in front of the snow (your feet, furniture) hide it
            #pragma multi_compile _ HARD_OCCLUSION SOFT_OCCLUSION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.meta.xr.sdk.core/Shaders/EnvironmentDepth/URP/EnvironmentOcclusionURP.hlsl"

            TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);
            TEXTURE2D(_TrailMap); SAMPLER(sampler_TrailMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _SnowColor;
                half4 _ShadeColor;
                half4 _DeepColor;
                float _Thickness;
                float _DriftHeight;
                float _DuneHeight;
                float _MoundHeight;
                half4 _HollowTint;
                half _GrainStrength;
                float _EnvironmentDepthBias;
                half _Sparkle;
                float _SparkleScale;
                float _TexelSize;
                float4 _MaskMap_ST;
                float4 _TrailMap_ST;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                float2 mpu : TEXCOORD3;          // metres per uv (x, z)
                half2 vary : TEXCOORD4;          // x = piled-up snow (mounds, drifts) 0..1, y = dune noise 0..1
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float HeightAt(float2 uv)
            {
                float3 m = SAMPLE_TEXTURE2D_LOD(_MaskMap, sampler_MaskMap, uv, 0).rgb;
                float t = SAMPLE_TEXTURE2D_LOD(_TrailMap, sampler_TrailMap, uv, 0).r;
                float h = _Thickness * smoothstep(0.0, 0.6, m.r) + m.g * _DriftHeight + m.b * _MoundHeight;
                return h - t * _Thickness * 0.85;
            }

            float Hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }

            float VNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i), b = Hash21(i + float2(1, 0)), c = Hash21(i + float2(0, 1)), d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // soft wind-blown dunes and lumps (metres)
            float Dunes(float2 xz)
            {
                return ((VNoise(xz * 1.7) - 0.5) + (VNoise(xz * 4.6 + 7.1) - 0.5) * 0.4) * _DuneHeight;
            }

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float2 uv = input.uv;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 mk = SAMPLE_TEXTURE2D_LOD(_MaskMap, sampler_MaskMap, uv, 0).rgb;
                float inside = smoothstep(0.0, 0.6, mk.r);
                float2 xz = posWS.xz;
                o.vary = half2(saturate(mk.b + mk.g * 0.6), VNoise(xz * 1.7) * 0.65 + VNoise(xz * 4.6 + 7.1) * 0.35);
                posWS.y += HeightAt(uv) + Dunes(xz) * inside;

                // normal from the height field (texture part in uv steps, dunes in world steps)
                float3 scale = float3(length(UNITY_MATRIX_M._m00_m10_m20), 1, length(UNITY_MATRIX_M._m02_m12_m22));
                float e = _TexelSize * 2.0;
                float hx = HeightAt(uv + float2(e, 0)) - HeightAt(uv - float2(e, 0));
                float hz = HeightAt(uv + float2(0, e)) - HeightAt(uv - float2(0, e));
                const float w = 0.03;
                float dx = (Dunes(xz + float2(w, 0)) - Dunes(xz - float2(w, 0))) * inside;
                float dz = (Dunes(xz + float2(0, w)) - Dunes(xz - float2(0, w))) * inside;
                float3 n = normalize(float3(-hx / (2.0 * e * scale.x) - dx / (2.0 * w), 1.0, -hz / (2.0 * e * scale.z) - dz / (2.0 * w)));

                o.positionWS = posWS;
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS = n;
                o.uv = uv;
                o.mpu = scale.xz;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half2 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, input.uv).rg;
                half trail = SAMPLE_TEXTURE2D(_TrailMap, sampler_TrailMap, input.uv).r;
                half3 N = normalize(input.normalWS);

                // crisp print walls: per-pixel slope of the trail map (the vertex grid is too coarse for them)
                float2 tx = float2(_TexelSize * 2.0, 0), tz = float2(0, _TexelSize * 2.0);   // 2-texel taps: smooth, not blocky
                float gx = SAMPLE_TEXTURE2D(_TrailMap, sampler_TrailMap, input.uv + tx).r - SAMPLE_TEXTURE2D(_TrailMap, sampler_TrailMap, input.uv - tx).r;
                float gz = SAMPLE_TEXTURE2D(_TrailMap, sampler_TrailMap, input.uv + tz).r - SAMPLE_TEXTURE2D(_TrailMap, sampler_TrailMap, input.uv - tz).r;
                float2 g = clamp(float2(gx, gz) * (_Thickness * 0.85) / (4.0 * _TexelSize * max(input.mpu, 0.01)), -2.0, 2.0);
                N = normalize(N + half3(g.x, 0, g.y));
                half3 V = normalize(GetWorldSpaceViewDir(input.positionWS));

            #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
            #else
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            #endif
                Light l = GetMainLight(shadowCoord);

                // soft wrapped sun, cast shadows fall to luminous blue
                half wrap = saturate((dot(N, l.direction) + 0.4h) / 1.4h);
                half sun = wrap * wrap * (3.0h - 2.0h * wrap);
                half lit = sun * lerp(1.0h, l.shadowAttenuation, 0.92h);
                half3 lc = l.color;
                half lum = max(max(lc.r, lc.g), max(lc.b, 0.001h));
                half3 sunCol = _SnowColor.rgb * lerp(half3(1, 1, 1), lc / lum, 0.4h);
                half3 col = lerp(_ShadeColor.rgb, sunCol, lit);

                // body: mound tops and drifts catch the light, hollows stay cool, soft blotches and a fine grain
                // keep it from reading as one flat sheet
                float2 wxz = input.positionWS.xz;
                half blotch = VNoise(wxz * 9.0 + 50.0);
                half bright = saturate(0.42h + input.vary.x * 0.7h + (input.vary.y - 0.5h) * 0.55h + (blotch - 0.5h) * 0.3h);
                col *= lerp(_HollowTint.rgb, half3(1, 1, 1), bright);
                half grain = VNoise(wxz * 35.0) * 0.6h + VNoise(wxz * 94.5) * 0.4h;
                col *= 1.0h - _GrainStrength + grain * _GrainStrength;

                // prints and divots: cool soft occlusion at the bottom
                col = lerp(col, _DeepColor.rgb, saturate(trail * 1.2h) * (0.6h - 0.3h * lit));
                // sky bounce keeps the shade alive, grazing angles glow (forward scattering)
                col += SampleSH(N) * 0.06h;
                half fres = 1.0h - saturate(dot(N, V));
                col += fres * fres * fres * fres * 0.08h;

                // view-dependent glints, brightest in the sun but still twinkling in the shade
                float3 cell = floor(input.positionWS * _SparkleScale);
                float h = frac(sin(dot(cell, float3(12.9898, 78.233, 37.719))) * 43758.5453);
                float h2 = frac(sin(dot(cell, float3(39.346, 11.135, 83.155))) * 24634.6345);
                float3 facing = normalize(float3(h - 0.5, 0.8, h2 - 0.5));
                half glint = step(1.0 - 0.08 * _Sparkle, h) * pow(saturate(dot(reflect(-V, N), facing)), 40.0);
                glint *= 0.6 + 0.4 * sin(_Time.y * (3.0 + h2 * 5.0) + h * 30.0);
                col += glint * 1.7h * lerp(0.35h, 1.0h, lit) * (1.0h - trail);

                half alpha = smoothstep(0.02h, 0.55h, mask.r);
                alpha *= META_DEPTH_GET_OCCLUSION_VALUE_WORLDPOS(input.positionWS, _EnvironmentDepthBias);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
