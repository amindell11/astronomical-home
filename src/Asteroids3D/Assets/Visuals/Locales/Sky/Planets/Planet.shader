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
        _SurfaceLongitude ("Surface Longitude (degrees)", Range(-180, 180)) = 0
        _RotationSpeed ("Surface Rotation (radians per second)", Range(-0.02, 0.02)) = 0.003
        _BandStreaming ("Band Streaming (radians per second)", Range(0, 0.06)) = 0
        _BandCount ("Band Count (pole to pole)", Range(2, 16)) = 7
        _StormChurn ("Storm Churn", Range(0, 0.06)) = 0
        _StormCellScale ("Storm Cell Scale (higher is smaller)", Range(2, 24)) = 8
        _FlowCycle ("Flow Cycle (seconds)", Range(4, 60)) = 14
        _AtmosphereColor ("Atmospheric Rim", Color) = (0.04, 0.18, 0.16, 1)
        _RimPower ("Rim Tightness", Range(1, 12)) = 8
        _RimIntensity ("Rim Brightness", Range(0, 8)) = 1
        _HaloWidth ("Halo Reach (fraction of radius)", Range(0.002, 0.3)) = 0.006
        _HaloIntensity ("Halo Brightness", Range(0, 8)) = 0.35
        _HaloFalloff ("Halo Falloff", Range(0.5, 6)) = 1
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
                float _SurfaceLongitude;
                float _RotationSpeed;
                float _BandStreaming;
                float _BandCount;
                float _StormChurn;
                float _StormCellScale;
                float _FlowCycle;
                float _RimPower;
                float _RimIntensity;
                float _HaloWidth;
                float _HaloIntensity;
                float _HaloFalloff;
            CBUFFER_END

            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            // x is value noise, yzw its gradient.
            float4 ValueNoiseWithGradient(float3 position)
            {
                float3 cell = floor(position);
                float3 w = frac(position);
                float3 u = w * w * w * (w * (w * 6 - 15) + 10);
                float3 du = 30 * w * w * (w * (w - 2) + 1);
                float a = Hash13(cell);
                float b = Hash13(cell + float3(1, 0, 0));
                float c = Hash13(cell + float3(0, 1, 0));
                float d = Hash13(cell + float3(1, 1, 0));
                float e = Hash13(cell + float3(0, 0, 1));
                float f = Hash13(cell + float3(1, 0, 1));
                float g = Hash13(cell + float3(0, 1, 1));
                float h = Hash13(cell + float3(1, 1, 1));
                float k1 = b - a, k2 = c - a, k3 = e - a;
                float k4 = a - b - c + d, k5 = a - c - e + g, k6 = a - b - e + f;
                float k7 = -a + b + c - d + e - f - g + h;
                return float4(a + k1 * u.x + k2 * u.y + k3 * u.z + k4 * u.x * u.y + k5 * u.y * u.z +
                    k6 * u.z * u.x + k7 * u.x * u.y * u.z,
                    du * float3(k1 + k4 * u.y + k6 * u.z + k7 * u.y * u.z,
                        k2 + k5 * u.z + k4 * u.x + k7 * u.z * u.x,
                        k3 + k6 * u.x + k5 * u.y + k7 * u.x * u.y));
            }

            float3 TurnAboutPole(float3 direction, float angle)
            {
                float s, c;
                sincos(angle, s, c);
                return float3(direction.x * c - direction.z * s, direction.y, direction.z * c + direction.x * s);
            }

            half3 SurfaceColor(float3 direction, float spin)
            {
                float2 uv = float2(atan2(direction.x, -direction.z) / TWO_PI + 0.5 + spin / TWO_PI,
                    asin(clamp(direction.y, -1, 1)) / PI + 0.5);
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Orthographic gameplay zoom leaves the distant sky's 40-degree field of view unchanged.
                float2 screen = input.projectedPosition.xy / input.projectedPosition.w;
                float2 view = screen * float2(abs(UNITY_MATRIX_P._m11) / UNITY_MATRIX_P._m00,
                    sign(UNITY_MATRIX_P._m11)) * 0.3639702343;
                float3 ray = normalize(UNITY_MATRIX_V[0].xyz * view.x +
                    UNITY_MATRIX_V[1].xyz * view.y - UNITY_MATRIX_V[2].xyz);
                float3 center = TransformObjectToWorld(float3(0, 0, 0)) + _PlanetCenter.xyz;
                float3 toCenter = (center - UNITY_MATRIX_I_V._m03_m13_m23) / _PlanetRadius;
                float along = dot(toCenter, ray);
                float3 nearest = ray * along - toCenter;
                float distanceSquared = dot(nearest, nearest);
                float outerRadiusSquared = (1 + _HaloWidth) * (1 + _HaloWidth);
                clip(along);
                clip(outerRadiusSquared - distanceSquared);
                float surfaceDepth = sqrt(max(0, 1 - distanceSquared));
                float3 normal = normalize(nearest - ray * surfaceDepth);
                float3 light = normalize(_LightDirection.xyz);
                float daylight = saturate(dot(normal, light));

                float tilt = radians(_AxialTilt);
                float3 surface = float3(normal.x * cos(tilt) + normal.y * sin(tilt),
                    normal.y * cos(tilt) - normal.x * sin(tilt), normal.z);
                float spin = radians(_SurfaceLongitude) - _Time.y * _RotationSpeed;

                float4 cells = ValueNoiseWithGradient(TurnAboutPole(surface, spin) * _StormCellScale);
                float3 churn = cross(surface, TurnAboutPole(cells.yzw, -spin)) * _StormChurn / _StormCellScale;
                float stream = _BandStreaming * cos(_BandCount * asin(clamp(surface.y, -1, 1)));
                // Two readings of the map, half a cycle apart, cross-fade so the flow's stretch never builds up.
                float2 phase = frac(_Time.y / _FlowCycle + cells.x * 0.6 + float2(0, 0.5));
                float2 elapsed = (phase - 0.5) * _FlowCycle;
                half3 first = SurfaceColor(
                    TurnAboutPole(normalize(surface - churn * elapsed.x), -stream * elapsed.x), spin);
                half3 second = SurfaceColor(
                    TurnAboutPole(normalize(surface - churn * elapsed.y), -stream * elapsed.y), spin);
                half3 albedo = lerp(first, second, abs(2 * phase.x - 1)) * _Tint.rgb;

                float glowLight = 0.15 + daylight * 0.85;
                float grazing = PositivePow(1 - saturate(dot(normal, -ray)), _RimPower);
                half3 atmosphere = _AtmosphereColor.rgb * grazing * _RimIntensity * glowLight;
                half3 body = albedo * (0.08 + daylight * 0.92) + atmosphere;
                float falloff = PositivePow(smoothstep(outerRadiusSquared, 1, distanceSquared), _HaloFalloff);
                half3 halo = _AtmosphereColor.rgb * glowLight * falloff * _HaloIntensity;
                float coverage = saturate((1 - distanceSquared) / fwidth(distanceSquared) + 0.5);
                return half4(lerp(halo, body, coverage), lerp(falloff * 0.35, 1, coverage));
            }
            ENDHLSL
        }
    }
}
