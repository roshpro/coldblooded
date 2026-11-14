Shader "FrostAndFound/Unlit"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Colour", Color) = (1,1,1,1)
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
            HLSLPROGRAM
            #pragma vertex UnlitVert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "FrostUnlitCore.hlsl"
            half4 Frag(Varyings i) : SV_Target { half4 c = UnlitFragColor(i); return half4(c.rgb, 1); }
            ENDHLSL
        }
    }
    FallBack Off
}
