Shader "Astronomical/Comparison/Shadowed Plate"
{
    Properties
    {
        _BaseMap ("Background Plate", 2D) = "white" {}
        _ShadowColor ("Shadow Tint", Color) = (0.25,0.3,0.5,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _ShadowColor;
            CBUFFER_END
            struct Input { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Output
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };
            Output Vertex(Input input)
            {
                Output output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                return output;
            }
            half4 Fragment(Output input) : SV_Target
            {
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #else
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                Light light = GetMainLight(shadowCoord);
                half3 plate = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                return half4(plate * lerp(_ShadowColor.rgb, 1, light.shadowAttenuation), 1);
            }
            ENDHLSL
        }
    }
}
