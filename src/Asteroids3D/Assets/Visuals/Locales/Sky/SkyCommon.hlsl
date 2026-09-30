#ifndef ASTRONOMICAL_SKY_COMMON_INCLUDED
#define ASTRONOMICAL_SKY_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

struct Attributes
{
    float3 positionOS : POSITION;
};

struct Varyings
{
    float4 positionHCS : SV_POSITION;
    float4 projectedPosition : TEXCOORD0;
};

struct SkyCoordinates
{
    float2 planePosition;
    float2 cameraPosition;
    float zoom;
};

float4 Hash42(float2 value)
{
    float4 p = frac(value.xyxy * float4(0.1031, 0.1030, 0.0973, 0.1099));
    p += dot(p, p.wzxy + 33.33);
    return frac((p.xxyz + p.yzzw) * p.zywx);
}

Varyings Vert(Attributes input)
{
    Varyings output;
    output.positionHCS = float4(input.positionOS.xy, UNITY_RAW_FAR_CLIP_VALUE, 1);
    output.projectedPosition = output.positionHCS;
    return output;
}

SkyCoordinates GetSkyCoordinates(float4 projectedPosition, float zoomReferenceSize)
{
    // The frozen Z game plane, seen by a flight camera looking down +Z.
    float3 planeRight = float3(1, 0, 0);
    float3 planeUp = float3(0, 1, 0);
    float3 cameraPositionWS = GetCameraPositionWS();
    float2 cameraPosition = float2(
        dot(cameraPositionWS, planeRight),
        dot(cameraPositionWS, planeUp));

    // Preserve authored scale at the main camera's initial orthographic size.
    float zoom = zoomReferenceSize * abs(UNITY_MATRIX_P._m11);
    float2 screenPosition = projectedPosition.xy / projectedPosition.w;
    float2 referencePosition = screenPosition * zoomReferenceSize * float2(
        abs(UNITY_MATRIX_P._m11) / UNITY_MATRIX_P._m00, sign(UNITY_MATRIX_P._m11));
    float3 referenceOffsetWS = UNITY_MATRIX_V[0].xyz * referencePosition.x +
        UNITY_MATRIX_V[1].xyz * referencePosition.y;
    float2 referencePlanePosition = float2(dot(referenceOffsetWS, planeRight), dot(referenceOffsetWS, planeUp));
    SkyCoordinates output;
    output.planePosition = referencePlanePosition;
    output.cameraPosition = cameraPosition;
    output.zoom = zoom;
    return output;
}

#endif
