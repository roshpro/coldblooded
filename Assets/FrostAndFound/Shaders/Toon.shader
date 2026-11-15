Shader "FrostAndFound/Toon"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Base Colour", Color) = (1,1,1,1)
        _ShadeColor ("Shade Colour (palette shadow)", Color) = (0.6,0.65,0.85,1)
        _RimColor ("Rim Colour", Color) = (1,1,1,1)
        _RimStrength ("Rim Strength", Range(0,1)) = 0.25
        _RimPower ("Rim Power", Range(1,8)) = 3.5
        _BandThreshold ("Band Threshold", Range(0,1)) = 0.52
        _BandSoftness ("Band Softness", Range(0.001,0.2)) = 0.035
        _ShadowStrength ("Cast Shadow Strength", Range(0,1)) = 0.85
        _AmbientStrength ("Ambient (SH) Strength", Range(0,1)) = 0.1
        _UnderShade ("Underside Shade (no bounce from the ground)", Range(0,1)) = 0
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,1)
        _Sparkle ("Glitter Amount", Range(0,1)) = 0
        _SparkleScale ("Glitter Density", Float) = 90
        _SparkleColor ("Glitter Colour", Color) = (1,1,1,1)
        _OutlineColor ("Outline Colour", Color) = (0.12,0.13,0.22,1)
        _OutlineWidth ("Outline Width", Range(0,4)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 200
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ToonVert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_instancing
            #include "FrostToonCore.hlsl"
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return ToonShade(input);
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
            #pragma target 3.5
            #pragma vertex ToonShadowVert
            #pragma fragment ToonShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "FrostToonCore.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ToonDepthVert
            #pragma fragment ToonShadowFrag
            #pragma multi_compile_instancing
            #include "FrostToonCore.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
