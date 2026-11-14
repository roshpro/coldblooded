// Vertical gradient dome. Used as the desktop sky and as the snowy backdrop only the Polaroid sees in MR
// (the virtual camera can't see the real room, so photos get a dreamy winter backdrop instead of black).
Shader "FrostAndFound/Sky"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.62,0.71,0.91,1)
        _HorizonColor ("Horizon", Color) = (0.91,0.93,0.98,1)
        _BottomColor ("Bottom", Color) = (0.79,0.83,0.93,1)
        _Flakes ("Painted Flakes", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Background" "Queue" = "Geometry+500" "RenderPipeline" = "UniversalPipeline" "PreviewType" = "Skybox" }
        Pass
        {
            Name "Sky"
            Tags { "LightMode" = "UniversalForward" }
            Cull Front
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor; half4 _HorizonColor; half4 _BottomColor; half _Flakes;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A i)
            {
                V o = (V)0;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.dir = normalize(i.positionOS.xyz);
                return o;
            }
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            half4 Frag(V i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float y = i.dir.y;
                half3 c = y > 0 ? lerp(_HorizonColor.rgb, _TopColor.rgb, smoothstep(0.0, 0.7, y))
                                : lerp(_HorizonColor.rgb, _BottomColor.rgb, smoothstep(0.0, 0.4, -y));
                // soft painted flakes
                float2 uv = float2(atan2(i.dir.z, i.dir.x) * 8.0, y * 16.0);
                float2 cell = floor(uv * 3.0);
                float h = Hash(cell);
                float2 f = frac(uv * 3.0) - 0.5;
                half flake = step(0.86, h) * smoothstep(0.18, 0.0, length(f + (Hash(cell + 3.1) - 0.5) * 0.5));
                c = lerp(c, half3(1, 1, 1), flake * _Flakes * 0.8h);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
