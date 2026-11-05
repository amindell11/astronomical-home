Shader "Locales/Planet"
{
    Properties
    {
        _MainTex ("Equirectangular Surface", 2D) = "black" {}
        _Tint ("Tint (RGB)", Color) = (1, 1, 1, 1)
        _PlanetCenter ("Center Relative to Locale (world units)", Vector) = (56000, 0, 100000, 0)
        _PlanetRadius ("Radius (world units)", Float) = 60000
        _LightDirection ("Direction Toward Light (world space)", Vector) = (-0.9, 0.25, -0.4, 0)
        _AxialTilt ("Axial Tilt (degrees)", Range(-90, 90)) = -20
        _RotationSpeed ("Surface Rotation (radians per second)", Range(-0.02, 0.02)) = 0.003
        _AtmosphereColor ("Atmospheric Rim", Color) = (0.04, 0.18, 0.16, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-20" "RenderType"="Transparent" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "../SkyCommon.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float4 _PlanetCenter;
                float4 _LightDirection;
                float4 _AtmosphereColor;
                float _PlanetRadius;
                float _AxialTilt;
                float _RotationSpeed;
            CBUFFER_END

            half4 Frag(Varyings input) : SV_Target
            {
                // Orthographic gameplay zoom leaves the distant sky's 40-degree field of view unchanged.
                float2 screen = input.projectedPosition.xy / input.projectedPosition.w;
                float2 view = screen * float2(abs(UNITY_MATRIX_P._m11) / UNITY_MATRIX_P._m00,
                    sign(UNITY_MATRIX_P._m11)) * 0.3639702343;
                float3 ray = normalize(UNITY_MATRIX_V[0].xyz * view.x +
                    UNITY_MATRIX_V[1].xyz * view.y - UNITY_MATRIX_V[2].xyz);
                float3 center = TransformObjectToWorld(float3(0, 0, 0)) + _PlanetCenter.xyz;
                float3 toCenter = (center - GetCameraPositionWS()) / _PlanetRadius;
                float along = dot(toCenter, ray);
                float3 nearest = ray * along - toCenter;
                float distanceSquared = dot(nearest, nearest);
                float outerRadiusSquared = 1.012 * 1.012;
                clip(along);
                clip(outerRadiusSquared - distanceSquared);
                float surfaceDepth = sqrt(max(0, 1 - distanceSquared));
                float3 normal = normalize(nearest - ray * surfaceDepth);
                float3 light = normalize(_LightDirection.xyz);
                float daylight = saturate(dot(normal, light));

                float tilt = radians(_AxialTilt);
                float3 surface = float3(normal.x * cos(tilt) + normal.y * sin(tilt),
                    normal.y * cos(tilt) - normal.x * sin(tilt), normal.z);
                float2 uv = float2(atan2(surface.x, -surface.z) / TWO_PI + 0.5 -
                    _Time.y * _RotationSpeed / TWO_PI, asin(clamp(surface.y, -1, 1)) / PI + 0.5);
                half3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb * _Tint.rgb;
                float grazing = pow(1 - saturate(dot(normal, -ray)), 5);
                half3 atmosphere = _AtmosphereColor.rgb * grazing * (0.15 + daylight * 0.85);
                half3 body = albedo * (0.08 + daylight * 0.92) + atmosphere;
                float haloAlpha = smoothstep(outerRadiusSquared, 1, distanceSquared) * 0.35;
                float coverage = saturate((1 - distanceSquared) / fwidth(distanceSquared) + 0.5);
                return half4(lerp(atmosphere * haloAlpha, body, coverage), lerp(haloAlpha, 1, coverage));
            }
            ENDHLSL
        }
    }
}
