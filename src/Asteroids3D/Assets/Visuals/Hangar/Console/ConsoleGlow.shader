Shader "Hangar/ConsoleGlow"
{
    Properties
    {
        [PerRendererData] [HDR] _GlowColor ("Glow Color", Color) = (1, 1, 1, 1)
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
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex GlowVertex
            #pragma fragment GlowFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _GlowColor;
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
                return half4(_GlowColor.rgb * radial, 0.0);
            }
            ENDHLSL
        }
    }
}
