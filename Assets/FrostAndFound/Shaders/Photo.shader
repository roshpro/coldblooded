// The developed Polaroid picture: photo texture, warm film grade, soft vignette and the develop flash.
Shader "FrostAndFound/Photo"
{
    Properties
    {
        _BaseMap ("Photo", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _Flash ("Develop Flash", Range(0,1)) = 0
        _Vignette ("Vignette", Range(0,1)) = 0.45
        _Grain ("Grain", Range(0,0.2)) = 0.04
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Photo"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; half4 _BaseColor; half _Flash; half _Vignette; half _Grain;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A i)
            {
                V o = (V)0;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                return o;
            }
            half4 Frag(V i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                // instant-film grade: lifted blacks, warm mids
                c = lerp(half3(0.06, 0.05, 0.08), half3(1, 0.98, 0.93), c);
                float2 d = i.uv - 0.5;
                half vig = 1.0h - _Vignette * smoothstep(0.25, 0.75, length(d) * 1.2);
                c *= vig;
                float n = frac(sin(dot(i.uv * 913.7, float2(12.9898, 78.233))) * 43758.5453);
                c += (n - 0.5) * _Grain;
                c = lerp(c, half3(1, 1, 1), _Flash);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
