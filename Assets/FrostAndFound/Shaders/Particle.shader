// Billboard particles (snowfall, glints, puffs, steam, birdies' stars). Alpha or additive by blend props.
Shader "FrostAndFound/Particle"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _NoOcclusion ("No Depth Occlusion", Float) = 0
        _Additive ("Additive", Float) = 0
        _NearFade ("Near Fade Distance (0 = off)", Float) = 0
        [HideInInspector] _SrcBlend ("Src", Float) = 5
        [HideInInspector] _DstBlend ("Dst", Float) = 10
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Pass
        {
            Name "Particle"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex UnlitVert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ HARD_OCCLUSION SOFT_OCCLUSION
            #include "FrostUnlitCore.hlsl"
            half4 Frag(Varyings i) : SV_Target
            {
                half4 c = UnlitFragColor(i) * i.color;
                // fade out flakes drifting right in front of your eyes (and in front of UI panels)
                if (_NearFade > 0) c.a *= saturate((i.viewDist - _NearFade * 0.35) / (_NearFade * 0.65));
                c.a *= FrostOcclusion(i.positionWS, _NoOcclusion);   // hidden behind real people and furniture
                return c;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
