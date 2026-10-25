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
    float3 cameraPositionWS = GetCameraPositionWS();
    float3 forward = -UNITY_MATRIX_V[2].xyz;
    float distanceToCenter = -cameraPositionWS.z / forward.z;
    float3 centerOffset = forward * distanceToCenter;
    float2 cameraPosition = (cameraPositionWS + centerOffset).xy;
    float zoom = zoomReferenceSize * abs(UNITY_MATRIX_P._m11) /
        lerp(distanceToCenter, 1.0, unity_OrthoParams.w);
    float2 screenPosition = projectedPosition.xy / projectedPosition.w;
    float3 viewOffset = UNITY_MATRIX_V[0].xyz * screenPosition.x / UNITY_MATRIX_P._m00 +
        UNITY_MATRIX_V[1].xyz * screenPosition.y / UNITY_MATRIX_P._m11;
    float3 rayDirection = forward + viewOffset * (1.0 - unity_OrthoParams.w);
    float3 rayOffset = viewOffset * unity_OrthoParams.w;
    float3 planeOffset = rayOffset - rayDirection *
        ((cameraPositionWS.z + rayOffset.z) / rayDirection.z) - centerOffset;
    SkyCoordinates output;
    output.planePosition = planeOffset.xy * zoom;
    output.cameraPosition = cameraPosition;
    output.zoom = zoom;
    return output;
}

#endif
