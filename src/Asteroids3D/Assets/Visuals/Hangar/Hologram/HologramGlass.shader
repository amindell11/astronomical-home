Shader "Astronomical/Hangar/Hologram Surface Placeholder"
{
    Properties
    {
        _HologramColor ("Hologram Color", Color) = (0.35,0.85,1,1)
        [PerRendererData] _BaseMap ("Ship Paint", 2D) = "white" {}
        _DebrisVisibility ("Debris Visibility", Range(0,1)) = 1
        [PerRendererData] _BaseColor ("Ship Tint", Color) = (1,1,1,1)
        [PerRendererData] _DamageFlash ("Ship Flash", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _HologramColor;
            half _DebrisVisibility;
            half4 _BaseColor;
            half _DamageFlash;
        CBUFFER_END
        ENDHLSL
        Pass
        {
            Name "HologramSurface"
            Stencil { Ref 1 WriteMask 1 Comp Always Pass Replace }
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex HologramVertex
            #pragma fragment HologramFragment
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            struct HologramInput
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            struct HologramOutput
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            HologramOutput HologramVertex(HologramInput input)
            {
                HologramOutput output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }
            half4 HologramFragment(HologramOutput input) : SV_Target
            {
                clip(_DebrisVisibility - InterleavedGradientNoise(input.positionCS.xy, 0));
                half3 paint = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                half luminance = dot(paint, half3(0.2126, 0.7152, 0.0722));
                return half4(_HologramColor.rgb * lerp(0.55, 1, luminance), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex HologramDepthVertex
            #pragma fragment HologramDepthFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            struct DepthInput
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            struct DepthOutput
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
            };
            DepthOutput HologramDepthVertex(DepthInput input)
            {
                DepthOutput output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 HologramDepthFragment(DepthOutput input) : SV_Target
            {
                clip(_DebrisVisibility - InterleavedGradientNoise(input.positionCS.xy, 0));
                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = saturate(PackNormalOctQuadEncode(normalWS) * 0.5 + 0.5);
                    return half4(PackFloat2To888(oct), 0);
                #else
                    return half4(normalWS, 0);
                #endif
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            float4 DepthVertex(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }
            half DepthFragment(float4 positionCS : SV_POSITION) : SV_Target
            {
                clip(_DebrisVisibility - InterleavedGradientNoise(positionCS.xy, 0));
                return positionCS.z;
            }
            ENDHLSL
        }
    }
}
