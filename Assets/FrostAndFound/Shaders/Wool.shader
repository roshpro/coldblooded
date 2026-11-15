// Knitted wool for the player's gloves. Colour comes from vertex colours; vertex alpha picks the stitch:
// 1 = stocking stitch (rows of little V's), 0 = ribbing (vertical ribs, for the cuff).
// The knit doesn't come from UVs: UV1 is each vertex's position on the open, resting glove (metres, fingers +Z)
// and UV2 its normal there. The stitches are laid out in that rest space at a fixed size and projected from the
// three axes (blended by the rest normal), so they ride on the surface like real yarn: curling, spreading or a
// wide knuckle never stretch or squash them, and there are no seams where the parts meet.
// Soft wrapped lighting and a fuzzy rim make the edges read as soft yarn rather than a hard outline.
Shader "FrostAndFound/Wool"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _RowsPerMeter ("Knit Rows Per Metre", Float) = 150
        _ColsPerMeter ("Knit Columns Per Metre", Float) = 135
        _StitchContrast ("Stitch Contrast", Range(0,1)) = 0.45
        _FuzzColor ("Fuzz Color", Color) = (1,0.95,0.92,1)
        _FuzzStrength ("Fuzz Strength", Range(0,1)) = 0.35
        _Smoothness ("Smoothness", Range(0,1)) = 0.05
        _Inflate ("Inflate (m, puffs a bare-hand mesh out into a glove)", Range(0,0.01)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _Tint;
            float _RowsPerMeter;
            float _ColsPerMeter;
            float _StitchContrast;
            float4 _FuzzColor;
            float _FuzzStrength;
            float _Smoothness;
            float _Inflate;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Wool"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float3 rest : TEXCOORD1;      // position on the resting glove
                float3 restN : TEXCOORD2;     // normal there
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 rest : TEXCOORD2;
                float3 restN : TEXCOORD3;
                float4 color : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // 0..1 brightness of the yarn; g is in stitches (x across columns, y up rows)
            float knit(float2 g, float stocking)
            {
                float px = frac(g.x) - 0.5;   // -0.5..0.5 across a column
                float py = frac(g.y);         // 0..1 up a row

                // stocking stitch: each cell is a "V" of two slanted yarn loops
                float leg = 0.06 + 0.36 * py;
                float d = abs(abs(px) - leg);
                float loop = saturate(1.0 - d / 0.2);
                loop *= smoothstep(0.0, 0.18, py) * smoothstep(1.0, 0.82, py); // soft gap between rows

                // ribbing: soft vertical ribs with faint rows
                float rib = pow(abs(cos(px * 3.14159)), 0.6) * (0.85 + 0.15 * smoothstep(0.0, 0.3, py));

                float s = lerp(rib, loop, stocking);
                float grain = hash21(floor(g * 7.0)) * 0.12; // stray fibres
                // far away (or seen edge-on) a stitch is smaller than a pixel: fade to the average instead of shimmering
                float2 w = fwidth(g);
                float fade = saturate(1.6 - 2.2 * max(w.x, w.y));
                return lerp(0.62, saturate(s + grain), fade);
            }

            // the knit projected from the rest glove's three axes. Rows always run along the hand (+Z) on the
            // top, bottom and sides, which is how a glove is knitted; the fingertips and the cuff's end get a
            // view down the fingers. Blending is tight so a stitch is rarely seen doubled.
            float knit3(float3 p, float3 n, float stocking)
            {
                float3 w = pow(abs(n), 6.0);
                w /= max(w.x + w.y + w.z, 1e-5);
                float cols = _ColsPerMeter, rows = _RowsPerMeter;
                // all three always run: the stitch fade takes screen derivatives, which need uniform control flow
                return w.y * knit(float2(p.x * cols, p.z * rows), stocking)            // back / palm
                     + w.x * knit(float2(p.y * cols + 0.37, p.z * rows), stocking)     // sides
                     + w.z * knit(float2(p.x * cols + 0.21, p.y * rows), stocking);    // tips / ends
            }

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz) + normalize(o.normalWS) * _Inflate;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.positionWS = positionWS;
                o.rest = input.rest;
                o.restN = input.restN;
                o.color = input.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float3 n = normalize(i.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);

                float k = knit3(i.rest, i.restN, step(0.5, i.color.a));
                float3 albedo = i.color.rgb * _Tint.rgb * lerp(1.0 - _StitchContrast, 1.0, k);

                // soft wrapped sun + sky: wool scatters light, so the shadow side stays warm and never goes black
                Light l = GetMainLight();
                half wrap = saturate((dot(n, l.direction) + 0.5) / 1.5);
                half3 lc = l.color;
                half lum = max(max(lc.r, lc.g), max(lc.b, 0.001));
                half3 lightTint = lerp(half3(1, 1, 1), lc / lum, 0.4);
                half3 col = albedo * (0.42 + 0.62 * wrap * wrap) * lightTint;
                col += albedo * SampleSH(n) * 0.35;
                col *= lerp(0.8, 1.0, k); // stitch gaps stay a little shadowed

                // wool fuzz: light catches loose fibres at grazing angles
                float rim = pow(1.0 - saturate(dot(n, v)), 2.5);
                col += _FuzzColor.rgb * i.color.rgb * rim * _FuzzStrength;
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; };

            V ShadowVert(A input)
            {
                V o = (V)0;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz) + normalize(normalWS) * _Inflate;
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                o.positionCS = positionCS;
                return o;
            }

            half4 ShadowFrag(V input) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    Fallback Off
}
