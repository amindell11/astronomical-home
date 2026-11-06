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
        _CloudGlow ("Cloud Interior Glow", Range(0, 3)) = 0
        _CloudGlowSpread ("Cloud Glow Spread", Range(0.01, 0.3)) = 0.16
        [HDR] _CloudGlowCool ("Cool Interior Light", Color) = (0.1, 0.65, 1.4, 1)
        [HDR] _CloudGlowWarm ("Violet Interior Light", Color) = (0.7, 0.15, 1.6, 1)
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

        HLSLINCLUDE
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
            float _CloudGlow;
            float _CloudGlowSpread;
            float4 _CloudGlowCool;
            float4 _CloudGlowWarm;
            float _CloudBankTileSize;
        CBUFFER_END

        TEXTURE2D(_CloudBankTile);
        SAMPLER(sampler_CloudBankTile);
        ENDHLSL

        Pass
        {
            Name "Nebula"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

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

            // A cloud bank is static and zoom-free, so its baked tile slides with parallax alone.
            half4 CloudBankFromTile(float2 position)
            {
                float angle = radians(_CloudDirection);
                float2 axis = float2(cos(angle), sin(angle));
                float2 rotated = float2(dot(position, axis), dot(position, float2(-axis.y, axis.x)));
                return SAMPLE_TEXTURE2D(_CloudBankTile, sampler_CloudBankTile, rotated / _CloudBankTileSize);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                SkyCoordinates coordinates = GetSkyCoordinates(input.projectedPosition, _ZoomReferenceSize);
                float2 position = coordinates.planePosition * pow(coordinates.zoom, -_NebulaZoomResponse);
                if (_CloudOpacity > 0)
                    return CloudBankFromTile(position + coordinates.cameraPosition * _NebulaParallax);
                return half4(Nebula(position, coordinates.cameraPosition), 0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "CloudBankBake"
            Tags { "LightMode" = "CloudBankBake" }

            Blend Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex BakeVert
            #pragma fragment BakeFrag

            struct BakeVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            BakeVaryings BakeVert(uint vertexID : SV_VertexID)
            {
                BakeVaryings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(vertexID);
                output.uv = GetFullScreenTriangleTexCoord(vertexID);
                return output;
            }

            float TileHash(float4 p)
            {
                p = frac(p * float4(0.1031, 0.1030, 0.0973, 0.1099));
                p += dot(p, p.wzxy + 33.33);
                return frac((p.x + p.y) * (p.z + p.w));
            }

            float ValueNoise4(float4 position)
            {
                float4 cell = floor(position);
                float4 blend = frac(position);
                blend = blend * blend * (3.0 - 2.0 * blend);
                float value = 0;
                [unroll]
                for (int corner = 0; corner < 16; corner++)
                {
                    float4 offset = float4(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1, (corner >> 3) & 1);
                    float4 weight = lerp(1.0 - blend, blend, offset);
                    value += TileHash(cell + offset) * weight.x * weight.y * weight.z * weight.w;
                }
                return value;
            }

            // Each axis wraps once around a circle of its period's circumference, so the tile repeats seamlessly.
            float TileNoise(float2 position, float2 period, float4 offset)
            {
                float2 angle = position / period * TWO_PI;
                float2 radius = period / TWO_PI;
                float4 torus = float4(cos(angle.x) * radius.x, sin(angle.x) * radius.x,
                    cos(angle.y) * radius.y, sin(angle.y) * radius.y);
                // Interpolating across four axes flattens value noise; restore its 2D contrast.
                return saturate(0.5 + (ValueNoise4(torus + offset) - 0.5) * 1.346);
            }

            float TileFractal(float2 position, float2 period, float4 offset)
            {
                float value = 0;
                float weight = 0.5;
                [unroll]
                for (int octave = 0; octave < 5; octave++)
                {
                    value += TileNoise(position, period, offset) * weight;
                    position *= 2.03;
                    period *= 2.03;
                    offset += float4(7.1, 13.7, 5.3, 11.9);
                    weight *= 0.5;
                }
                return value;
            }

            half4 CloudBankTile(float2 grain, float2 period)
            {
                float4 seed = _Seed * float4(13.7, 29.3, 7.9, 17.3);
                float2 stretch = float2(1.0 / _CloudStretch, 1);
                float2 p = grain * stretch;
                float2 pPeriod = period * stretch;
                float2 warp = float2(TileFractal(p * 0.7, pPeriod * 0.7, seed),
                    TileFractal(p * 0.7, pPeriod * 0.7, seed + 19.3)) - 0.5;
                float2 cloud = p + warp * 2.4;
                float detail = TileFractal(grain * 4 + warp * 4, period * 4, 0);
                float broadField = TileFractal(cloud, pPeriod, seed);
                float field = broadField + (detail - 0.5) * 0.16;
                float threshold = 1 - _CloudCoverage;
                float coverage = smoothstep(threshold - 0.09, threshold + 0.12, field);
                float terraces = lerp(field, floor(field * 24) / 24, 0.35);
                float light = pow(smoothstep(threshold + 0.02, threshold + 0.30, terraces), 2.5);
                float violet = pow(saturate(1 - abs(TileFractal(p * 1.4, pPeriod * 1.4, seed + 71.2) - 0.5) * 10), 1.5);
                float3 color = lerp(_CloudShadow.rgb, _NebulaCool.rgb, light);
                color = lerp(color, _NebulaWarm.rgb * (0.2 + light), violet * 0.25);
                color += _NebulaWarm.rgb * violet * sqrt(light) * 0.15;
                color *= _NebulaStrength;
                float opacity = coverage * _CloudOpacity;
                float glowEnvelope = smoothstep(threshold - _CloudGlowSpread, threshold + 0.18, broadField);
                float lightPockets = pow(smoothstep(0.4, 0.88,
                    TileNoise(cloud * 1.6, pPeriod * 1.6, seed + float4(9.1, 41.3, 3.7, 23.1))), 2);
                lightPockets *= 0.5 + 2.5 * lightPockets * lightPockets * lightPockets;
                float glowHue = smoothstep(0.5, 0.75, TileNoise(p * 0.8, pPeriod * 0.8, seed + 71.2));
                float3 glow = _CloudShadow.rgb * 0.25 + lerp(_CloudGlowCool.rgb, _CloudGlowWarm.rgb, glowHue) * lightPockets;
                float shadowField = TileFractal(grain * 1.4, period * 1.4, 25.7);
                float lightShadow = smoothstep(0.36, 0.4, shadowField) * 0.22 +
                    smoothstep(0.44, 0.48, shadowField) * 0.3 + smoothstep(0.52, 0.56, shadowField) * 0.35;
                glow *= glowEnvelope * lerp(1, 0.2, lightShadow) * _CloudGlow * _NebulaStrength;
                return half4(color * opacity + glow, opacity);
            }

            half4 BakeFrag(BakeVaryings input) : SV_Target
            {
                float2 period = _CloudBankTileSize * _NebulaScale;
                return CloudBankTile(input.uv * period, period);
            }
            ENDHLSL
        }
    }
}
