Shader "Astronomical/Vfx/Engine Flame"
{
    Properties
    {
        _BandsMap ("Bands", 2D) = "black" {}
        _GlowMap ("Glow", 2D) = "black" {}
        _Columns ("Columns", Float) = 4
        _Rows ("Rows", Float) = 2
        _RimShade ("Rim Shade", Float) = 0.5
        _RimAlpha ("Rim Alpha", Float) = 0.78
        _MidAlpha ("Mid Alpha", Float) = 0.88
        _Glow ("Glow", Float) = 0.72
        [HideInInspector] _Frame ("Frame", Float) = 0
        [HideInInspector] _BaseColor ("Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BandsMap); SAMPLER(sampler_BandsMap);
            TEXTURE2D(_GlowMap); SAMPLER(sampler_GlowMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BandsMap_ST;
                float4 _GlowMap_ST;
                float _Columns;
                float _Rows;
                float _RimShade;
                float _RimAlpha;
                float _MidAlpha;
                float _Glow;
                float _Frame;
                float4 _BaseColor;
            CBUFFER_END
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };
            Varying Vert(Input v) { Varying o; o.position=TransformObjectToHClip(v.vertex.xyz); o.uv=v.uv; return o; }
            half4 Frag(Varying i):SV_Target
            {
                float col = fmod(_Frame, _Columns);
                float row = floor(_Frame / _Columns);
                float2 uv = float2((col + i.uv.x) / _Columns, 1 - (row + 1 - i.uv.y) / _Rows);
                float3 m = SAMPLE_TEXTURE2D(_BandsMap, sampler_BandsMap, uv).rgb;
                // The baked blur is clipped at the cell's sides and tail; fade it out before the quad edge.
                float edge = smoothstep(0.0, 0.2, i.uv.x) * smoothstep(0.0, 0.2, 1.0 - i.uv.x) * smoothstep(0.0, 0.15, i.uv.y);
                float g = SAMPLE_TEXTURE2D(_GlowMap, sampler_GlowMap, uv).r * edge;
                float3 b = _BaseColor.rgb;
                float3 rim = b * _RimShade;
                float3 mid = min(b * 1.05 + 0.12, float3(1.0, 1.0, 1.0));
                float3 core = min(0.75 + 0.3 * b, float3(1.0, 1.0, 1.0));
                float3 tint = rim;
                tint = lerp(tint, mid, m.g);
                tint = lerp(tint, core, m.b);
                float a = max(max(m.r * _RimAlpha, m.g * _MidAlpha), m.b);
                float3 rgb = tint * a + b * g * _Glow;
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
}
