Shader "Custom/Nebula"
{
    Properties
    {
        [HideInInspector][PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _ZoomReferenceSize ("Zoom Reference Size", Float) = 7
        _Seed ("Seed", Float) = 0
        [Header(Nebula)]
        _NebulaStrength ("Nebula Strength", Range(0, 2)) = 0
        _NebulaScale ("Nebula Scale", Range(0.01, 0.3)) = 0.085
        _NebulaSpeed ("Nebula Motion Speed", Range(0, 0.05)) = 0.008
        _NebulaParallax ("Nebula Parallax", Range(0, 1)) = 0.025
        _NebulaZoomResponse ("Nebula Response to Zoom", Range(0, 1)) = 0
        [Toggle] _NebulaForeground ("Foreground Wisps Only", Float) = 0
        _NebulaCool ("Nebula Cool Color", Color) = (0.18, 0.48, 0.65, 1)
        _NebulaWarm ("Nebula Warm Color", Color) = (0.5, 0.2, 0.38, 1)
        [Header(Cloud Banks)]
        _CloudOpacity ("Cloud Bank Opacity (0 for additive wisps)", Range(0, 1)) = 0
        _CloudCoverage ("Cloud Bank Coverage", Range(0, 1)) = 0.5
        _CloudDirection ("Cloud Bank Direction", Range(-180, 180)) = 25
        _CloudStretch ("Cloud Bank Stretch", Range(1, 6)) = 2.5
        _CloudShadow ("Cloud Bank Shadow", Color) = (0.025, 0.09, 0.3, 1)
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth Test", Float) = 4

    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent-40"
        }

        Blend One OneMinusSrcAlpha
        Cull Off
        ZTest [_ZTest]
        ZWrite Off

        Pass
        {
            Name "Nebula"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "SkyCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Seed;
                float _ZoomReferenceSize;
                float _NebulaStrength;
                float _NebulaScale;
                float _NebulaSpeed;
                float _NebulaParallax;
                float _NebulaZoomResponse;
                float _NebulaForeground;
                float4 _NebulaCool;
                float4 _NebulaWarm;
                float _CloudOpacity;
                float _CloudCoverage;
                float _CloudDirection;
                float _CloudStretch;
                float4 _CloudShadow;
            CBUFFER_END

            float CloudNoise(float2 position)
            {
                float2 cell = floor(position);
                float2 blend = frac(position);
                blend = blend * blend * (3.0 - 2.0 * blend);
                return lerp(lerp(Hash42(cell).x, Hash42(cell + float2(1, 0)).x, blend.x),
                    lerp(Hash42(cell + float2(0, 1)).x, Hash42(cell + 1).x, blend.x), blend.y);
            }

            float3 NebulaLayer(float2 position, float2 drift, float layerSeed, float coverageStart)
            {
                float time = _Time.y * _NebulaSpeed;
                float2 p = position * _NebulaScale + (_Seed + layerSeed) * float2(13.7, 29.3) + time * drift;
                float2 warp = float2(CloudNoise(p * 0.6 + float2(time, 0)),
                    CloudNoise(p * 0.6 + float2(17.3, -time))) - 0.5;
                float2 cloudPosition = p + warp * 2.5;
                float broad = CloudNoise(cloudPosition);
                float detail = CloudNoise(cloudPosition * 2.1 + 31.7);
                float fine = CloudNoise(cloudPosition * 4.3 - 19.1);
                float cloud = broad * 0.6 + detail * 0.28 + fine * 0.12;
                float coverage = smoothstep(coverageStart, 0.78, cloud);
                float filaments = pow(saturate(1.0 - abs(detail * 2.0 - 1.0)), 3.0);
                if (_NebulaForeground > 0.5) coverage *= smoothstep(0.8, 0.95, filaments);
                float3 color = lerp(_NebulaCool.rgb, _NebulaWarm.rgb, smoothstep(0.3, 0.7, broad));
                return color * coverage * (0.35 + filaments * 0.65) * _NebulaStrength;
            }

            float3 Nebula(float2 position, float2 cameraPosition)
            {
                [branch]
                if (_NebulaStrength <= 0) return 0;
                if (_NebulaForeground > 0.5)
                    return NebulaLayer(position + cameraPosition * _NebulaParallax,
                        float2(0.7, -0.5), 7.3, 0.66);
                float3 farClouds = NebulaLayer((position + cameraPosition * _NebulaParallax * 0.5) * 0.7,
                    float2(0.35, 0.15), 0, 0.42);
                float3 nearWisps = NebulaLayer(position + cameraPosition * _NebulaParallax,
                    float2(-0.25, 0.45), 3.7, 0.48);
                return farClouds * 0.6 + nearWisps * 0.4;
            }

            float CloudFractal(float2 p)
            {
                float value = 0;
                float weight = 0.5;
                [unroll]
                for (int octave = 0; octave < 5; octave++)
                {
                    value += CloudNoise(p) * weight;
                    p = p * 2.03 + float2(7.1, 13.7);
                    weight *= 0.5;
                }
                return value;
            }

            half4 CloudBanks(float2 position)
            {
                float angle = radians(_CloudDirection);
                float2 axis = float2(cos(angle), sin(angle));
                float2 p = float2(dot(position, axis), dot(position, float2(-axis.y, axis.x)));
                p *= _NebulaScale;
                float2 grainPosition = p;
                p *= float2(1.0 / _CloudStretch, 1);
                p += _Seed * float2(13.7, 29.3) + _Time.y * _NebulaSpeed * float2(0.2, 0.1);
                float2 warp = float2(CloudFractal(p * 0.7), CloudFractal(p * 0.7 + 19.3)) - 0.5;
                float2 cloud = p + warp * 2.4;
                float detail = CloudFractal(grainPosition * 7 + warp * 4);
                float field = CloudFractal(cloud) + (detail - 0.5) * 0.16;
                float threshold = 1 - _CloudCoverage;
                float coverage = smoothstep(threshold - 0.09, threshold + 0.12, field);
                float terraces = lerp(field, floor(field * 24) / 24, 0.35);
                float light = pow(smoothstep(threshold + 0.02, threshold + 0.30, terraces), 2.5);
                float violet = pow(saturate(1 - abs(CloudFractal(p * 1.4 + 71.2) - 0.5) * 10), 1.5);
                float3 color = lerp(_CloudShadow.rgb, _NebulaCool.rgb, light);
                color = lerp(color, _NebulaWarm.rgb * (0.2 + light), violet * 0.65);
                color += _NebulaWarm.rgb * violet * sqrt(light) * 0.35;
                color *= _NebulaStrength;
                float opacity = coverage * _CloudOpacity;
                return half4(color * opacity, opacity);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                SkyCoordinates coordinates = GetSkyCoordinates(input.projectedPosition, _ZoomReferenceSize);
                float2 position = coordinates.planePosition * pow(coordinates.zoom, -_NebulaZoomResponse);
                if (_CloudOpacity > 0)
                    return CloudBanks(position + coordinates.cameraPosition * _NebulaParallax);
                return half4(Nebula(position, coordinates.cameraPosition), 0);
            }
            ENDHLSL
        }
    }
}
