Shader "Environment/Flat Background"
{
    Properties
    {
        _MainTex ("Clouds", 2D) = "black" {}
        _RepeatDistance ("Repeat Distance (world units)", Float) = 20000
        _ViewHeightInTiles ("View Height (tiles)", Range(0.1, 4)) = 0.5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-100" "RenderType"="Opaque" }
        Pass
        {
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _Mapping;
            struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(float3 position : POSITION)
            {
                Varyings output;
                output.position = float4(position.xy, UNITY_RAW_FAR_CLIP_VALUE, 1);
                output.uv = position.xy * 0.5 * _Mapping.zw + _Mapping.xy;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                return half4(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb, 1);
            }
            ENDHLSL
        }
    }
}
