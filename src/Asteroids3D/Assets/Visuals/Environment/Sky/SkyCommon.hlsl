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
    output.positionHCS = TransformObjectToHClip(input.positionOS);
    output.projectedPosition = output.positionHCS;
    return output;
}

SkyCoordinates GetSkyCoordinates(float4 projectedPosition, float zoomReferenceSize)
{
    float3 planeRight = normalize(float3(
        unity_ObjectToWorld._m00,
        unity_ObjectToWorld._m10,
        unity_ObjectToWorld._m20));
    float3 planeUp = normalize(float3(
        unity_ObjectToWorld._m01,
        unity_ObjectToWorld._m11,
        unity_ObjectToWorld._m21));
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
