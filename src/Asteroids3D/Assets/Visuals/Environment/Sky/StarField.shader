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

        [Header(Depth)]
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
                float3 positionWS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float _Seed;
                float _StarDensity;
                float _CellScale;
                float _StarSizeMin;
                float _StarSizeMax;
                float _PositionJitter;
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
            CBUFFER_END

            float4 Hash42(float2 value)
            {
                float4 p = frac(value.xyxy * float4(0.1031, 0.1030, 0.0973, 0.1099));
                p += dot(p, p.wzxy + 33.33);
                return frac((p.xxyz + p.yzzw) * p.zywx);
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
                float2 fieldPosition = (planePosition + cameraPosition * parallax) * _CellScale;
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
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionHCS = TransformWorldToHClip(output.positionWS);
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
                float2 planePosition = float2(dot(input.positionWS, planeRight), dot(input.positionWS, planeUp));
                float3 cameraPositionWS = GetCameraPositionWS();
                float2 cameraPosition = float2(
                    dot(cameraPositionWS, planeRight),
                    dot(cameraPositionWS, planeUp));

                // Preserve authored scale at the main camera's initial orthographic size.
                float zoom = 7.0 * abs(UNITY_MATRIX_P._m11);
                planePosition = cameraPosition + (planePosition - cameraPosition) *
                    pow(zoom, 1.0 - _SpacingZoomResponse);
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

                return half4(farStars + middleFarStars + middleNearStars + nearStars, 0);
            }
            ENDHLSL
        }
    }
}
