Shader "Astronomical/Hangar/Hologram Layer"
{
    Properties
    {
        [PerRendererData] _MainTex ("Stage Image", 2D) = "black" {}
        _Opacity ("Layer Opacity", Range(0, 1)) = 1
        _EdgeFeather ("Edge Feather", Range(0, 0.5)) = 0.12
        [PerRendererData] _ReachApex ("Reach Apex", Vector) = (0.5, 0.5, 0, 0)
        [PerRendererData] _ReachUnits ("Reach Units", Vector) = (1, 1, 0, 0)
        [PerRendererData] _ReachAxis ("Reach Axis", Vector) = (0, 1, 0, 0)
        [PerRendererData] _ReachAngle ("Reach Angle", Float) = 0.96
        [PerRendererData] _ReachLength ("Reach Length", Float) = 12
        [PerRendererData] _ReachSoftness ("Reach Softness", Range(0.01, 0.5)) = 0.18
        [PerRendererData] _Sputter ("Sputter", Range(0, 1)) = 0.7
        [PerRendererData] _SputterLines ("Sputter Lines", Range(20, 600)) = 220
        [PerRendererData] _EdgeFringe ("Edge Fringe", Range(0, 4)) = 1.2
        [PerRendererData] _FringeColor ("Fringe Color", Color) = (1, 0.886, 0.69, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Opacity;
                half _EdgeFeather;
                float4 _ReachApex;
                float4 _ReachUnits;
                float4 _ReachAxis;
                float _ReachAngle;
                float _ReachLength;
                float _ReachSoftness;
                float _Sputter;
                float _SputterLines;
                float _EdgeFringe;
                half4 _FringeColor;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float2 p = (uv - _ReachApex.xy) * _ReachUnits.xy;
                float r = length(p);
                float ang = acos(saturate(dot(p / max(r, 1e-4), _ReachAxis.xy)));
                float band = _ReachAngle * _ReachSoftness;
                float inside = saturate((_ReachAngle - ang) / band)
                    * saturate((_ReachLength - r) / (_ReachLength * _ReachSoftness));
                float sputterOn = (inside > 0.0 && inside < 1.0) ? 1.0 : 0.0;
                float row = floor(uv.y * _SputterLines);
                float t = floor(_Time.y * 24.0);
                float keep = Hash(float2(row, t)) < lerp(1.0 - _Sputter, 1.0, inside) ? 1.0 : 0.0;
                float shift = (Hash(float2(row, t + 17.0)) - 0.5) * 0.02 * _Sputter * (1.0 - inside)
                    * sputterOn * keep;
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(shift, 0.0));
                c *= 1.0 - sputterOn * (1.0 - keep);
                c.rgb += _FringeColor.rgb * (_EdgeFringe * c.a * (1.0 - inside) * inside * 4.0);
                c *= step(1e-3, inside);
                half edge = min(min(uv.x, 1 - uv.x), min(uv.y, 1 - uv.y));
                half fade = _Opacity * smoothstep(0, _EdgeFeather, edge);
                return c * fade;
            }
            ENDHLSL
        }
    }
}
