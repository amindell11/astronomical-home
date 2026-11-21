Shader "Hangar/ConsoleGlow"
{
    Properties
    {
        [PerRendererData] [HDR] _GlowColor ("Glow Color", Color) = (1, 1, 1, 1)
        _Cone ("Cone", Float) = 0
        [PerRendererData] _TopShare ("Top Share", Float) = 0.18
        [PerRendererData] _Falloff ("Falloff", Float) = 1.4
        _EdgeSoftness ("Edge Softness", Range(0.02, 1)) = 0.6
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "ConsoleGlow"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex GlowVertex
            #pragma fragment GlowFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _GlowColor;
                half _Cone;
                half _TopShare;
                half _Falloff;
                half _EdgeSoftness;
            CBUFFER_END
            struct GlowInput
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            struct GlowOutput
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            GlowOutput GlowVertex(GlowInput input)
            {
                GlowOutput output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            // Alpha 0 lets additive RGB add over the premultiplied console image.
            half4 GlowFragment(GlowOutput input) : SV_Target
            {
                float2 p = (input.uv - 0.5) * 2.0;
                float radial = exp(-dot(p, p) * 4.0) * saturate(1.0 - dot(p, p));
                float t = 1.0 - input.uv.y;
                float halfWidth = lerp(_TopShare, 1.0, t);
                float d = abs((input.uv.x - 0.5) * 2.0) / halfWidth;
                float side = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, d);
                float cone = side * pow(1.0 - t, _Falloff) * (0.75 + 0.25 * (1.0 - saturate(d * d)));
                return half4(_GlowColor.rgb * lerp(radial, cone, _Cone), 0.0);
            }
            ENDHLSL
        }
    }
}
