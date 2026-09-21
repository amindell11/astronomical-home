Shader "Custom/StarField"
{
    Properties
    {
        [HideInInspector][PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        [Header(Pattern)]
        _Seed ("Seed", Float) = 0
        _StarDensity ("Total Star Density", Range(0, 1)) = 0.12
        _CellScale ("Cells Per World Unit", Range(0.05, 4)) = 0.8
        _StarSizeMin ("Minimum Star Radius", Range(0.002, 0.15)) = 0.012
        _StarSizeMax ("Maximum Star Radius", Range(0.002, 0.2)) = 0.045
        _PositionJitter ("Position Jitter", Range(0, 0.6)) = 0.5

        [HideInInspector] _ZoomReferenceSize ("Zoom Reference Size", Float) = 7

        [Header(Depth)]
        _ParallaxScale ("Overall Parallax Scale", Range(0, 2)) = 1
        _ParallaxFar ("Far Parallax", Range(0, 2)) = 0.2
        _ParallaxNear ("Near Parallax", Range(0, 2)) = 0.9
        _NearLayerShare ("Near Half Share", Range(0, 1)) = 0.35

        _DepthElongation ("Extra Far Elongation", Range(0, 1)) = 0.35

        [Header(Zoom)]
        _SizeZoomResponse ("Size Response to Zoom", Range(0, 1)) = 1
        _SpacingZoomResponse ("Spacing Response to Zoom", Range(0, 1)) = 1

        [Header(Appearance)]
        [HDR] _ColorCool ("Cool Star Color", Color) = (0.65, 0.8, 1, 1)
        [HDR] _ColorWarm ("Warm Star Color", Color) = (1, 0.82, 0.58, 1)
        _WarmColorShare ("Warm Color Share", Range(0, 1)) = 0.25
        _Brightness ("Brightness", Range(0, 8)) = 1.5
        _HaloSize ("Halo Size", Range(1, 4)) = 2
        _HaloStrength ("Halo Strength", Range(0, 1)) = 0.2

        _ShapeVariation ("Shape Variation", Range(0, 1)) = 0
        _Softness ("Softness", Range(0, 1)) = 0


        [Header(Nebula)]
        _NebulaStrength ("Nebula Strength", Range(0, 0.3)) = 0
        _NebulaScale ("Nebula Scale", Range(0.01, 0.3)) = 0.085
        _NebulaSpeed ("Nebula Motion Speed", Range(0, 0.05)) = 0.008
        _NebulaParallax ("Nebula Parallax", Range(0, 1)) = 0.025
        _NebulaZoomResponse ("Nebula Response to Zoom", Range(0, 1)) = 0
        [Toggle] _NebulaForeground ("Foreground Wisps Only", Float) = 0
        _NebulaCool ("Nebula Cool Color", Color) = (0.18, 0.48, 0.65, 1)
        _NebulaWarm ("Nebula Warm Color", Color) = (0.5, 0.2, 0.38, 1)

        [Header(Shooting Stars)]
        _ShootingBrightness ("Shooting Star Brightness", Range(0, 2)) = 0
        _ShootingInterval ("Shooting Star Interval (seconds per region)", Range(6, 60)) = 12
        _ShootingParallax ("Shooting Star Parallax", Range(0, 1)) = 0.1
        _ShootingColor ("Shooting Star Color", Color) = (0.65, 0.8, 1, 1)

        [Header(Motion)]
        _TwinkleNoise ("Twinkle Noise", Range(0, 1)) = 0
        _TwinkleAmount ("Twinkle Amount", Range(0, 1)) = 0.2
        _TwinkleDurationMin ("Minimum Twinkle Duration (seconds)", Range(0.1, 60)) = 10
        _TwinkleDurationMax ("Maximum Twinkle Duration (seconds)", Range(0.1, 60)) = 18
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent-50"
        }

        Blend One One
        Cull Off
        ZTest LEqual
        ZWrite Off

        Pass
        {
            Name "StarField"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

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

            CBUFFER_START(UnityPerMaterial)
                float _Seed;
                float _StarDensity;
                float _CellScale;
                float _StarSizeMin;
                float _StarSizeMax;
                float _PositionJitter;
                float _ZoomReferenceSize;
                float _ParallaxScale;
                float _ParallaxFar;
                float _ParallaxNear;
                float _NearLayerShare;
                float _DepthElongation;
                float _SizeZoomResponse;
                float _SpacingZoomResponse;
                float4 _ColorCool;
                float4 _ColorWarm;
                float _WarmColorShare;
                float _Brightness;
                float _HaloSize;
                float _HaloStrength;
                float _ShapeVariation;
                float _Softness;
                float _TwinkleNoise;
                float _TwinkleAmount;
                float _TwinkleDurationMin;
                float _TwinkleDurationMax;
                float _NebulaStrength;
                float _NebulaScale;
                float _NebulaSpeed;
                float _NebulaParallax;
                float _NebulaZoomResponse;
                float _NebulaForeground;
                float4 _NebulaCool;
                float4 _NebulaWarm;
                float _ShootingBrightness;
                float _ShootingInterval;
                float _ShootingParallax;
                float4 _ShootingColor;
            CBUFFER_END

            float4 Hash42(float2 value)
            {
                float4 p = frac(value.xyxy * float4(0.1031, 0.1030, 0.0973, 0.1099));
                p += dot(p, p.wzxy + 33.33);
                return frac((p.xxyz + p.yzzw) * p.zywx);
            }

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

            float3 ShootingRegion(float2 position, float2 region, float aa)
            {
                const float regionSize = 24.0;
                float4 regionRandom = Hash42(region + _Seed * float2(31.3, 17.7));
                float time = _Time.y + regionRandom.x * _ShootingInterval;
                float cycle = floor(time / _ShootingInterval);
                float4 random = Hash42(region + cycle * float2(73.1, 91.7) + _Seed + 173.3);
                float age = time - cycle * _ShootingInterval - lerp(0.1, 0.6, random.x) * _ShootingInterval;
                float duration = lerp(0.9, 1.5, random.y);
                [branch]
                if (random.w > 0.65 || age <= 0 || age >= duration) return 0;

                float life = age / duration;
                float angle = lerp(-0.9, -0.3, random.z) + step(0.5, regionRandom.z) * PI;
                float2 direction = float2(cos(angle), sin(angle));
                float2 center = (region + regionRandom.yz) * regionSize;
                float2 head = center + direction * lerp(-4.0, 4.0, life);
                float2 offset = position - head;
                float along = dot(offset, direction);
                float across = abs(dot(offset, float2(-direction.y, direction.x)));
                float tail = saturate(1.0 + along / 3.0);

                float width = 0.012 * tail;
                float streak = (1.0 - smoothstep(width, width + aa, across)) * tail * tail *
                    (1.0 - smoothstep(0.0, aa, along));
                float glow = exp2(-length(offset) * 35.0);
                float fade = smoothstep(0.0, 0.2, life) * (1.0 - smoothstep(0.65, 1.0, life));

                return _ShootingColor.rgb * (streak + glow * 0.4) * fade * _ShootingBrightness;
            }
            float3 ShootingStars(float2 position)
            {
                [branch]
                if (_ShootingBrightness <= 0) return 0;
                float2 region = floor(position / 24.0);
                float aa = max(length(fwidth(position)), 0.001);
                float3 light = 0;
                [unroll]
                for (int y = -1; y <= 1; y++)
                [unroll]
                for (int x = -1; x <= 1; x++)
                    light += ShootingRegion(position, region + float2(x, y), aa);
                return light;
            }

            float StarSupport(float radius, float haloRadius, float antialiasWidth, float elongation)
            {
                float blurScale = _Softness > 0 ? 1.2 : 1.0;
                float haloSupport = haloRadius * (blurScale + 0.2 * _ShapeVariation);
                return (max(radius * blurScale, haloSupport) + antialiasWidth) *
                    (1.0 + elongation);
            }

            float LightFalloff(float distanceToCenter, float radius, float antialiasWidth)
            {
                float original = 1.0 - smoothstep(0.0, radius + antialiasWidth, distanceToCenter);
                float normalizedDistance = distanceToCenter / (radius * 1.2 + antialiasWidth);
                float soft = max(0.0, (exp2(-6.0 * normalizedDistance * normalizedDistance) - 0.015625) /
                    0.984375);
                return lerp(original, soft, _Softness);
            }

            float3 EvaluateCell(
                float2 cell,
                float2 positionInCell,
                float antialiasWidth,
                float density,
                float sizeScale,
                float brightnessScale,
                float layerSeed,
                float elongation)
            {
                float2 seededCell = cell + float2(_Seed * 37.0 + layerSeed, _Seed * 91.0 - layerSeed);
                float4 random = Hash42(seededCell);

                if (random.x >= density)
                    return 0;

                float2 center = 0.5 + (random.yz - 0.5) * _PositionJitter;
                float radius = lerp(_StarSizeMin, max(_StarSizeMin, _StarSizeMax), random.w) * sizeScale;
                float distanceToCenter = length(positionInCell - center);
                float4 appearance = Hash42(seededCell + float2(127.1, 311.7));
                float brightness = lerp(0.45, 1.15, appearance.x * appearance.x);
                float haloRadius = radius * _HaloSize * lerp(0.75, 1.25, appearance.y);
                float haloStrength = _HaloStrength * lerp(0.65, 1.25, appearance.z);
                float maxHaloRadius = haloRadius * (1.0 + 0.25 * _TwinkleAmount);
                if (distanceToCenter > StarSupport(radius, maxHaloRadius, antialiasWidth, elongation))
                    return 0;

                float4 shape = Hash42(seededCell + float2(269.5, 183.3));
                float angle = shape.x * TWO_PI;
                float2 axis = float2(cos(angle), sin(angle));
                float2 offset = positionInCell - center;
                float2 local = float2(dot(offset, axis), dot(offset, float2(-axis.y, axis.x)));
                float stretch = 1.0 + elongation * shape.y;
                local *= float2(1.0 / stretch, stretch);
                float core = LightFalloff(length(local), radius, antialiasWidth);

                float phase = random.y * TWO_PI;
                float minimumDuration = max(0.1, min(_TwinkleDurationMin, _TwinkleDurationMax));
                float maximumDuration = max(minimumDuration, max(_TwinkleDurationMin, _TwinkleDurationMax));
                float duration = lerp(minimumDuration, maximumDuration, appearance.w);
                float speed = TWO_PI / duration;
                float cycle = _Time.y * speed + phase;
                float regularWave = sin(cycle) * 0.5 + 0.5;
                float noisyWave = 0.5 + 0.25 * sin(cycle) +
                    0.15 * sin(cycle * 3.0 + shape.z * TWO_PI) +
                    0.1 * sin(cycle * 5.0 + shape.w * TWO_PI);
                float twinkleWave = lerp(regularWave, noisyWave, _TwinkleNoise);
                float twinkle = lerp(1.0 - _TwinkleAmount, 1.0, twinkleWave);
                haloRadius *= 1.0 + (twinkleWave - 0.5) * _TwinkleAmount * 0.5;
                float2 haloOffset = (shape.zw - 0.5) * (0.28 * _ShapeVariation * haloRadius);
                float halo = LightFalloff(length(local - haloOffset), haloRadius, antialiasWidth);
                float intensity = core + halo * haloStrength;

                if (intensity <= 0)
                    return 0;

                float warmBlend = _WarmColorShare > 0
                    ? smoothstep(1.0 - _WarmColorShare, 1.0, random.z)
                    : 0;
                float3 color = lerp(_ColorCool.rgb, _ColorWarm.rgb, warmBlend);
                return color * intensity * brightness * brightnessScale * twinkle * _Brightness;
            }

            float3 EvaluateLayer(
                float2 planePosition,
                float2 cameraPosition,
                float parallax,
                float density,
                float sizeScale,
                float brightnessScale,
                float layerSeed,
                float depth,
                float zoomSizeScale)
            {
                sizeScale *= zoomSizeScale;
                float2 fieldPosition = (planePosition + cameraPosition * ((1.0 + parallax) * _ParallaxScale)) * _CellScale;
                float antialiasWidth = max(length(fwidth(fieldPosition)), 0.0001);
                float maximumRadius = max(_StarSizeMin, _StarSizeMax) * sizeScale;
                float maximumHaloRadius = maximumRadius * _HaloSize * 1.25 * (1.0 + 0.25 * _TwinkleAmount);
                float elongation = _ShapeVariation * (0.4 + _DepthElongation * depth);
                float support = StarSupport(maximumRadius, maximumHaloRadius, antialiasWidth, elongation);
                float centerOffset = 0.5 * _PositionJitter;

                [branch]
                if (support <= 0.5 - centerOffset)
                    return EvaluateCell(floor(fieldPosition), frac(fieldPosition), antialiasWidth,
                        density, sizeScale, brightnessScale, layerSeed, elongation);

                int2 firstCell = (int2)ceil(fieldPosition - 0.5 - centerOffset - support);
                int2 lastCell = (int2)floor(fieldPosition - 0.5 + centerOffset + support);
                float3 stars = 0;
                [loop]
                for (int y = firstCell.y; y <= lastCell.y; y++)
                [loop]
                for (int x = firstCell.x; x <= lastCell.x; x++)
                {
                    float2 cell = float2(x, y);
                    stars += EvaluateCell(cell, fieldPosition - cell, antialiasWidth,
                        density, sizeScale, brightnessScale, layerSeed, elongation);
                }
                return stars;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS);
                output.projectedPosition = output.positionHCS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
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
                float zoom = _ZoomReferenceSize * abs(UNITY_MATRIX_P._m11);
                float2 screenPosition = input.projectedPosition.xy / input.projectedPosition.w;
                float2 referencePosition = screenPosition * _ZoomReferenceSize * float2(
                    abs(UNITY_MATRIX_P._m11) / UNITY_MATRIX_P._m00, sign(UNITY_MATRIX_P._m11));
                float3 referenceOffsetWS = UNITY_MATRIX_V[0].xyz * referencePosition.x +
                    UNITY_MATRIX_V[1].xyz * referencePosition.y;
                float2 referencePlanePosition = float2(dot(referenceOffsetWS, planeRight), dot(referenceOffsetWS, planeUp));
                float2 nebulaPosition = referencePlanePosition * pow(zoom, -_NebulaZoomResponse);
                [branch]
                if (_NebulaForeground > 0.5)
                    return half4(Nebula(nebulaPosition, cameraPosition), 0);
                float2 planePosition = referencePlanePosition * pow(zoom, -_SpacingZoomResponse);
                float zoomSizeScale = pow(zoom, _SizeZoomResponse - _SpacingZoomResponse);

                float nearDensity = _StarDensity * _NearLayerShare * 0.5;
                float farDensity = _StarDensity * (1.0 - _NearLayerShare) * 0.5;
                float3 farStars = EvaluateLayer(
                    planePosition, cameraPosition, _ParallaxFar,
                    farDensity, 0.65, 0.6, 19.19, 1.0, zoomSizeScale);
                float3 middleFarStars = EvaluateLayer(
                    planePosition, cameraPosition, lerp(_ParallaxFar, _ParallaxNear, 1.0 / 3.0),
                    farDensity, 0.85, 0.7333333, 37.37, 2.0 / 3.0, zoomSizeScale);
                float3 middleNearStars = EvaluateLayer(
                    planePosition, cameraPosition, lerp(_ParallaxFar, _ParallaxNear, 2.0 / 3.0),
                    nearDensity, 1.05, 0.8666667, 55.55, 1.0 / 3.0, zoomSizeScale);
                float3 nearStars = EvaluateLayer(
                    planePosition, cameraPosition, _ParallaxNear,
                    nearDensity, 1.25, 1.0, 73.73, 0.0, zoomSizeScale);

                float3 atmosphere = Nebula(nebulaPosition, cameraPosition) +
                    ShootingStars(planePosition + cameraPosition * _ShootingParallax);
                return half4(farStars + middleFarStars + middleNearStars + nearStars + atmosphere, 0);
            }
            ENDHLSL
        }
    }
}
