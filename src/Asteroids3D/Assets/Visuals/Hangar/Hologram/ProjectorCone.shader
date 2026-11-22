Shader "Astronomical/Hangar/Projector Cone"
{
    Properties
    {
        [PerRendererData] _ConeColor ("Beam Color", Color) = (1, 0.945098, 0.839216, 1)
        [PerRendererData] _ConeLight ("Beam Light", Range(0, 3)) = 1.05
        [PerRendererData] _Level ("Flicker Level", Float) = 1
        [PerRendererData] _Apex ("Lens UV", Vector) = (0.5, 0.222937, 0, 0)
        [PerRendererData] _QuadUnits ("Quad Units", Vector) = (2.304, 1.866, 0, 0)
        [PerRendererData] _SourceRadius ("Source Radius", Float) = 0.32
        [PerRendererData] _Spread ("Spread", Float) = 0.2867454
        [PerRendererData] _Length ("Length", Float) = 1.45
        _Streaks ("Streak Contrast", Range(0, 1)) = 0.55
        _SourceGlow ("Source Glow", Range(0, 2)) = 0.45
        _Haze ("Haze", Range(0, 1)) = 0.16
        _Drift ("Streak Drift", Range(0, 2)) = 0.18
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
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex ConeVertex
            #pragma fragment ConeFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _ConeColor;
                float _ConeLight;
                float _Level;
                float4 _Apex;
                float4 _QuadUnits;
                float _SourceRadius;
                float _Spread;
                float _Length;
                float _Streaks;
                float _SourceGlow;
                float _Haze;
                float _Drift;
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
            half4 ConeFragment(ConeOutput input) : SV_Target
            {
                float2 p = (input.uv - _Apex.xy) * _QuadUnits.xy;
                float across = p.x;
                float along = p.y;
                float height = max(along, 0.0);
                float width = _SourceRadius + height * _Spread;
                float u = across / width;
                float side = exp2(-2.2 * u * u) * (1.0 - smoothstep(0.72, 1.0, abs(u)));
                float seat = _SourceRadius * 0.3;
                float arc = sqrt(saturate(1.0 - (across / _SourceRadius) * (across / _SourceRadius)));
                float foot = smoothstep(-seat, seat * 0.35, along + seat * 0.7 * arc);
                float terminal = 1.0 - smoothstep(_Length * 0.35, _Length, height);
                float fade = terminal * exp2(-1.35 * height / _Length);
                float clock = _Time.y * _Drift;
                float broad = pow(0.5 + 0.5 * sin(u * 18.0 + 0.6 * sin(u * 7.0) + clock), 2.0);
                float fine = pow(max(sin(u * 47.0 + 1.4 * sin(u * 13.0) - clock * 0.7), 0.0), 4.0);
                float detail = (0.28 + 0.55 * broad + 0.35 * fine)
                    * (0.9 + 0.1 * sin(height * 7.0 - clock * 3.0 + u * 6.0));
                float rays = (1.0 - _Streaks) + _Streaks * detail;
                float body = side * foot * fade * rays;
                float coreAcross = across / (_SourceRadius * 0.48);
                float coreAlong = along / (_SourceRadius * 1.3);
                float core = exp2(-3.0 * coreAcross * coreAcross - 2.0 * coreAlong * coreAlong) * foot * terminal;
                float haloDistance = length(float2(across / (_SourceRadius * 1.8), along / (_SourceRadius * 0.65)));
                float halo = exp2(-2.0 * haloDistance * haloDistance)
                    * (1.0 - smoothstep(1.5, 2.0, haloDistance)) * terminal;
                float haze = exp2(-3.5 * u * u) * (1.0 - smoothstep(0.7, 1.3, abs(u))) * foot * fade;
                float energy = _ConeLight * (body + _SourceGlow * (core + 0.25 * halo) + _Haze * haze);
                return half4(_ConeColor.rgb * energy * _Level, 0.0);
            }
            ENDHLSL
        }
    }
}
