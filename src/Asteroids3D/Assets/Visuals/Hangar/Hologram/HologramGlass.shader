Shader "Astronomical/Hangar/Hologram Glass"
{
    Properties
    {
        _GlassColor ("Glass Color", Color) = (0.82,0.9,1,1)
        _GlassLight ("Glass Light", Range(0,2)) = 0.8
        _RimPower ("Rim Power", Range(0.5,6)) = 2.5
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
            half4 _GlassColor;
            half _GlassLight, _RimPower, _DebrisVisibility;
            half4 _BaseColor;
            half _DamageFlash;
        CBUFFER_END
        ENDHLSL
        Pass
        {
            Name "HologramGlass"
            Stencil { Ref 1 WriteMask 1 Comp Always Pass Replace }
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex GlassVertex
            #pragma fragment GlassFragment
            struct GlassInput
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            struct GlassOutput
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };
            GlassOutput GlassVertex(GlassInput input)
            {
                GlassOutput output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 GlassFragment(GlassOutput input) : SV_Target
            {
                clip(_DebrisVisibility - InterleavedGradientNoise(input.positionCS.xy, 0));
                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                half facing = saturate(dot(normalWS, GetWorldSpaceNormalizeViewDir(input.positionWS)));
                half rim = pow(1.0 - facing, _RimPower);
                return half4(_GlassColor.rgb * _GlassLight * (0.35 + 0.65 * rim), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex GlassDepthVertex
            #pragma fragment GlassDepthFragment
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
            DepthOutput GlassDepthVertex(DepthInput input)
            {
                DepthOutput output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 GlassDepthFragment(DepthOutput input) : SV_Target
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
