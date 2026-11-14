// Alpha-blended unlit (decals, photo overlays, speech bubbles). Separate alpha blend keeps passthrough correct.
Shader "FrostAndFound/UnlitTransparent"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Colour", Color) = (1,1,1,1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "UnlitTransparent"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex UnlitVert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "FrostUnlitCore.hlsl"
            half4 Frag(Varyings i) : SV_Target { return UnlitFragColor(i); }
            ENDHLSL
        }
    }
    FallBack Off
}
