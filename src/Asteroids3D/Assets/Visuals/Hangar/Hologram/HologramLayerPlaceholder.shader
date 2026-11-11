Shader "Astronomical/Hangar/Hologram Layer Placeholder"
{
    Properties
    {
        [PerRendererData] _MainTex ("Stage Image", 2D) = "black" {}
        _Opacity ("Layer Opacity", Range(0, 1)) = 1
        _EdgeFeather ("Edge Feather", Range(0, 0.5)) = 0.12
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
            half4 Frag(Varyings input) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half edge = min(min(input.uv.x, 1 - input.uv.x), min(input.uv.y, 1 - input.uv.y));
                half fade = _Opacity * smoothstep(0, _EdgeFeather, edge);
                return c * fade;
            }
            ENDHLSL
        }
    }
}
