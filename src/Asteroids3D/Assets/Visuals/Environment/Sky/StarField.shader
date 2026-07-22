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
        _NearLayerShare ("Near Layer Share", Range(0, 1)) = 0.35

        [Header(Appearance)]
        [HDR] _ColorCool ("Cool Star Color", Color) = (0.65, 0.8, 1, 1)
        [HDR] _ColorWarm ("Warm Star Color", Color) = (1, 0.82, 0.58, 1)
        _WarmColorShare ("Warm Color Share", Range(0, 1)) = 0.25
        _Brightness ("Brightness", Range(0, 8)) = 1.5
        _HaloSize ("Halo Size", Range(1, 4)) = 2
        _HaloStrength ("Halo Strength", Range(0, 1)) = 0.2

        [Header(Motion)]
        _TwinkleAmount ("Twinkle Amount", Range(0, 1)) = 0.2
        _TwinkleSpeed ("Twinkle Speed", Range(0, 10)) = 0.5
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
                float4 _ColorCool;
                float4 _ColorWarm;
                float _WarmColorShare;
                float _Brightness;
                float _HaloSize;
                float _HaloStrength;
                float _TwinkleAmount;
                float _TwinkleSpeed;
            CBUFFER_END

            float4 Hash42(float2 value)
            {
                float4 p = frac(value.xyxy * float4(0.1031, 0.1030, 0.0973, 0.1099));
                p += dot(p, p.wzxy + 33.33);
                return frac((p.xxyz + p.yzzw) * p.zywx);
            }

            float3 EvaluateLayer(
                float2 planePosition,
                float2 cameraPosition,
                float parallax,
                float density,
                float sizeScale,
                float layerSeed)
            {
                float2 fieldPosition = (planePosition + cameraPosition * parallax) * _CellScale;
                float2 cell = floor(fieldPosition);
                float4 random = Hash42(cell + float2(_Seed * 37.0 + layerSeed, _Seed * 91.0 - layerSeed));

                if (random.x >= density)
                    return 0;

                float2 center = 0.5 + (random.yz - 0.5) * _PositionJitter;
                float radius = lerp(_StarSizeMin, max(_StarSizeMin, _StarSizeMax), random.w) * sizeScale;
                float distanceToCenter = length(frac(fieldPosition) - center);
                float antialiasWidth = max(fwidth(distanceToCenter), 0.0001);
                float core = 1.0 - smoothstep(radius - antialiasWidth, radius + antialiasWidth, distanceToCenter);
                float haloRadius = radius * _HaloSize;
                float halo = 1.0 - smoothstep(haloRadius - antialiasWidth, haloRadius + antialiasWidth, distanceToCenter);
                float intensity = core + halo * _HaloStrength;

                if (intensity <= 0)
                    return 0;

                float phase = random.y * TWO_PI;
                float twinkleWave = sin(_Time.y * _TwinkleSpeed + phase) * 0.5 + 0.5;
                float twinkle = lerp(1.0 - _TwinkleAmount, 1.0, twinkleWave);
                float warmBlend = _WarmColorShare > 0
                    ? smoothstep(1.0 - _WarmColorShare, 1.0, random.z)
                    : 0;
                float3 color = lerp(_ColorCool.rgb, _ColorWarm.rgb, warmBlend);
                return color * intensity * twinkle * _Brightness;
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

                float nearDensity = _StarDensity * _NearLayerShare;
                float farDensity = _StarDensity * (1.0 - _NearLayerShare);
                float3 farStars = EvaluateLayer(
                    planePosition,
                    cameraPosition,
                    _ParallaxFar,
                    farDensity,
                    0.8,
                    19.19);
                float3 nearStars = EvaluateLayer(
                    planePosition,
                    cameraPosition,
                    _ParallaxNear,
                    nearDensity,
                    1.25,
                    73.73);

                return half4(farStars + nearStars, 0);
            }
            ENDHLSL
        }
    }
}
