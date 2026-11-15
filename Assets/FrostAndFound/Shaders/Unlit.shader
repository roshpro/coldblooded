Shader "FrostAndFound/Unlit"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Colour", Color) = (1,1,1,1)
        _NoOcclusion ("No Depth Occlusion", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            Blend One OneMinusSrcAlpha   // premultiplied, so real-world depth occlusion can fade it out
            HLSLPROGRAM
            #pragma vertex UnlitVert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ HARD_OCCLUSION SOFT_OCCLUSION
            #include "FrostUnlitCore.hlsl"
            half4 Frag(Varyings i) : SV_Target { half4 c = UnlitFragColor(i); half occ = FrostOcclusion(i.positionWS, _NoOcclusion); return half4(c.rgb * occ, occ); }
            ENDHLSL
        }
    }
    FallBack Off
}
