// Winter "ice glass" for the system UI: a cool vertical gradient, a hairline frosty stroke drawn inside the edge,
// a glassy bevel catching light along the top, frost crystals creeping in from the edges (thickest in the corners)
// and an occasional sheen sweeping across. Blend modes are material properties so the same shader serves the
// translucent panels and the opaque buttons/cards; separate alpha blending keeps Quest passthrough correct.
// Decorations (snow caps, icicles) use it with _Size = 0 and per-vertex colour.
Shader "FrostAndFound/UIGlass"
{
    Properties
    {
        _BaseColor ("Colour (alpha = opacity)", Color) = (0.07, 0.19, 0.35, 0.9)
        _Gradient ("Top/bottom gradient", Range(0, 0.6)) = 0.22
        _Size ("Panel size (m), 0 = no edge effects", Vector) = (0, 0, 0, 0)
        _Radius ("Corner radius (m)", Float) = 0.03
        _Stroke ("Stroke width (m)", Float) = 0.0018
        _StrokeColor ("Stroke colour", Color) = (0.8, 0.94, 1, 0.32)
        _Highlight ("Top bevel highlight", Range(0, 1)) = 0.5
        _Frost ("Frost at the edges", Range(0, 1)) = 0.5
        _FrostMap ("Frost crystals (R)", 2D) = "black" {}
        _FrostTile ("Frost tile size (m)", Float) = 0.2
        _Sheen ("Sheen sweep", Range(0, 1)) = 0.35
        _VertexColor ("Use vertex colour", Float) = 0
        [HideInInspector] _SrcBlend ("Src", Float) = 5
        [HideInInspector] _DstBlend ("Dst", Float) = 10
        [HideInInspector] _SrcBlendA ("SrcA", Float) = 1
        [HideInInspector] _DstBlendA ("DstA", Float) = 10
        [HideInInspector] _ZWrite ("ZWrite", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "UIGlass"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend], [_SrcBlendA] [_DstBlendA]
            ZWrite [_ZWrite]
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_FrostMap); SAMPLER(sampler_FrostMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _StrokeColor;
                float4 _Size;
                float4 _FrostMap_ST;
                half _Gradient;
                float _Radius;
                float _Stroke;
                half _Highlight;
                half _Frost;
                float _FrostTile;
                half _Sheen;
                half _VertexColor;
                half _SrcBlend;
                half _DstBlend;
                half _SrcBlendA;
                half _DstBlendA;
                half _ZWrite;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 p : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.p = v.positionOS.xy;
                o.color = lerp(half4(1, 1, 1, 1), v.color, _VertexColor);
                return o;
            }

            float SdRoundBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 baseCol = _BaseColor * i.color;
                half3 col = baseCol.rgb;
                half a = baseCol.a;
                if (_Size.x > 0.0001)
                {
                    float2 hs = _Size.xy * 0.5;
                    float2 p = i.p;
                    float inside = -SdRoundBox(p, hs, _Radius);            // metres in from the edge
                    half v = saturate(p.y / hs.y * 0.5 + 0.5);              // 0 bottom .. 1 top
                    col *= lerp(1.0h - _Gradient, 1.0h + _Gradient, v);

                    // frost: a thin rime all round the edge, thicker drifts in the corners
                    float2 c = saturate((abs(p) - (hs - 0.12)) / 0.12);
                    half corner = c.x * c.y;
                    half zone = exp(-inside / 0.02) * 0.8h + corner * exp(-inside / 0.07) * 0.9h;
                    half crystals = SAMPLE_TEXTURE2D(_FrostMap, sampler_FrostMap, p / _FrostTile).r;
                    half frost = saturate(zone) * crystals * _Frost;
                    col = lerp(col, half3(0.86h, 0.95h, 1.0h), frost * 0.6h);
                    a = max(a, saturate(frost * 1.2h) * 0.95h);

                    // glassy bevel: a bright hairline just inside the top edge, fading down the sides
                    half bevel = (1.0h - smoothstep(_Stroke, _Stroke + 0.004, inside)) * smoothstep(0.4h, 1.0h, v);
                    col += bevel * _Highlight * 0.32h;

                    // an occasional sheen sweeping diagonally across the glass (offset per panel so they don't sync)
                    float period = 7.0;
                    float ph = frac(_Time.y / period + _Size.x * 3.1 + _Size.y * 1.7) * period / 1.3;
                    float diag = (p.x + p.y * 0.6) / (hs.x + hs.y * 0.6);
                    half band = saturate(1.0 - abs(diag - lerp(-1.4, 1.4, saturate(ph))) / 0.18);
                    col += band * band * _Sheen * 0.09h * step(ph, 1.0);

                    // hairline stroke inside the edge
                    half stroke = 1.0h - smoothstep(_Stroke * 0.45, _Stroke, inside);
                    col = lerp(col, _StrokeColor.rgb, stroke * _StrokeColor.a);
                    a = lerp(a, 1.0h, stroke * _StrokeColor.a);
                }
                return half4(col, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
