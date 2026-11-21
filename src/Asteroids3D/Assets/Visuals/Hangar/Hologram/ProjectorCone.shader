Shader "Astronomical/Hangar/Projector Cone"
{
    Properties
    {
        [PerRendererData] _ConeColor ("Cone Color", Color) = (1, 0.886, 0.69, 1)
        [PerRendererData] _ConeLight ("Cone Light", Range(0, 3)) = 0.6
        [PerRendererData] _Level ("Flicker Level", Float) = 1
        [PerRendererData] _Apex ("Apex", Vector) = (0.5, 0, 0, 0)
        [PerRendererData] _QuadUnits ("Quad Units", Vector) = (1, 1, 0, 0)
        [PerRendererData] _Axis ("Axis", Vector) = (0, 1, 0, 0)
        [PerRendererData] _Angle ("Angle", Float) = 0.96
        [PerRendererData] _Length ("Length", Float) = 12
        [PerRendererData] _Width ("Width", Float) = 4.5
        [PerRendererData] _Softness ("Softness", Range(0.01, 0.5)) = 0.18
        _Falloff ("Fade With Distance", Range(0.2, 4)) = 1.4
        _CentreBias ("Centre Bias", Range(0, 4)) = 1.5
        _Rays ("Rays", Range(0, 1)) = 0.7
        _RayCount ("Ray Count", Range(4, 120)) = 46
        _RayDrift ("Ray Drift", Range(0, 2)) = 0.25
        _BaseFade ("Base Fade", Range(0, 3)) = 0.6
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-1" }
        Pass
        {
            Name "ProjectorCone"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex ConeVertex
            #pragma fragment ConeFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _ConeColor;
                half _ConeLight;
                half _Level;
                float4 _Apex;
                float4 _QuadUnits;
                float4 _Axis;
                float _Angle;
                float _Length;
                float _Width;
                float _Softness;
                half _Falloff;
                half _CentreBias;
                half _Rays;
                half _RayCount;
                half _RayDrift;
                half _BaseFade;
            CBUFFER_END
            struct ConeInput
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            struct ConeOutput
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            ConeOutput ConeVertex(ConeInput input)
            {
                ConeOutput output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }
            float Noise(float x)
            {
                float cell = floor(x);
                float blend = frac(x);
                blend = blend * blend * (3.0 - 2.0 * blend);
                return lerp(Hash(float2(cell, 7.0)), Hash(float2(cell + 1.0, 7.0)), blend);
            }
            half4 ConeFragment(ConeOutput input) : SV_Target
            {
                float2 p = (input.uv - _Apex.xy) * _QuadUnits.xy;
                float2 axis = _Axis.xy;
                if (dot(p, axis) < 0.0)
                    return half4(0.0, 0.0, 0.0, 0.0);
                float r = length(p);
                float a = atan2(axis.x * p.y - axis.y * p.x, dot(p, axis));
                float across = max(abs(a) / _Angle, abs(p.x * axis.y - p.y * axis.x) / _Width);
                float edge = 1.0 - smoothstep(1.0 - _Softness, 1.0, across);
                float along = saturate(r / _Length);
                float fall = pow(1.0 - along, _Falloff);
                float centre = pow(saturate(cos(across * 1.5707963)), _CentreBias);
                float n1 = Noise(a * _RayCount + _Time.y * _RayDrift);
                float n2 = Noise(a * _RayCount * 2.7 - _Time.y * _RayDrift * 1.7 + 11.0);
                float rays = lerp(1.0, saturate(n1 * 1.4 - 0.2) * (0.6 + 0.4 * n2), _Rays);
                rays = lerp(1.0, rays, saturate(r / (_Length * 0.35)));
                float base = smoothstep(0.0, _BaseFade, r);
                float beam = _ConeLight * edge * fall * lerp(0.35, 1.0, centre) * rays * base;
                return half4(_ConeColor.rgb * beam * _Level, 0.0);
            }
            ENDHLSL
        }
    }
}
