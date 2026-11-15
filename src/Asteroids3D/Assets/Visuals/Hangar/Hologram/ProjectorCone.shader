Shader "Astronomical/Hangar/Projector Cone"
{
    Properties
    {
        _ConeColor ("Cone Color", Color) = (1,0.886,0.69,1)
        _ConeLight ("Cone Light", Range(0,1.5)) = 0.5
        _BaseWidth ("Width At Lens (Frame Sides)", Range(0.02,0.5)) = 0.14
        _TopWidth ("Width At Ship (Frame Sides)", Range(0.05,1)) = 0.35
        _Reach ("Reach Toward Ship", Range(0.3,1)) = 0.85
        _Falloff ("Fade On The Way Up", Range(0.2,4)) = 1.1
        _EdgeSoftness ("Edge Softness", Range(0.02,1)) = 0.4
        _Rays ("Rays", Range(0,1)) = 0.3
        _LensGlow ("Lens Glow", Range(0,2)) = 0.7
        [PerRendererData] _Lean ("Lean (Frame Sides)", Float) = 0
        [PerRendererData] _Level ("Flicker Level", Float) = 1
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
                half _ConeLight, _BaseWidth, _TopWidth, _Reach, _Falloff, _EdgeSoftness, _Rays, _LensGlow;
                half _Lean, _Level;
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
                float aspect : TEXCOORD1;
            };
            ConeOutput ConeVertex(ConeInput input)
            {
                ConeOutput output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                // The quad is two frame sides wide; this is its height in frame sides.
                float3 right = float3(UNITY_MATRIX_M[0][0], UNITY_MATRIX_M[1][0], UNITY_MATRIX_M[2][0]);
                float3 up = float3(UNITY_MATRIX_M[0][1], UNITY_MATRIX_M[1][1], UNITY_MATRIX_M[2][1]);
                output.aspect = length(up) / max(0.5 * length(right), 0.0001);
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
                float t = input.uv.y;
                float across = (input.uv.x - 0.5) * 2.0;
                float along = saturate(t / _Reach);
                float halfWidth = lerp(_BaseWidth, _TopWidth, along) * 0.5;
                float u = (across - _Lean * t) / halfWidth;
                float d = abs(u);
                float side = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, d);
                float rise = pow(1.0 - along, _Falloff) * smoothstep(0.0, 0.03, t) * step(t, _Reach);
                float clock = _Time.y;
                float streaks = 0.55 + 0.45 * sin(u * 23.0 + Noise(u * 9.0 + clock * 0.35) * 6.0)
                    * Noise(u * 5.0 - clock * 0.2 + 3.0);
                float beam = _ConeLight * side * rise * lerp(1.0, streaks, _Rays) * (0.75 + 0.25 * (1.0 - saturate(d * d)));
                float2 glow = float2(across / (_BaseWidth * 0.9), t * input.aspect / (_BaseWidth * 0.58));
                beam += _LensGlow * 0.5 * exp(-dot(glow, glow) * 1.6);
                return half4(_ConeColor.rgb * beam * _Level, 0.0);
            }
            ENDHLSL
        }
    }
}
