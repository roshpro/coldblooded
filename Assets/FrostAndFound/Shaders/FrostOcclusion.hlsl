#ifndef FROST_OCCLUSION_INCLUDED
#define FROST_OCCLUSION_INCLUDED

// Real-world occlusion from the Quest 3 depth sensor (Meta Depth API). A real person walking past, the sofa,
// a table: anything real in front of a virtual thing hides it. The keywords are switched on globally by
// EnvironmentDepthManager only on a headset that has depth; elsewhere every call below returns 1.
#include "Packages/com.meta.xr.sdk.core/Shaders/EnvironmentDepth/URP/EnvironmentOcclusionURP.hlsl"

// 1 while a camera other than the player's eyes renders (UI overlay, the Polaroid lens): those never occlude.
float _FrostOcclusionOff;

// Renderers with this rendering-layer bit are never occluded (DepthOcclusion.Exempt: the player's own kit, the
// aurora that lives beyond the walls). A per-renderer bit needs no material copies, so animated materials stay shared.
#define FROST_NO_OCCLUSION_LAYER (1u << 30)

// 1 = this object is fully visible, 0 = a real surface is in front of it. noOcclusion (per material) opts out.
half FrostOcclusionBiased(float3 positionWS, half noOcclusion, float bias)
{
#if defined(HARD_OCCLUSION) || defined(SOFT_OCCLUSION)
    half occ = (half)CalculateEnvironmentDepthOcclusion(positionWS, bias);
    // the sensor sees nothing useful closer than ~0.4 m, and anything that close is the player's own hands:
    // held things (snowball, camera, photos) are never cut out by the hand or controller holding them
    float d = distance(positionWS, GetCameraPositionWS());
    occ = lerp(1.0h, occ, (half)saturate((d - 0.45) / 0.4));
    half exempt = (GetMeshRenderingLayer() & FROST_NO_OCCLUSION_LAYER) != 0 ? 1.0h : 0.0h;
    return lerp(occ, 1.0h, (half)saturate(noOcclusion + _FrostOcclusionOff + exempt));
#else
    return 1.0h;
#endif
}

// 5% depth bias keeps feet on the real floor and snow caps on real tables from flickering
half FrostOcclusion(float3 positionWS, half noOcclusion) { return FrostOcclusionBiased(positionWS, noOcclusion, 0.05); }

#endif
