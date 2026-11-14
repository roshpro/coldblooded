#ifndef FROST_TOON_CORE_INCLUDED
#define FROST_TOON_CORE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half4 _ShadeColor;
    half4 _RimColor;
    half4 _EmissionColor;
    half4 _OutlineColor;
    half4 _SparkleColor;
    half _RimStrength;
    half _RimPower;
    half _BandSoftness;
    half _BandThreshold;
    half _ShadowStrength;
    half _AmbientStrength;
    half _OutlineWidth;
    half _Sparkle;
    half _SparkleScale;
CBUFFER_END

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 uv         : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3  normalWS   : TEXCOORD2;
    float3 positionOS : TEXCOORD3;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings ToonVert(Attributes input)
{
    Varyings o = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
    o.positionCS = p.positionCS;
    o.positionWS = p.positionWS;
    o.normalWS = TransformObjectToWorldNormal(input.normalOS);
    o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    o.positionOS = input.positionOS.xyz;
    return o;
}

// Cheap hash for glitter / snow glints.
float FrostHash31(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

half FrostSparkle(float3 positionWS, half3 normalWS, half3 viewDirWS, half amount, half scale)
{
    if (amount <= 0.001h) return 0;
    float3 cell = floor(positionWS * scale);
    float h = FrostHash31(cell);
    float h2 = FrostHash31(cell + 17.13);
    // each glint has its own facing; it flashes only when the view lines up with it
    float3 facing = normalize(float3(h - 0.5, h2 - 0.5, FrostHash31(cell + 3.7) - 0.5) + normalWS * 0.6);
    float align = saturate(dot(facing, viewDirWS));
    float twinkle = 0.6 + 0.4 * sin(_Time.y * (2.0 + h * 6.0) + h2 * 40.0);
    half s = step(1.0 - 0.14 * amount, h) * pow(align, 24.0) * twinkle;
    return s * 3.0;
}

half4 ToonShade(Varyings input)
{
    half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
    half3 baseCol = _BaseColor.rgb * tex.rgb;
    half3 shadeCol = _ShadeColor.rgb * lerp(half3(1, 1, 1), tex.rgb, 0.85h);

    half3 N = normalize(input.normalWS);
    half3 V = normalize(GetWorldSpaceViewDir(input.positionWS));

#if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
#else
    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
#endif
    Light mainLight = GetMainLight(shadowCoord);
    half ndl = dot(N, mainLight.direction) * 0.5h + 0.5h;
    half band = smoothstep(_BandThreshold - _BandSoftness, _BandThreshold + _BandSoftness, ndl);
    half shadow = smoothstep(0.35h, 0.65h, mainLight.shadowAttenuation);
    band *= lerp(1.0h, shadow, _ShadowStrength);

    // tint by the light's hue only (its intensity would blow the palette out)
    half3 lc = mainLight.color;
    half lum = max(max(lc.r, lc.g), max(lc.b, 0.001h));
    half3 lightTint = lerp(half3(1, 1, 1), lc / lum, 0.5h);
    half3 col = lerp(shadeCol, baseCol * lightTint, band);

    // the ambient answer: SH keeps shadow sides alive
    col += SampleSH(N) * baseCol * _AmbientStrength;

    // a soft highlight band on the brightest side
    half hi = smoothstep(0.86h, 0.9h, ndl) * band;
    col += baseCol * hi * 0.08h;

    // rim
    half rim = pow(1.0h - saturate(dot(N, V)), _RimPower) * _RimStrength;
    col += _RimColor.rgb * rim * (0.35h + 0.65h * band);

    // glitter
    col += _SparkleColor.rgb * FrostSparkle(input.positionWS, N, V, _Sparkle, _SparkleScale);

    col += _EmissionColor.rgb;
    return half4(col, 1.0h);
}

// ---------------------------------------------------------------- shadow caster / depth
float3 _LightDirection;
float3 _LightPosition;

struct ShadowVaryings
{
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

ShadowVaryings ToonShadowVert(Attributes input)
{
    ShadowVaryings o = (ShadowVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
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

ShadowVaryings ToonDepthVert(Attributes input)
{
    ShadowVaryings o = (ShadowVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    return o;
}

half4 ToonShadowFrag(ShadowVaryings input) : SV_Target
{
    return 0;
}

// ---------------------------------------------------------------- inverted-hull outline
Varyings ToonOutlineVert(Attributes input)
{
    Varyings o = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    float3 normalCS = TransformWorldToHClipDir(normalWS);
    float2 offset = normalCS.xy;
    float len = max(length(offset), 1e-4);
    offset /= len;
    offset.x *= _ScreenParams.y / max(_ScreenParams.x, 1.0);
    // ~2 px at 1800 px eye height, constant in screen space, capped so close-ups stay tidy
    float w = _OutlineWidth * 0.0022 * min(positionCS.w, 2.5);
    positionCS.xy += offset * w;
    o.positionCS = positionCS;
    o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
    return o;
}

half4 ToonOutlineFrag(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    return half4(_OutlineColor.rgb, 1.0h);
}

#endif
