Shader "Locales/Flat Background"
{
    Properties
    {
        _MainTex ("Clouds", 2D) = "black" {}
        _BaseColor ("Solid Base", Color) = (0, 0, 0, 1)
        _TextureStrength ("Cloud Texture Strength", Range(0, 1)) = 1
        _RepeatDistance ("Repeat Distance (world units)", Float) = 10000
        _ViewHeightInTiles ("View Height (tiles)", Range(0.1, 4)) = 0.1
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
            CBUFFER_START(UnityPerMaterial)
                float _RepeatDistance;
                float _ViewHeightInTiles;
                float4 _BaseColor;
                float _TextureStrength;
            CBUFFER_END
            struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(float3 position : POSITION)
            {
                Varyings output;
                output.position = float4(position.xy, UNITY_RAW_FAR_CLIP_VALUE, 1);
                float2 offset = frac(GetCameraPositionWS().xy / _RepeatDistance);
                float aspect = abs(UNITY_MATRIX_P._m11 / UNITY_MATRIX_P._m00);
                output.uv = position.xy * 0.5 * float2(_ViewHeightInTiles * aspect, _ViewHeightInTiles) + offset;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                return half4(_BaseColor.rgb + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb * _TextureStrength, 1);
            }
            ENDHLSL
        }
    }
}
